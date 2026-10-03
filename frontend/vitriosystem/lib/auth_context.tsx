"use client";

import {
  createContext,
  useContext,
  useEffect,
  useState,
  useCallback,
  type ReactNode,
} from "react";
import { useRouter } from "next/navigation";
import { logout as clearSession, getMe, bootstrapSession, ROLES, type User } from "./api";

/** Lojista ou admin — quem pode usar o painel. Clientes da vitrine não. */
export function isStaff(user: User | null): boolean {
  return !!user && (user.role === ROLES.SHOPKEEPER || user.role === ROLES.ADMIN);
}

// Páginas da vitrine (/store/{slug}/client). Lá o cliente pode estar sem login,
// então perder a sessão não deve mandar ninguém para o login do painel.
function isStorefrontPath() {
  return typeof window !== "undefined" && /^\/store\/[^/]+\/client/.test(window.location.pathname);
}

interface AuthContextValue {
  user: User | null;
  loading: boolean;
  /** Sai e vai para o login do painel. */
  logout: () => Promise<void>;
  /** Sai e continua na página atual (usado na vitrine). */
  logoutHere: () => Promise<void>;
  refresh: () => Promise<void>;
  // Recarrega os dados do usuário sem ligar o "loading" global
  // (usado depois de editar o perfil, pra não desmontar a tela).
  reloadUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null);
  const [loading, setLoading] = useState(true);
  const router = useRouter();

  const logoutHere = useCallback(async () => {
    await clearSession();
    setUser(null);
  }, []);

  const logout = useCallback(async () => {
    await logoutHere();
    router.push("/auth/login");
  }, [logoutHere, router]);

  const refresh = useCallback(async () => {
    setLoading(true);

    try {
      // Tenta recuperar um access token novo a partir do cookie HttpOnly.
      const hasSession = await bootstrapSession();

      if (!hasSession) {
        setUser(null);
        return;
      }

      const { dados } = await getMe();
      setUser(dados);
    } catch {
      setUser(null);
    } finally {
      setLoading(false);
    }
  }, []);

  const reloadUser = useCallback(async () => {
    try {
      const { dados } = await getMe();
      if (dados) setUser(dados);
    } catch {
      // mantém o usuário atual; um 401 real já dispara "auth:unauthorized"
    }
  }, []);

  useEffect(() => {
    refresh();
  }, [refresh]);

  useEffect(() => {
    function handleUnauthorized() {
      setUser(null);
      if (!isStorefrontPath()) router.push("/auth/login");
    }

    window.addEventListener("auth:unauthorized", handleUnauthorized);
    return () => window.removeEventListener("auth:unauthorized", handleUnauthorized);
  }, [router]);

  return (
    <AuthContext.Provider value={{ user, loading, logout, logoutHere, refresh, reloadUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth precisa ser usado dentro de <AuthProvider>");
  return ctx;
}

// Painel: exige lojista/admin. Cliente da vitrine logado também é mandado
// para o login do painel (lá ele vê o aviso de que a conta é de cliente).
export function useRequireAuth() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const allowed = isStaff(user);

  useEffect(() => {
    if (!loading && !allowed) {
      router.replace("/auth/login");
    }
  }, [loading, allowed, router]);

  return { user: allowed ? user : null, loading };
}

export function useGuestOnly() {
  const { user, loading } = useAuth();
  const router = useRouter();

  // Só lojista/admin é mandado para o painel; cliente logado pode ver o login do painel.
  const staff = isStaff(user);

  useEffect(() => {
    if (!loading && staff) {
      router.replace("/menu/initialpage");
    }
  }, [loading, staff, router]);

  return { loading };
}

export function useRequireRole(allowedRoles: string[]) {
  const { user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (loading) return;

    if (!user) {
      router.replace("/auth/login");
      return;
    }

    if (!allowedRoles.includes(user.role)) {
      router.replace("/menu/initialpage");
    }
  }, [loading, user, allowedRoles, router]);

  return { user, loading };
}
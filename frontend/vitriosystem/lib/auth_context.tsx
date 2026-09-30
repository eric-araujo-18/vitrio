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
import { logout as clearSession, getMe, bootstrapSession, type User } from "./api";

interface AuthContextValue {
  user: User | null;
  loading: boolean;
  logout: () => void;
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

  const logout = useCallback(async () => {
    await clearSession();
    setUser(null);
    router.push("/auth/login");
  }, [router]);

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
      router.push("/auth/login");
    }

    window.addEventListener("auth:unauthorized", handleUnauthorized);
    return () => window.removeEventListener("auth:unauthorized", handleUnauthorized);
  }, [router]);

  return (
    <AuthContext.Provider value={{ user, loading, logout, refresh, reloadUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error("useAuth precisa ser usado dentro de <AuthProvider>");
  return ctx;
}

export function useRequireAuth() {
  const { user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && !user) {
      router.replace("/auth/login");
    }
  }, [loading, user, router]);

  return { user, loading };
}

export function useGuestOnly() {
  const { user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && user) {
      router.replace("/menu/initialpage");
    }
  }, [loading, user, router]);

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
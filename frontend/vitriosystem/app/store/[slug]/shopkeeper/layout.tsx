"use client";

import { useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { ArrowLeft, LoaderCircle, Store as StoreIcon } from "lucide-react";
import { getMyStores, ROLES, type Store } from "@/lib/api";
import { useAuth } from "@/lib/auth_context";
import SidebarShopkeeper from "./components/Sidebar/SidebarShopkeeper";
import { ShopkeeperStoreProvider } from "./components/ShopkeeperStoreContext";
import { WarningBox, btnPrimary } from "../shopkeeper/components/Ui";

// Layout comum de TODAS as telas do painel da loja:
// 1) exige login de lojista/admin
// 2) resolve a loja pelo slug (só entre as lojas do próprio usuário)
// 3) renderiza a sidebar uma única vez e entrega a loja via contexto
export default function ShopkeeperLayout({ children }: { children: ReactNode }) {
  const { slug } = useParams<{ slug: string }>();
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();

  // Resultado da busca da loja, guardado junto com o slug: se o slug muda, volta a "carregando"
  // sem precisar de setState dentro do efeito.
  const [loaded, setLoaded] = useState<{
    slug: string;
    store: Store | null;
    status: "ready" | "notfound" | "error";
  } | null>(null);
  const current = loaded?.slug === slug ? loaded : null;
  const store = current?.store ?? null;
  const status = current?.status ?? "loading";
  const setStore = (updated: Store) => setLoaded({ slug, store: updated, status: "ready" });

  useEffect(() => {
    if (authLoading) return;

    if (!user) {
      router.replace("/auth/login");
      return;
    }

    if (user.role !== ROLES.SHOPKEEPER && user.role !== ROLES.ADMIN) {
      router.replace(`/store/${slug}`);
      return;
    }

    let active = true;

    getMyStores()
      .then(({ dados }) => {
        if (!active) return;
        const found = dados?.find((s) => s.slug === slug) ?? null;
        setLoaded({ slug, store: found, status: found ? "ready" : "notfound" });
      })
      .catch(() => active && setLoaded({ slug, store: null, status: "error" }));

    return () => {
      active = false;
    };
  }, [authLoading, user, slug, router]);

  if (authLoading || status === "loading" || !user) {
    return (
      <div
        role="status"
        className="flex min-h-screen items-center justify-center gap-2 bg-surface text-body-md text-on-surface-variant"
      >
        <LoaderCircle size={20} aria-hidden="true" className="animate-spin text-primary-container" />
        Carregando painel...
      </div>
    );
  }

  if (status !== "ready" || !store) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-surface p-4 text-center">
        <div className="flex h-16 w-16 items-center justify-center rounded-2xl bg-primary-fixed/50 text-primary-container">
          <StoreIcon size={30} aria-hidden="true" />
        </div>
        <p className="max-w-sm text-body-lg text-on-surface-variant">
          {status === "notfound"
            ? "Loja não encontrada ou você não tem acesso a ela."
            : "Erro ao carregar a loja."}
        </p>
        <Link href="/menu/stores" className={btnPrimary}>
          <ArrowLeft size={16} aria-hidden="true" />
          Voltar para minhas lojas
        </Link>
      </div>
    );
  }

  return (
    <ShopkeeperStoreProvider value={{ store, setStore }}>
      <div className="flex min-h-screen bg-surface text-on-surface antialiased">
        <SidebarShopkeeper />
        <main className="min-w-0 flex-1 animate-[fadeIn_0.3s_ease] p-5 md:px-10 md:py-8">
          {store.blockedByPlan && (
            <div className="mb-5">
              <WarningBox>
                Esta loja está fora do ar e não recebe pedidos novos, porque seu plano permite menos lojas no ar do
                que você tem. Os pedidos já feitos continuam aqui. Para colocá-la no ar, pause outra loja ou{" "}
                <Link href="/menu/subscription" className="font-semibold underline">
                  veja os planos
                </Link>
                .
              </WarningBox>
            </div>
          )}
          {children}
        </main>
      </div>
    </ShopkeeperStoreProvider>
  );
}
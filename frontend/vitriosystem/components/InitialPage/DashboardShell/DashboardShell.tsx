"use client";

import type { ReactNode } from "react";
import { LoaderCircle } from "lucide-react";
import SidebarInitialPage from "@/components/InitialPage/Sidebar/SidebarInitialPage";
import Header from "@/components/InitialPage/Header/Header";
import { useRequireAuth } from "@/lib/auth_context";
import type { User } from "@/lib/api";

interface DashboardShellProps {
  title?: ReactNode;
  subtitle?: ReactNode;
  // Função pra ter acesso ao usuário já carregado (nunca null aqui dentro).
  children: (user: User) => ReactNode;
}

/*
  Estrutura comum das telas de /menu/*: exige login, mostra sidebar + header
  e só renderiza o conteúdo quando o usuário já foi carregado.

  O espaço reservado para a sidebar usa os MESMOS pontos de quebra dela:
  - até 576px   → sidebar vira barra embaixo (70px) → espaço no fim da página
  - 577–992px   → sidebar de 90px
  - 993px ou +  → sidebar de 260px
*/
export default function DashboardShell({ title, subtitle, children }: DashboardShellProps) {
  const { user, loading } = useRequireAuth();

  if (loading) {
    return (
      <div
        role="status"
        className="flex min-h-screen items-center justify-center gap-2 bg-surface text-body-md text-on-surface-variant"
      >
        <LoaderCircle size={20} aria-hidden="true" className="animate-spin text-primary-container" />
        Carregando...
      </div>
    );
  }

  // useRequireAuth já disparou o redirect pro login
  if (!user) return null;

  return (
    <div className="flex min-h-screen bg-surface text-on-surface antialiased">
      <SidebarInitialPage />

      <main className="flex min-w-0 flex-1 flex-col pb-[calc(70px+env(safe-area-inset-bottom))] min-[577px]:ml-[90px] min-[577px]:pb-0 min-[993px]:ml-[260px]">
        <Header />

        <section className="animate-[fadeIn_0.35s_ease] p-6 md:p-10">
          {(title || subtitle) && (
            <div className="mb-9">
              {title && (
                <h1 className="mb-2 text-display-lg-mobile text-on-surface md:text-display-lg">
                  {title}
                </h1>
              )}
              {subtitle && <p className="text-body-lg text-on-surface-variant">{subtitle}</p>}
            </div>
          )}

          {children(user)}
        </section>
      </main>
    </div>
  );
}
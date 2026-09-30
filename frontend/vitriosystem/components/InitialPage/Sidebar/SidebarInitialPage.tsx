"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  CircleHelp,
  CreditCard,
  Globe,
  LayoutDashboard,
  LogOut,
  Settings,
  Store,
  User,
  type LucideIcon,
} from "lucide-react";
import { useAuth } from "@/lib/auth_context";

interface NavItem {
  label: string;
  href: string;
  icon: LucideIcon;
}

const NAV_ITEMS: NavItem[] = [
  { label: "Dashboard", href: "/menu/initialpage", icon: LayoutDashboard },
  { label: "Minha Conta", href: "/menu/myaccount", icon: User },
  { label: "Lojas", href: "/menu/stores", icon: Store },
  { label: "Domínios", href: "/menu/domains", icon: Globe },
  { label: "Assinatura", href: "/menu/subscription", icon: CreditCard },
  { label: "Configurações", href: "/menu/settings", icon: Settings },
  { label: "Suporte", href: "/menu/support", icon: CircleHelp },
];

/*
  Os três modos seguem os mesmos pontos de quebra do CSS antigo,
  para não desalinhar com o DashboardShell:
  - até 576px   → barra fixa embaixo, só ícones
  - 577–992px   → lateral estreita (90px), só ícones
  - 993px ou +  → lateral completa (260px), ícone + texto
*/

function isActive(pathname: string, href: string) {
  // Mantém o item ativo também em subpáginas (ex: /menu/stores/123).
  return pathname === href || pathname.startsWith(`${href}/`);
}

export default function SidebarInitialPage() {
  const pathname = usePathname();
  const { logout } = useAuth();

  return (
    <aside className="fixed inset-x-0 bottom-0 z-[999] flex h-[70px] items-center border-t border-slate-200/85 bg-white/90 pb-[env(safe-area-inset-bottom)] backdrop-blur-xl min-[577px]:inset-y-0 min-[577px]:right-auto min-[577px]:h-auto min-[577px]:w-[90px] min-[577px]:flex-col min-[577px]:items-stretch min-[577px]:border-t-0 min-[577px]:border-r min-[577px]:bg-white min-[577px]:pb-0 min-[993px]:w-[260px]">
      {/* Logo */}
      <Link
        href="/menu/initialpage"
        className="hidden h-20 shrink-0 items-center justify-center gap-2 border-b border-slate-100 px-6 min-[577px]:flex min-[993px]:justify-start"
      >
        <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-primary text-on-primary">
          <Store size={20} aria-hidden="true" />
        </span>
        <span className="hidden text-headline-sm font-bold tracking-tight text-on-surface min-[993px]:inline">
          Vitrio<span className="text-primary">System</span>
        </span>
      </Link>

      {/* Navegação */}
      <nav
        aria-label="Menu principal"
        className="flex w-full items-center justify-around px-2 min-[577px]:flex-1 min-[577px]:flex-col min-[577px]:items-stretch min-[577px]:justify-start min-[577px]:gap-1 min-[577px]:overflow-y-auto min-[577px]:p-4"
      >
        {NAV_ITEMS.map(({ label, href, icon: Icon }) => {
          const active = isActive(pathname, href);

          return (
            <Link
              key={href}
              href={href}
              aria-label={label}
              title={label}
              aria-current={active ? "page" : undefined}
              className={`group flex items-center justify-center gap-3 rounded-lg p-2.5 text-label-md transition-colors duration-200 min-[577px]:py-3 min-[993px]:justify-start min-[993px]:px-3.5 ${
                active
                  ? "bg-primary-fixed/50 font-semibold text-primary"
                  : "text-on-surface-variant hover:bg-primary-container/6 hover:text-primary-container"
              }`}
            >
              <Icon
                size={20}
                aria-hidden="true"
                className="shrink-0 transition-transform duration-200 group-hover:scale-110"
              />
              <span className="hidden min-[993px]:inline">{label}</span>
            </Link>
          );
        })}
      </nav>

      {/* Sair */}
      <div className="hidden border-t border-slate-100 p-4 min-[577px]:block">
        <button
          type="button"
          onClick={logout}
          aria-label="Sair"
          title="Sair"
          className="group flex w-full items-center justify-center gap-3 rounded-lg p-3 text-label-md font-semibold text-red-600 transition-colors duration-200 hover:bg-red-50 hover:text-red-700 min-[993px]:justify-start min-[993px]:px-3.5"
        >
          <LogOut
            size={20}
            aria-hidden="true"
            className="shrink-0 transition-transform duration-200 group-hover:translate-x-0.5"
          />
          <span className="hidden min-[993px]:inline">Sair</span>
        </button>
      </div>
    </aside>
  );
}
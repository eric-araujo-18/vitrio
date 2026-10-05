"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { CSSProperties } from "react";
import {
  ArrowLeft,
  ExternalLink,
  LayoutDashboard,
  LogOut,
  Package,
  Palette,
  Settings,
  ShoppingCart,
  Store as StoreIcon,
  Tags,
  type LucideIcon,
} from "lucide-react";
import { useAuth } from "@/lib/auth_context";
import { useShopkeeperStore } from "../ShopkeeperStoreContext";
import { StatusBadge } from "../Ui";

interface NavItem {
  label: string;
  href: string;
  icon: LucideIcon;
  /** true = precisa ser rota exata; false = também ativa em sub-rotas */
  exact?: boolean;
  /** Contador discreto ao lado do item (ex.: pedidos pendentes). 0 = não mostra. */
  badge?: number;
}

/*
  Até 900px vira um trilho só de ícones (64px); acima disso, 240px com texto.
  A cor de destaque acompanha a cor da loja via --sb-accent / --sb-accent-dark.
*/

const footerLinkClass =
  "flex w-full items-center justify-center gap-2.5 rounded-lg p-2.5 text-left text-body-md font-medium text-on-surface-variant transition-colors hover:bg-slate-100 hover:text-on-surface min-[901px]:justify-start min-[901px]:px-2.5 min-[901px]:py-2";

export default function SidebarShopkeeper() {
  const pathname = usePathname();
  const { logout } = useAuth();
  // A loja vem do layout (já validada como sendo do usuário logado),
  // então a sidebar não precisa buscar nada sozinha.
  const { store, pendingOrders } = useShopkeeperStore();

  const basePath = `/store/${store.slug}/shopkeeper`;

  const navItems: NavItem[] = [
    { label: "Início", href: basePath, icon: LayoutDashboard, exact: true },
    { label: "Produtos", href: `${basePath}/products`, icon: Package },
    { label: "Categorias", href: `${basePath}/categories`, icon: Tags },
    { label: "Pedidos", href: `${basePath}/pedidos`, icon: ShoppingCart, badge: pendingOrders },
    { label: "Personalização", href: `${basePath}/personalizacao`, icon: Palette },
    { label: "Configurações", href: `${basePath}/configuracoes`, icon: Settings },
  ];

  const isItemActive = (item: NavItem) => {
    if (!pathname) return false;
    return item.exact ? pathname === item.href : pathname.startsWith(item.href);
  };

  const accentVars = {
    "--sb-accent": store.primaryColor || "#2563eb",
    "--sb-accent-dark": store.secondaryColor || "#1d4ed8",
  } as CSSProperties;

  return (
    <aside
      style={accentVars}
      className="sticky top-0 flex h-screen w-16 shrink-0 flex-col border-r border-slate-200/85 bg-white text-on-surface min-[901px]:w-60"
    >
      {/* Cabeçalho com a loja */}
      <div className="flex items-center justify-center gap-2.5 border-b border-slate-200/85 py-4 min-[901px]:justify-start min-[901px]:px-4 min-[901px]:py-5">
        <div
          aria-hidden="true"
          title={store.name}
          className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-[var(--sb-accent)] text-white"
        >
          {store.logoUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={store.logoUrl} alt="" className="h-full w-full object-cover" />
          ) : (
            <StoreIcon size={18} strokeWidth={2.25} />
          )}
        </div>

        <div className="hidden min-w-0 flex-col gap-1 min-[901px]:flex">
          <span className="truncate text-body-md leading-tight font-semibold">{store.name}</span>
          <StatusBadge status={!store.isActive ? "Pending" : store.blockedByPlan ? "Blocked" : "Active"}>
            {!store.isActive ? "Pausada" : store.blockedByPlan ? "Fora do ar" : "Ativa"}
          </StatusBadge>
        </div>
      </div>

      {/* Navegação */}
      <nav aria-label="Menu da loja" className="flex-1 overflow-y-auto px-3 pt-4 pb-2">
        <span className="hidden px-2 pb-2.5 text-label-sm font-bold tracking-wider text-on-surface-variant uppercase min-[901px]:block">
          Gerenciar
        </span>

        <ul className="flex flex-col gap-1">
          {navItems.map((item) => {
            const active = isItemActive(item);
            const Icon = item.icon;
            const badge = item.badge ?? 0;
            const label = badge > 0 ? `${item.label} (${badge} pendente${badge === 1 ? "" : "s"})` : item.label;
            return (
              <li key={item.href}>
                <Link
                  href={item.href}
                  aria-current={active ? "page" : undefined}
                  aria-label={label}
                  title={label}
                  className={`relative flex items-center justify-center gap-2.5 rounded-lg p-2.5 text-body-md transition-colors min-[901px]:justify-start min-[901px]:px-3 min-[901px]:py-2 ${
                    active
                      ? "bg-[color-mix(in_srgb,var(--sb-accent)_12%,white)] font-semibold text-[var(--sb-accent-dark)] before:absolute before:top-1/2 before:-left-3 before:h-3/5 before:w-[3px] before:-translate-y-1/2 before:rounded-r before:bg-[var(--sb-accent)]"
                      : "font-medium text-on-surface-variant hover:bg-slate-100 hover:text-on-surface"
                  }`}
                >
                  <Icon size={20} strokeWidth={2} aria-hidden="true" className="shrink-0" />
                  <span className="hidden min-[901px]:inline">{item.label}</span>

                  {badge > 0 && (
                    <>
                      {/* Barra larga: número pequeno à direita. Trilho de ícones: só um ponto. */}
                      <span
                        aria-hidden="true"
                        className="ml-auto hidden min-w-5 rounded-full bg-[color-mix(in_srgb,var(--sb-accent)_14%,white)] px-1.5 text-center text-[11px] leading-5 font-semibold text-[var(--sb-accent-dark)] min-[901px]:inline"
                      >
                        {badge > 99 ? "99+" : badge}
                      </span>
                      <span
                        aria-hidden="true"
                        className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full bg-[var(--sb-accent)] ring-2 ring-white min-[901px]:hidden"
                      />
                    </>
                  )}
                </Link>
              </li>
            );
          })}
        </ul>
      </nav>

      {/* Rodapé */}
      <div className="flex flex-col gap-0.5 border-t border-slate-200/85 p-3">
        <Link
          href={`/store/${store.slug}/client`}
          target="_blank"
          rel="noopener noreferrer"
          aria-label="Ver vitrine"
          title="Ver vitrine"
          className={footerLinkClass}
        >
          <ExternalLink size={17} strokeWidth={2} aria-hidden="true" className="shrink-0" />
          <span className="hidden min-[901px]:inline">Ver vitrine</span>
        </Link>
        <Link href="/menu/stores" aria-label="Minhas lojas" title="Minhas lojas" className={footerLinkClass}>
          <ArrowLeft size={17} strokeWidth={2} aria-hidden="true" className="shrink-0" />
          <span className="hidden min-[901px]:inline">Minhas lojas</span>
        </Link>
        <button
          type="button"
          onClick={logout}
          aria-label="Sair"
          title="Sair"
          className={`${footerLinkClass} hover:bg-red-50! hover:text-red-600!`}
        >
          <LogOut size={17} strokeWidth={2} aria-hidden="true" className="shrink-0" />
          <span className="hidden min-[901px]:inline">Sair</span>
        </button>
      </div>
    </aside>
  );
}
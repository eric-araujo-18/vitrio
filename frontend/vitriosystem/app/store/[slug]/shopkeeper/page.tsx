"use client";

import { useEffect, useState, type ReactNode } from "react";
import Link from "next/link";
import {
  AlertTriangle,
  ChevronRight,
  DollarSign,
  ExternalLink,
  Inbox,
  Package,
  PauseCircle,
  ShoppingCart,
  Tags,
  type LucideIcon,
} from "lucide-react";
import { unwrap } from "@/lib/api";
import { getStoreDashboard, type StoreDashboard } from "@/lib/api_order";
import { formatDateTime, formatPrice, ORDER_STATUS_LABELS } from "@/lib/format";
import { useShopkeeperStore } from "./components/ShopkeeperStoreContext";
import {
  ErrorBox,
  LoadingState,
  PageHeader,
  StatusBadge,
  btnSecondary,
  card,
  cardSubtitle,
  cardTitle,
  table,
  tableWrapper,
} from "./components/Ui";

export default function ShopkeeperDashboardPage() {
  const { store } = useShopkeeperStore();
  const [data, setData] = useState<StoreDashboard | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    unwrap(getStoreDashboard(store.id))
      .then((d) => active && setData(d))
      .catch((err) => active && setError(err instanceof Error ? err.message : "Erro ao carregar o painel."));
    return () => {
      active = false;
    };
  }, [store.id]);

  const base = `/store/${store.slug}/shopkeeper`;

  return (
    <>
      <PageHeader
        title={store.name}
        subtitle="Resumo da sua loja nos últimos 30 dias."
        actions={
          <Link
            href={`/store/${store.slug}/client`}
            target="_blank"
            rel="noopener noreferrer"
            className={btnSecondary}
          >
            <ExternalLink size={16} aria-hidden="true" />
            Ver vitrine
          </Link>
        }
      />

      {!store.isActive && (
        <div className="mb-6 flex flex-wrap items-center gap-x-3 gap-y-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-body-md">
          <PauseCircle size={20} aria-hidden="true" className="shrink-0 text-amber-600" />
          <span className="flex-1">
            <strong className="text-amber-900">Sua loja está pausada.</strong>{" "}
            <span className="text-amber-800/80">Ela não aparece para visitantes.</span>
          </span>
          <Link
            href={`${base}/configuracoes`}
            className="font-semibold text-primary-container hover:underline"
          >
            Reativar
          </Link>
        </div>
      )}

      {error && <ErrorBox>{error}</ErrorBox>}
      {!data && !error && <LoadingState>Carregando números...</LoadingState>}

      {data && (
        <>
          <div className="mb-6 grid grid-cols-1 gap-4 min-[420px]:grid-cols-2 lg:grid-cols-[repeat(auto-fill,minmax(200px,1fr))]">
            <StatCard
              icon={DollarSign}
              iconBox="bg-emerald-500/10 text-emerald-600"
              label="Faturamento (30d)"
              value={formatPrice(data.revenueLast30Days)}
              hint={`${data.ordersLast30Days} pedido(s), sem cancelados`}
            />
            <StatCard
              href={`${base}/pedidos`}
              icon={ShoppingCart}
              iconBox="bg-amber-500/10 text-amber-600"
              label="Pedidos pendentes"
              value={data.pendingOrders}
              hint="aguardando sua confirmação"
            />
            <StatCard
              href={`${base}/products`}
              icon={Package}
              iconBox="bg-primary-container/10 text-primary-container"
              label="Produtos"
              value={data.totalProducts}
              hint={`${data.activeProducts} ativo(s) na vitrine`}
            />
            <StatCard
              href={`${base}/products`}
              icon={AlertTriangle}
              iconBox="bg-red-500/10 text-red-600"
              label="Sem estoque"
              value={data.outOfStockProducts}
              hint="produto(s) zerados"
            />
            <StatCard
              href={`${base}/categories`}
              icon={Tags}
              iconBox="bg-secondary/10 text-secondary"
              label="Categorias"
              value={data.totalCategories}
            />
          </div>

          <section className={card}>
            <div className="mb-4 flex items-center justify-between gap-3">
              <h2 className={cardTitle}>Pedidos recentes</h2>
              {data.recentOrders.length > 0 && (
                <Link
                  href={`${base}/pedidos`}
                  className="inline-flex items-center gap-1 text-label-md font-semibold text-primary-container hover:underline"
                >
                  Ver todos
                  <ChevronRight size={16} aria-hidden="true" />
                </Link>
              )}
            </div>

            {data.recentOrders.length === 0 ? (
              <div className="flex flex-col items-center gap-2 rounded-xl bg-surface px-4 py-8 text-center">
                <Inbox size={26} aria-hidden="true" className="text-outline" />
                <p className={cardSubtitle}>
                  Nenhum pedido ainda. Divulgue o link da sua vitrine:{" "}
                  <strong className="font-mono text-code-sm text-on-surface">/store/{store.slug}</strong>
                </p>
              </div>
            ) : (
              <div className={`${tableWrapper} -mx-1 rounded-xl`}>
                <table className={table}>
                  <thead>
                    <tr>
                      <th>Código</th>
                      <th>Cliente</th>
                      <th>Itens</th>
                      <th>Total</th>
                      <th>Status</th>
                      <th>Data</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.recentOrders.map((o) => (
                      <tr key={o.id}>
                        <td>
                          <strong className="font-mono text-code-sm text-on-surface">#{o.code}</strong>
                        </td>
                        <td className="max-w-[200px] truncate">{o.customerName}</td>
                        <td>{o.itemCount}</td>
                        <td className="whitespace-nowrap">
                          <span className="font-semibold text-on-surface">{formatPrice(o.total)}</span>
                        </td>
                        <td>
                          <StatusBadge status={o.status}>{ORDER_STATUS_LABELS[o.status]}</StatusBadge>
                        </td>
                        <td className="whitespace-nowrap">
                          <span className="text-outline">{formatDateTime(o.creationDate)}</span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </section>
        </>
      )}
    </>
  );
}

/* ===========================
   CARD DE NÚMERO
   Com href vira link (hover destacado); sem href é só informativo.
=========================== */

function StatCard({
  href,
  icon: Icon,
  iconBox,
  label,
  value,
  hint,
}: {
  href?: string;
  icon: LucideIcon;
  iconBox: string;
  label: string;
  value: ReactNode;
  hint?: string;
}) {
  const content = (
    <>
      <div className="flex items-center justify-between gap-2">
        <span className="text-label-md font-semibold text-on-surface-variant">{label}</span>
        <span className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-lg ${iconBox}`}>
          <Icon size={18} aria-hidden="true" />
        </span>
      </div>
      <span className="text-[26px] leading-tight font-bold tracking-tight text-on-surface tabular-nums">
        {value}
      </span>
      {hint && <span className="text-body-sm text-outline">{hint}</span>}
    </>
  );

  const base =
    "flex flex-col gap-2 rounded-2xl border border-slate-200/85 bg-white p-5 shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)]";

  if (!href) return <div className={base}>{content}</div>;

  return (
    <Link
      href={href}
      className={`${base} transition-all duration-200 hover:-translate-y-px hover:border-primary-container/25 hover:shadow-[0_4px_6px_-1px_rgba(37,99,235,0.04),0_10px_24px_-4px_rgba(15,23,42,0.08)]`}
    >
      {content}
    </Link>
  );
}
"use client";

import { Fragment, useCallback, useEffect, useState } from "react";
import {
  ChevronDown,
  Inbox,
  LoaderCircle,
  Mail,
  MapPin,
  MessageCircle,
  RefreshCw,
  StickyNote,
  XCircle,
} from "lucide-react";
import { unwrap } from "@/lib/api";
import { getOrdersByStore, updateOrderStatus, type Order, type OrderStatus } from "@/lib/api_order";
import { formatDateTime, formatPrice, ORDER_NEXT_STATUS, ORDER_STATUS_LABELS, whatsappLink } from "@/lib/format";
import { formatPhone } from "@/lib/validators";
import { addressLine1, addressLine2 } from "@/lib/address";
import { useShopkeeperStore } from "../../components/ShopkeeperStoreContext";
import {
  EmptyState,
  ErrorBox,
  InlineConfirm,
  LoadingState,
  PageHeader,
  StatusBadge,
  btnDanger,
  btnIcon,
  btnPrimary,
  btnSecondary,
  chip,
  table,
  tableWrapper,
} from "../../components/Ui";

const FILTERS: (OrderStatus | "all")[] = ["all", "Pending", "Confirmed", "Shipped", "Delivered", "Canceled"];

export default function OrdersPage() {
  const { store } = useShopkeeperStore();

  const [orders, setOrders] = useState<Order[]>([]);
  const [filter, setFilter] = useState<OrderStatus | "all">("all");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<number | null>(null);
  const [updatingId, setUpdatingId] = useState<number | null>(null);

  // Substituem confirm()/alert(): confirmação e erro aparecem dentro do pedido aberto.
  const [confirmCancelId, setConfirmCancelId] = useState<number | null>(null);
  const [rowError, setRowError] = useState<{ id: number; message: string } | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setOrders(await unwrap(getOrdersByStore(store.id, filter === "all" ? undefined : filter)));
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao carregar pedidos.");
    } finally {
      setLoading(false);
    }
  }, [store.id, filter]);

  useEffect(() => {
    load();
  }, [load]);

  function toggleExpanded(id: number) {
    setExpanded((cur) => (cur === id ? null : id));
    setConfirmCancelId(null);
    setRowError(null);
  }

  async function changeStatus(order: Order, status: OrderStatus) {
    setUpdatingId(order.id);
    setRowError(null);
    try {
      const updated = await unwrap(updateOrderStatus(order.id, status));
      setOrders((prev) =>
        filter === "all" || updated.status === filter
          ? prev.map((o) => (o.id === updated.id ? updated : o))
          : prev.filter((o) => o.id !== updated.id)
      );
      setConfirmCancelId(null);
    } catch (err) {
      setRowError({
        id: order.id,
        message: err instanceof Error ? err.message : "Erro ao atualizar pedido.",
      });
    } finally {
      setUpdatingId(null);
    }
  }

  return (
    <>
      <PageHeader
        title="Pedidos"
        subtitle="Pedidos feitos pela sua vitrine. O estoque é reservado quando o pedido chega."
        actions={
          <button type="button" onClick={load} disabled={loading} className={btnSecondary}>
            <RefreshCw size={16} aria-hidden="true" className={loading ? "animate-spin" : ""} />
            Atualizar
          </button>
        }
      />

      <div className="-mx-5 mb-5 flex gap-2 overflow-x-auto px-5 pb-1 md:mx-0 md:flex-wrap md:px-0">
        {FILTERS.map((f) => (
          <button
            key={f}
            type="button"
            onClick={() => setFilter(f)}
            aria-pressed={filter === f}
            className={chip(filter === f)}
          >
            {f === "all" ? "Todos" : ORDER_STATUS_LABELS[f]}
          </button>
        ))}
      </div>

      {error && (
        <div className="mb-4">
          <ErrorBox>{error}</ErrorBox>
        </div>
      )}

      {loading ? (
        <LoadingState>Carregando pedidos...</LoadingState>
      ) : orders.length === 0 ? (
        <EmptyState icon={<Inbox size={26} aria-hidden="true" />}>
          Nenhum pedido {filter !== "all" && "com esse status "}por enquanto.
        </EmptyState>
      ) : (
        <div className={tableWrapper}>
          <table className={table}>
            <thead>
              <tr>
                <th>Pedido</th>
                <th>Cliente</th>
                <th>Total</th>
                <th>Status</th>
                <th>Data</th>
                <th>
                  <span className="sr-only">Detalhes</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {orders.map((order) => {
                const isOpen = expanded === order.id;
                const next = ORDER_NEXT_STATUS[order.status];
                const isUpdating = updatingId === order.id;

                return (
                  <Fragment key={order.id}>
                    <tr
                      onClick={() => toggleExpanded(order.id)}
                      className={`cursor-pointer ${isOpen ? "bg-surface" : ""}`}
                    >
                      <td>
                        <strong className="font-mono text-code-sm text-on-surface">#{order.code}</strong>
                      </td>
                      <td>
                        <span className="block max-w-[220px] truncate text-on-surface">{order.customerName}</span>
                        <span className="block text-body-sm text-outline">{formatPhone(order.customerPhone)}</span>
                      </td>
                      <td className="whitespace-nowrap">
                        <span className="font-semibold text-on-surface">{formatPrice(order.total)}</span>
                      </td>
                      <td>
                        <StatusBadge status={order.status}>{ORDER_STATUS_LABELS[order.status]}</StatusBadge>
                      </td>
                      <td className="whitespace-nowrap">
                        <span className="text-outline">{formatDateTime(order.creationDate)}</span>
                      </td>
                      <td className="text-right">
                        <button
                          type="button"
                          onClick={(e) => {
                            e.stopPropagation();
                            toggleExpanded(order.id);
                          }}
                          aria-expanded={isOpen}
                          aria-label={isOpen ? "Fechar detalhes" : "Ver detalhes"}
                          className={btnIcon}
                        >
                          <ChevronDown
                            size={16}
                            aria-hidden="true"
                            className={`transition-transform duration-200 ${isOpen ? "rotate-180" : ""}`}
                          />
                        </button>
                      </td>
                    </tr>

                    {isOpen && (
                      <tr className="hover:bg-transparent!">
                        <td colSpan={6} className="bg-surface">
                          <div className="flex animate-[fadeIn_0.2s_ease] flex-col gap-4 py-1">
                            {/* Itens */}
                            <div className="rounded-xl border border-slate-200 bg-white">
                              {order.items.map((item) => (
                                <div
                                  key={item.id}
                                  className="flex items-center justify-between gap-4 border-b border-slate-100 px-4 py-2.5 last:border-b-0"
                                >
                                  <span className="min-w-0 text-on-surface">
                                    <span className="font-semibold">{item.quantity}×</span> {item.productName}{" "}
                                    {item.color && (
                                      <span className="mr-1 inline-flex rounded-md bg-slate-100 px-1.5 py-0.5 text-body-sm font-semibold text-on-surface">
                                        {item.color}
                                      </span>
                                    )}
                                    {item.size && (
                                      <span className="mr-1 inline-flex rounded-md bg-slate-100 px-1.5 py-0.5 text-body-sm font-semibold text-on-surface">
                                        Tam. {item.size}
                                      </span>
                                    )}
                                    <span className="text-body-sm text-outline">
                                      ({formatPrice(item.unitPrice)} cada)
                                    </span>
                                  </span>
                                  <strong className="shrink-0 text-on-surface">{formatPrice(item.subtotal)}</strong>
                                </div>
                              ))}
                            </div>

                            {/* Endereço de entrega */}
                            {order.shippingAddress && (
                              <div className="flex items-start gap-2 rounded-lg bg-surface-container-low px-3 py-2.5 text-body-md text-on-surface">
                                <MapPin size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-outline" />
                                <div>
                                  <strong className="block">Entregar em</strong>
                                  <span className="block">{addressLine1(order.shippingAddress)}</span>
                                  <span className="block text-on-surface-variant">{addressLine2(order.shippingAddress)}</span>
                                </div>
                              </div>
                            )}

                            {/* Observações e e-mail */}
                            {(order.notes || order.customerEmail) && (
                              <div className="flex flex-col gap-2 text-body-md">
                                {order.notes && (
                                  <p className="flex items-start gap-2 whitespace-pre-line text-on-surface">
                                    <StickyNote size={16} aria-hidden="true" className="mt-0.5 shrink-0 text-outline" />
                                    <span>
                                      <strong>Observações:</strong> {order.notes}
                                    </span>
                                  </p>
                                )}
                                {order.customerEmail && (
                                  <p className="flex items-center gap-2 text-on-surface-variant">
                                    <Mail size={16} aria-hidden="true" className="shrink-0 text-outline" />
                                    <a href={`mailto:${order.customerEmail}`} className="hover:underline">
                                      {order.customerEmail}
                                    </a>
                                  </p>
                                )}
                              </div>
                            )}

                            {rowError?.id === order.id && <ErrorBox>{rowError.message}</ErrorBox>}

                            {/* Ações */}
                            {confirmCancelId === order.id ? (
                              <InlineConfirm
                                message={
                                  <>
                                    Cancelar o pedido <strong>#{order.code}</strong>? Os itens voltam para o estoque.
                                  </>
                                }
                                confirmLabel="Cancelar pedido"
                                busy={isUpdating}
                                onConfirm={() => changeStatus(order, "Canceled")}
                                onCancel={() => setConfirmCancelId(null)}
                              />
                            ) : (
                              <div className="flex flex-wrap gap-2">
                                <a
                                  href={whatsappLink(
                                    order.customerPhone,
                                    `Olá, ${order.customerName.split(" ")[0]}! Sobre seu pedido #${order.code} na ${store.name}...`
                                  )}
                                  target="_blank"
                                  rel="noopener noreferrer"
                                  className={btnSecondary}
                                >
                                  <MessageCircle size={16} aria-hidden="true" />
                                  Falar no WhatsApp
                                </a>

                                {next.map((status) =>
                                  status === "Canceled" ? (
                                    <button
                                      key={status}
                                      type="button"
                                      disabled={isUpdating}
                                      onClick={() => setConfirmCancelId(order.id)}
                                      className={btnDanger}
                                    >
                                      <XCircle size={16} aria-hidden="true" />
                                      Cancelar pedido
                                    </button>
                                  ) : (
                                    <button
                                      key={status}
                                      type="button"
                                      disabled={isUpdating}
                                      onClick={() => changeStatus(order, status)}
                                      className={btnPrimary}
                                    >
                                      {isUpdating && (
                                        <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
                                      )}
                                      Marcar como {ORDER_STATUS_LABELS[status].toLowerCase()}
                                    </button>
                                  )
                                )}
                              </div>
                            )}
                          </div>
                        </td>
                      </tr>
                    )}
                  </Fragment>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </>
  );
}
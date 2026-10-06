"use client";

import { useEffect, useState } from "react";
import { CreditCard, ImageOff, LoaderCircle, MapPin, MessageCircle, Package, X } from "lucide-react";
import { addressLine1, addressLine2 } from "@/lib/address";
import { getMyOrders, type CustomerOrder, type OrderStatus } from "@/lib/api_order";
import { useBackdropDismiss } from "@/lib/backdrop";
import { formatDateTime, formatPrice, formatTime, ORDER_STATUS_LABELS, PAYMENT_STATUS_LABELS, whatsappLink } from "@/lib/format";
import { iconButton, storeOverlay, storeSecondaryButton, useLockBodyScroll } from "./Ui";

interface MyOrdersDrawerProps {
  storeSlug: string;
  storeName: string;
  onClose: () => void;
}

const STATUS_STYLES: Record<OrderStatus, string> = {
  AwaitingPayment: "bg-sky-500/10 text-sky-700",
  Pending: "bg-amber-500/10 text-amber-700",
  Confirmed: "bg-blue-500/10 text-blue-700",
  Shipped: "bg-violet-500/10 text-violet-700",
  Delivered: "bg-emerald-500/10 text-emerald-700",
  Canceled: "bg-slate-200 text-slate-600",
};

// "Meus pedidos" do cliente logado, só desta loja.
export default function MyOrdersDrawer({ storeSlug, storeName, onClose }: MyOrdersDrawerProps) {
  const [orders, setOrders] = useState<CustomerOrder[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  useLockBodyScroll();
  const backdrop = useBackdropDismiss(onClose);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  useEffect(() => {
    let active = true;
    getMyOrders(storeSlug)
      .then((res) => {
        if (!active) return;
        if (!res.status) throw new Error(res.mensagem ?? "Não foi possível carregar seus pedidos.");
        setOrders(res.dados ?? []);
      })
      .catch((err) => active && setError(err instanceof Error ? err.message : "Erro ao carregar pedidos."));
    return () => {
      active = false;
    };
  }, [storeSlug]);

  return (
    <div className={`${storeOverlay} flex justify-end`} {...backdrop}>
      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="my-orders-title"

        className="flex h-full w-full max-w-[440px] animate-drawer-in flex-col bg-white shadow-[-10px_0_30px_rgba(0,0,0,0.12)]"
      >
        <div className="flex items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <h2 id="my-orders-title" className="text-headline-sm text-slate-900">
            Meus pedidos
          </h2>
          <button type="button" onClick={onClose} aria-label="Fechar" className={iconButton}>
            <X size={20} aria-hidden="true" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto px-5 py-4">
          {error ? (
            <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-body-md text-red-700">
              {error}
            </p>
          ) : orders === null ? (
            <div role="status" className="flex items-center justify-center gap-2 py-16 text-body-md text-slate-500">
              <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />
              Carregando...
            </div>
          ) : orders.length === 0 ? (
            <div className="flex flex-col items-center gap-3 py-16 text-center">
              <div className="flex h-16 w-16 items-center justify-center rounded-full bg-slate-100 text-slate-400">
                <Package size={28} aria-hidden="true" />
              </div>
              <p className="max-w-xs text-body-md text-slate-500">
                Você ainda não fez pedidos na {storeName} com esta conta.
              </p>
              <button type="button" onClick={onClose} className={`${storeSecondaryButton} mt-2 w-auto px-5`}>
                Ver produtos
              </button>
            </div>
          ) : (
            <ul className="flex flex-col gap-3">
              {orders.map((order) => (
                <li key={order.id} className="rounded-xl border border-slate-200 p-4">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-mono text-body-md font-bold text-slate-900">#{order.code}</span>
                    <span className={`rounded-full px-2.5 py-1 text-label-sm font-semibold ${STATUS_STYLES[order.status]}`}>
                      {ORDER_STATUS_LABELS[order.status]}
                    </span>
                  </div>
                  <p className="mt-0.5 text-body-sm text-slate-500">
                    {formatDateTime(order.creationDate)}
                    {/* "Aguardando pagamento" já aparece no status do pedido; aqui só os outros casos. */}
                    {order.paymentMethod === "Online" && order.paymentStatus !== "Pending" && (
                      <> · {PAYMENT_STATUS_LABELS[order.paymentStatus]}</>
                    )}
                  </p>

                  {order.paymentCheckoutUrl && (
                    <a
                      href={order.paymentCheckoutUrl}
                      className="mt-3 inline-flex h-9 items-center gap-2 rounded-lg bg-[var(--store-primary)] px-3.5 text-label-md font-semibold text-white hover:brightness-110"
                    >
                      <CreditCard size={16} aria-hidden="true" />
                      Pagar agora
                      {order.paymentDeadline && (
                        <span className="font-normal opacity-80">(até {formatTime(order.paymentDeadline)})</span>
                      )}
                    </a>
                  )}

                  <ul className="mt-3 flex flex-col gap-2.5">
                    {order.items.map((item) => (
                      <li key={item.id} className="flex items-center gap-3">
                        <div className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-slate-100 text-slate-400">
                          {item.imageUrl ? (
                            // eslint-disable-next-line @next/next/no-img-element
                            <img src={item.imageUrl} alt="" className="h-full w-full object-cover" />
                          ) : (
                            <ImageOff size={16} aria-hidden="true" />
                          )}
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="truncate text-body-md text-slate-900">
                            <span className="font-semibold">{item.quantity}×</span> {item.productName}
                          </p>
                          {(item.color || item.size) && (
                            <p className="text-body-sm text-slate-500">
                              {[item.color, item.size && `Tamanho ${item.size}`].filter(Boolean).join(", ")}
                            </p>
                          )}
                        </div>
                        <span className="shrink-0 text-body-md text-slate-700 tabular-nums">
                          {formatPrice(item.unitPrice * item.quantity)}
                        </span>
                      </li>
                    ))}
                  </ul>

                  {order.shippingAddress && (
                    <p className="mt-3 flex items-start gap-2 rounded-lg bg-slate-50 px-3 py-2 text-body-sm text-slate-600">
                      <MapPin size={15} aria-hidden="true" className="mt-0.5 shrink-0 text-slate-400" />
                      <span>
                        {addressLine1(order.shippingAddress)}
                        <br />
                        {addressLine2(order.shippingAddress)}
                      </span>
                    </p>
                  )}

                  <div className="mt-3 flex items-center justify-between gap-3 border-t border-slate-100 pt-3">
                    <span className="text-body-md text-slate-600">
                      Total <strong className="ml-1 text-slate-900 tabular-nums">{formatPrice(order.total)}</strong>
                    </span>
                    {order.storePhone && (
                      <a
                        href={whatsappLink(order.storePhone, `Olá! Quero falar sobre o pedido #${order.code}.`)}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-1.5 text-label-md font-semibold text-[var(--store-primary)] hover:underline"
                      >
                        <MessageCircle size={16} aria-hidden="true" />
                        Falar com a loja
                      </a>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>
      </aside>
    </div>
  );
}
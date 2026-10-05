"use client";

import Link from "next/link";
import { ShoppingBag, X } from "lucide-react";
import type { OrderSummary } from "@/lib/api_order";
import { formatPrice } from "@/lib/format";

// Aviso pequeno no canto da tela quando chega um pedido. Não bloqueia nada e some sozinho.
export default function NewOrderToast({
  order,
  ordersHref,
  onDismiss,
}: {
  order: OrderSummary;
  ordersHref: string;
  onDismiss: () => void;
}) {
  return (
    <div
      role="status"
      aria-live="polite"
      className="fixed right-4 bottom-4 z-50 flex w-[min(320px,calc(100vw-2rem))] animate-[fadeIn_0.3s_ease] items-start gap-3 rounded-xl border border-slate-200/85 bg-white/95 p-3.5 shadow-[0_4px_6px_-1px_rgba(15,23,42,0.05),0_10px_24px_-4px_rgba(15,23,42,0.12)] backdrop-blur-md"
    >
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-[color-mix(in_srgb,var(--sb-accent,#2563eb)_12%,white)] text-[var(--sb-accent-dark,#1d4ed8)]">
        <ShoppingBag size={18} aria-hidden="true" />
      </span>

      <div className="min-w-0 flex-1">
        <p className="text-body-md font-semibold text-on-surface">Novo pedido #{order.code}</p>
        <p className="truncate text-body-sm text-on-surface-variant">
          {order.customerName} · {formatPrice(order.total)}
        </p>
        <Link
          href={ordersHref}
          onClick={onDismiss}
          className="mt-1 inline-block text-label-md font-semibold text-[var(--sb-accent-dark,#1d4ed8)] hover:underline"
        >
          Ver pedidos
        </Link>
      </div>

      <button
        type="button"
        onClick={onDismiss}
        aria-label="Fechar aviso"
        className="-mt-1 -mr-1 rounded-md p-1 text-outline transition-colors hover:bg-slate-100 hover:text-on-surface"
      >
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  );
}

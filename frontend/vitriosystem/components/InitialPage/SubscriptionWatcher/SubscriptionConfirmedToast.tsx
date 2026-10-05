"use client";

import Link from "next/link";
import { CircleCheck, X } from "lucide-react";

// Aviso pequeno no canto da tela quando um pagamento de assinatura é confirmado.
export default function SubscriptionConfirmedToast({ planName, onDismiss }: { planName: string; onDismiss: () => void }) {
  return (
    <div
      role="status"
      aria-live="polite"
      className="fixed right-4 bottom-[calc(86px+env(safe-area-inset-bottom))] z-50 flex w-[min(320px,calc(100vw-2rem))] animate-[fadeIn_0.3s_ease] items-start gap-3 rounded-xl border border-slate-200/85 bg-white/95 p-3.5 shadow-[0_4px_6px_-1px_rgba(15,23,42,0.05),0_10px_24px_-4px_rgba(15,23,42,0.12)] backdrop-blur-md min-[577px]:bottom-4"
    >
      <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-emerald-500/10 text-emerald-700">
        <CircleCheck size={18} aria-hidden="true" />
      </span>

      <div className="min-w-0 flex-1">
        <p className="text-body-md font-semibold text-on-surface">Pagamento confirmado!</p>
        <p className="text-body-sm text-on-surface-variant">Seu plano agora é {planName}.</p>
        <Link
          href="/menu/subscription"
          onClick={onDismiss}
          className="mt-1 inline-block text-label-md font-semibold text-primary-container hover:underline"
        >
          Ver assinatura
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

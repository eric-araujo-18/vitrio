import Link from "next/link";
import type { ReactNode } from "react";
import {
  ChevronRight,
  CircleAlert,
  ExternalLink,
  LoaderCircle,
  Store as StoreIcon,
} from "lucide-react";
import type { Store } from "@/lib/api";

/* ===========================
   BOTÃO PRIMÁRIO
   Usado em <Link> e <button>. O "group/btn" permite
   animar o ícone de seta no hover.
=========================== */

export const primaryButtonClass =
  "group/btn inline-flex h-11 items-center justify-center gap-2 rounded-lg bg-primary-container px-5 text-title-md text-on-primary shadow-[0_1px_2px_rgba(0,0,0,0.05),0_2px_8px_rgba(37,99,235,0.25)] transition-all duration-200 hover:bg-[#1d4ed8] hover:shadow-[0_4px_12px_rgba(37,99,235,0.35)]";

/* ===========================
   PAINEL COM TÍTULO E AÇÃO
=========================== */

type StoresPanelProps = {
  title: string;
  action?: ReactNode;
  children: ReactNode;
};

export function StoresPanel({ title, action, children }: StoresPanelProps) {
  return (
    <section className="rounded-2xl border border-slate-200/85 bg-white/90 p-6 shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)] backdrop-blur-md sm:p-7">
      <div className="mb-6 flex flex-col items-start gap-4 sm:flex-row sm:items-center sm:justify-between">
        <h2 className="text-headline-md text-on-surface">{title}</h2>
        {action}
      </div>

      <div className="flex flex-col gap-4">{children}</div>
    </section>
  );
}

/* ===========================
   CARD DE LOJA
=========================== */

const STORE_STATUS = {
  active: { label: "Ativa", badge: "bg-emerald-500/10 text-emerald-700", dot: "bg-emerald-500" },
  paused: { label: "Pausada", badge: "bg-amber-500/10 text-amber-700", dot: "bg-amber-500" },
  blocked: { label: "Fora do ar: limite do plano", badge: "bg-red-500/10 text-red-700", dot: "bg-red-500" },
};

// "planAction": conteúdo extra embaixo do status (ex.: botão para escolher esta loja
// quando ela está fora do ar pelo limite do plano).
export function StoreCard({ store, planAction }: { store: Store; planAction?: ReactNode }) {
  const status = STORE_STATUS[!store.isActive ? "paused" : store.blockedByPlan ? "blocked" : "active"];

  return (
    <div className="group flex animate-[fadeIn_0.45s_ease] flex-col items-start gap-6 rounded-2xl border border-slate-200/85 bg-white p-5 transition-all duration-200 hover:-translate-y-0.5 hover:border-primary-container/25 hover:shadow-[0_4px_6px_-1px_rgba(37,99,235,0.04),0_10px_24px_-4px_rgba(15,23,42,0.08)] sm:p-6 md:flex-row md:items-center md:justify-between">
      <div className="flex min-w-0 items-center gap-5">
        <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-xl bg-primary-fixed/50 text-primary-container transition-colors duration-200 group-hover:bg-primary-container group-hover:text-on-primary sm:h-16 sm:w-16">
          <StoreIcon size={24} aria-hidden="true" />
        </div>

        <div className="min-w-0">
          <h3 className="truncate text-headline-sm text-on-surface">{store.name}</h3>
          <p className="mt-0.5 truncate font-mono text-code-sm text-on-surface-variant">
            /store/{store.slug}
          </p>
          <span
            className={`mt-2 inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-label-sm ${status.badge}`}
          >
            <span aria-hidden="true" className={`h-1.5 w-1.5 rounded-full ${status.dot}`} />
            {status.label}
          </span>
          {planAction && <div className="mt-3">{planAction}</div>}
        </div>
      </div>

      <div className="flex w-full gap-2 md:w-auto">
        <Link
          href={`/store/${store.slug}/client`}
          target="_blank"
          rel="noopener noreferrer"
          aria-label="Ver vitrine"
          title="Ver vitrine"
          className="inline-flex h-11 w-11 shrink-0 items-center justify-center rounded-lg border border-slate-200 bg-white text-on-surface-variant shadow-[0_1px_2px_rgba(15,23,42,0.04)] transition-colors hover:border-slate-300 hover:bg-surface hover:text-primary"
        >
          <ExternalLink size={18} aria-hidden="true" />
        </Link>

        <Link
          href={`/store/${store.slug}/shopkeeper`}
          className={`${primaryButtonClass} flex-1 md:flex-none`}
        >
          Gerenciar
          <ChevronRight
            size={18}
            aria-hidden="true"
            className="transition-transform duration-200 group-hover/btn:translate-x-1"
          />
        </Link>
      </div>
    </div>
  );
}

/* ===========================
   ESTADOS: CARREGANDO, ERRO, VAZIO
=========================== */

export function StoresLoading() {
  return (
    <p className="flex items-center gap-2 py-6 text-body-md text-on-surface-variant">
      <LoaderCircle size={18} aria-hidden="true" className="animate-spin text-primary-container" />
      Carregando lojas...
    </p>
  );
}

export function StoresError({ message }: { message: string }) {
  return (
    <div
      role="alert"
      className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-body-md text-red-700"
    >
      <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
      <span>{message}</span>
    </div>
  );
}

export function StoresEmpty({ children }: { children: ReactNode }) {
  return (
    <div className="flex flex-col items-center rounded-2xl border border-dashed border-slate-300 bg-surface px-6 py-10 text-center">
      <div className="mb-4 flex h-14 w-14 items-center justify-center rounded-xl bg-primary-fixed/50 text-primary-container">
        <StoreIcon size={24} aria-hidden="true" />
      </div>
      <p className="max-w-sm text-body-md text-on-surface-variant">{children}</p>
    </div>
  );
}
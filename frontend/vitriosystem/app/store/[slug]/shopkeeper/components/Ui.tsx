"use client";

import { useEffect, type ReactNode } from "react";
import { useBackdropDismiss } from "@/lib/backdrop";
import { CircleAlert, CircleCheck, LoaderCircle, TriangleAlert } from "lucide-react";

/*
  Substitui o Shopkeeper.module.css.
  Classes em constantes (para usar em <Link>, <button>, <input>...) e
  alguns componentes pequenos para os padrões que se repetem nas telas.
*/

/* ===========================
   BOTÕES
=========================== */

const buttonBase =
  "inline-flex h-10 items-center justify-center gap-2 whitespace-nowrap rounded-lg px-4 text-label-md font-semibold transition-colors disabled:cursor-not-allowed disabled:opacity-60";

export const btnPrimary = `${buttonBase} bg-primary-container text-on-primary shadow-[0_1px_2px_rgba(0,0,0,0.05),0_2px_8px_rgba(37,99,235,0.25)] hover:bg-[#1d4ed8]`;

export const btnSecondary = `${buttonBase} border border-slate-200 bg-white text-on-surface shadow-[0_1px_2px_rgba(15,23,42,0.04)] hover:border-slate-300 hover:bg-surface`;

export const btnDanger = `${buttonBase} border border-red-200 bg-red-50 text-red-600 hover:bg-red-100`;

export const btnIcon =
  "inline-flex h-8 w-8 items-center justify-center rounded-lg border border-slate-200 bg-white text-on-surface-variant transition-colors hover:border-slate-300 hover:text-on-surface disabled:cursor-not-allowed disabled:opacity-60";

/* ===========================
   CARTÕES
=========================== */

export const card =
  "rounded-2xl border border-slate-200/85 bg-white p-5 shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)] sm:p-6";

export const cardTitle = "text-title-md font-bold text-on-surface";
export const cardSubtitle = "mt-1 text-body-md text-on-surface-variant";

/* ===========================
   FORMULÁRIOS
=========================== */

export const label = "text-label-md font-semibold text-on-surface";
export const hint = "text-body-sm text-outline";

export const input =
  "w-full rounded-lg border border-slate-300 bg-white px-3 py-2.5 text-body-md text-on-surface outline-none transition placeholder:text-slate-400 focus:border-primary-container focus:ring-[3px] focus:ring-primary-container/15 disabled:cursor-not-allowed disabled:bg-slate-50";

/* ===========================
   TABELAS
   Uma classe no <table> estiliza th/td via variantes [&_th].
=========================== */

export const tableWrapper =
  "overflow-x-auto rounded-2xl border border-slate-200/85 bg-white";

export const table =
  "w-full border-collapse text-body-md [&_td]:border-b [&_td]:border-slate-100 [&_td]:px-4 [&_td]:py-3 [&_td]:align-middle [&_td]:text-on-surface-variant [&_th]:border-b [&_th]:border-slate-200 [&_th]:bg-surface [&_th]:px-4 [&_th]:py-3 [&_th]:text-left [&_th]:text-label-sm [&_th]:font-bold [&_th]:whitespace-nowrap [&_th]:text-outline [&_th]:uppercase [&_tr:last-child_td]:border-b-0 [&_tbody_tr]:transition-colors [&_tbody_tr:hover]:bg-primary-container/[0.02]";

/* ===========================
   FILTROS (chips)
=========================== */

export function chip(active: boolean) {
  return `h-8 shrink-0 rounded-full border px-3.5 text-label-md font-semibold transition-colors ${
    active
      ? "border-primary-container bg-primary-container text-on-primary"
      : "border-slate-200 bg-white text-on-surface-variant hover:border-slate-300 hover:text-on-surface"
  }`;
}

/* ===========================
   BADGES DE STATUS
=========================== */

const BADGE_COLORS: Record<string, string> = {
  Pending: "bg-amber-500/10 text-amber-700",
  Confirmed: "bg-primary-container/10 text-primary",
  Shipped: "bg-violet-500/10 text-violet-700",
  Delivered: "bg-emerald-500/10 text-emerald-700",
  Canceled: "bg-slate-500/10 text-slate-600",
  AwaitingPayment: "bg-sky-500/10 text-sky-700",
  Active: "bg-emerald-500/10 text-emerald-700",
  Inactive: "bg-slate-500/10 text-slate-600",
  Blocked: "bg-red-500/10 text-red-700",
};

export function StatusBadge({ status, children }: { status: string; children: ReactNode }) {
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-label-sm whitespace-nowrap ${
        BADGE_COLORS[status] ?? BADGE_COLORS.Inactive
      }`}
    >
      <span aria-hidden="true" className="h-1.5 w-1.5 rounded-full bg-current" />
      {children}
    </span>
  );
}

/* ===========================
   CABEÇALHO DE PÁGINA
=========================== */

export function PageHeader({
  title,
  subtitle,
  actions,
}: {
  title: ReactNode;
  subtitle?: ReactNode;
  actions?: ReactNode;
}) {
  return (
    <div className="mb-7 flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0">
        <h1 className="truncate text-[26px] leading-tight font-bold tracking-tight text-on-surface">
          {title}
        </h1>
        {subtitle && <p className="mt-1 text-body-md text-on-surface-variant">{subtitle}</p>}
      </div>
      {actions && <div className="flex flex-wrap gap-2">{actions}</div>}
    </div>
  );
}

/* ===========================
   ESTADOS
=========================== */

export function LoadingState({ children = "Carregando..." }: { children?: ReactNode }) {
  return (
    <p role="status" className="flex items-center gap-2 py-4 text-body-md text-on-surface-variant">
      <LoaderCircle size={18} aria-hidden="true" className="animate-spin text-primary-container" />
      {children}
    </p>
  );
}

export function ErrorBox({ children }: { children: ReactNode }) {
  return (
    <div
      role="alert"
      className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-body-md text-red-700"
    >
      <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
      <span>{children}</span>
    </div>
  );
}

export function WarningBox({ children }: { children: ReactNode }) {
  return (
    <div
      role="status"
      className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-body-md text-amber-800"
    >
      <TriangleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
      <span>{children}</span>
    </div>
  );
}

export function SuccessBox({ children }: { children: ReactNode }) {
  return (
    <div
      role="status"
      className="flex items-start gap-2 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2.5 text-body-md text-emerald-700"
    >
      <CircleCheck size={18} aria-hidden="true" className="mt-px shrink-0" />
      <span>{children}</span>
    </div>
  );
}

export function EmptyState({ icon, children }: { icon?: ReactNode; children: ReactNode }) {
  return (
    <div className="flex flex-col items-center gap-4 rounded-2xl border border-dashed border-slate-300 bg-white px-4 py-14 text-center text-body-md text-on-surface-variant">
      {icon && (
        <div className="flex h-14 w-14 items-center justify-center rounded-xl bg-primary-fixed/50 text-primary-container">
          {icon}
        </div>
      )}
      {children}
    </div>
  );
}

/* ===========================
   CONFIRMAÇÃO EM LINHA
   Substitui o confirm() do navegador: aparece no próprio lugar da ação.
=========================== */

export function InlineConfirm({
  message,
  confirmLabel,
  onConfirm,
  onCancel,
  busy = false,
}: {
  message: ReactNode;
  confirmLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
  busy?: boolean;
}) {
  return (
    <div
      role="alertdialog"
      className="flex animate-[fadeIn_0.2s_ease] flex-col gap-3 rounded-xl border border-red-200 bg-red-50/70 p-3.5 sm:flex-row sm:items-center sm:justify-between"
    >
      <p className="flex items-start gap-2 text-body-md text-red-900">
        <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0 text-red-600" />
        <span>{message}</span>
      </p>
      <div className="flex shrink-0 gap-2">
        <button type="button" onClick={onCancel} disabled={busy} className={`${btnSecondary} h-9`}>
          Voltar
        </button>
        <button
          type="button"
          onClick={onConfirm}
          disabled={busy}
          autoFocus
          className="inline-flex h-9 items-center justify-center gap-2 rounded-lg bg-red-600 px-4 text-label-md font-semibold whitespace-nowrap text-white transition-colors hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
        >
          {busy && <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />}
          {confirmLabel}
        </button>
      </div>
    </div>
  );
}

/* ===========================
   DIÁLOGO DE CONFIRMAÇÃO
   Para ações destrutivas fora de tabelas (ex: grade de cards).
   Substitui o confirm() do navegador. Fecha com Esc ou clicando fora.
=========================== */

export function ConfirmDialog({
  title,
  message,
  confirmLabel,
  onConfirm,
  onCancel,
  busy = false,
  error,
}: {
  title: string;
  message: ReactNode;
  confirmLabel: string;
  onConfirm: () => void;
  onCancel: () => void;
  busy?: boolean;
  error?: string | null;
}) {
  // Fecha clicando fora, mas não quando o mouse só termina fora.
  const backdrop = useBackdropDismiss(() => !busy && onCancel());

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && !busy && onCancel();
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKey);
    return () => {
      document.body.style.overflow = previous;
      window.removeEventListener("keydown", onKey);
    };
  }, [busy, onCancel]);

  return (
    <div
      className="fixed inset-0 z-[1000] flex animate-overlay-in items-center justify-center bg-slate-900/40 p-4 backdrop-blur-sm"
      {...backdrop}
    >
      <div
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="confirm-dialog-title"
        className="w-full max-w-[420px] animate-modal-in rounded-2xl bg-white p-6 shadow-[0_25px_50px_-12px_rgba(15,23,42,0.25)]"
      >
        <div className="flex items-start gap-3.5">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-red-500/10 text-red-600">
            <TriangleAlert size={20} aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <h2 id="confirm-dialog-title" className="text-headline-sm text-on-surface">
              {title}
            </h2>
            <div className="mt-1 text-body-md text-on-surface-variant">{message}</div>
          </div>
        </div>

        {error && (
          <div className="mt-4">
            <ErrorBox>{error}</ErrorBox>
          </div>
        )}

        <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <button type="button" onClick={onCancel} disabled={busy} className={btnSecondary}>
            Voltar
          </button>
          <button
            type="button"
            onClick={onConfirm}
            disabled={busy}
            autoFocus
            className="inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-red-600 px-4 text-label-md font-semibold whitespace-nowrap text-white transition-colors hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-60"
          >
            {busy && <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />}
            {confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

import { Minus, Plus } from "lucide-react";

/*
  Tudo aqui usa as cores da loja (--store-primary, --store-secondary),
  definidas no elemento raiz da vitrine.
*/

export const storePrimaryButton =
  "inline-flex h-12 w-full items-center justify-center gap-2 rounded-lg bg-[var(--store-primary)] px-4 text-title-md font-bold text-white transition-colors hover:bg-[var(--store-secondary)] disabled:cursor-not-allowed disabled:opacity-60";

export const storeSecondaryButton =
  "inline-flex h-12 w-full items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-title-md text-slate-700 transition-colors hover:border-slate-300 hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60";

export const storeOverlay =
  "fixed inset-0 z-50 animate-overlay-in bg-slate-900/50 backdrop-blur-sm";

export const storeInput =
  "w-full rounded-lg border border-slate-300 bg-white px-3 text-body-lg text-slate-900 outline-none transition placeholder:text-slate-400 focus:border-[var(--store-primary)] focus:ring-[3px] focus:ring-[color-mix(in_srgb,var(--store-primary)_20%,transparent)] disabled:bg-slate-50";

export const iconButton =
  "flex h-9 w-9 items-center justify-center rounded-lg text-slate-500 transition-colors hover:bg-slate-100 hover:text-slate-900";

/** Trava o scroll da página enquanto um modal/gaveta está aberto. */
export function useLockBodyScroll() {
  useEffect(() => {
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previous;
    };
  }, []);
}

/* ===========================
   SELETOR DE QUANTIDADE
=========================== */

interface QuantityStepperProps {
  value: number;
  onDecrease: () => void;
  onIncrease: () => void;
  canDecrease?: boolean;
  canIncrease?: boolean;
  size?: "sm" | "md";
}

export function QuantityStepper({
  value,
  onDecrease,
  onIncrease,
  canDecrease = true,
  canIncrease = true,
  size = "md",
}: QuantityStepperProps) {
  const box = size === "sm" ? "h-8 w-8" : "h-10 w-10";
  const iconSize = size === "sm" ? 14 : 16;
  const buttonClass = `flex ${box} items-center justify-center text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 disabled:cursor-not-allowed disabled:opacity-40 disabled:hover:bg-transparent`;

  return (
    <div className="inline-flex w-fit items-center overflow-hidden rounded-lg border border-slate-200 bg-white">
      <button
        type="button"
        onClick={onDecrease}
        disabled={!canDecrease}
        aria-label="Diminuir quantidade"
        className={buttonClass}
      >
        <Minus size={iconSize} aria-hidden="true" />
      </button>
      <span
        aria-live="polite"
        className={`min-w-9 text-center font-bold text-slate-900 ${size === "sm" ? "text-body-md" : "text-body-lg"}`}
      >
        {value}
      </span>
      <button
        type="button"
        onClick={onIncrease}
        disabled={!canIncrease}
        aria-label="Aumentar quantidade"
        className={buttonClass}
      >
        <Plus size={iconSize} aria-hidden="true" />
      </button>
    </div>
  );
}
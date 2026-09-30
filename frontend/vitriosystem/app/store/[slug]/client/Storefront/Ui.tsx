"use client";

import { useEffect } from "react";
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
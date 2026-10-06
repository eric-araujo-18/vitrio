"use client";

import { useMemo, useSyncExternalStore } from "react";
import type { CartItem } from "./cart";

// Pedido pago online que o cliente foi pagar no Mercado Pago. Fica salvo no navegador (por loja),
// junto com os itens do carrinho, para a vitrine oferecer "pagar agora" ou "cancelar" quando ele
// voltar, mesmo sem conta e mesmo que tenha fechado o checkout sem pagar. Se o pedido for
// cancelado ou vencer, os itens voltam para o carrinho.
export interface PendingPayment {
  code: string;
  items: CartItem[];
}

const storageKey = (slug: string) => `vitrio_pending_payment_${slug}`;
const CHANGE_EVENT = "vitrio:pending-payment";

function read(slug: string): string | null {
  try {
    return localStorage.getItem(storageKey(slug));
  } catch {
    return null; // storage bloqueado (modo anônimo de alguns navegadores)
  }
}

export function savePendingPayment(slug: string, pending: PendingPayment) {
  try {
    localStorage.setItem(storageKey(slug), JSON.stringify(pending));
  } catch {
    // sem storage: a vitrine só não lembra do pedido
  }
  window.dispatchEvent(new Event(CHANGE_EVENT));
}

/** Esquece o pedido salvo. Com `code`, só se for esse pedido (outro pode ter sido salvo depois). */
export function clearPendingPayment(slug: string, code?: string) {
  if (code !== undefined) {
    const current = parse(read(slug));
    if (current?.code !== code) return;
  }
  try {
    localStorage.removeItem(storageKey(slug));
  } catch {
    // nada a fazer
  }
  window.dispatchEvent(new Event(CHANGE_EVENT));
}

function parse(raw: string | null): PendingPayment | null {
  if (!raw) return null;
  try {
    const value = JSON.parse(raw);
    return typeof value?.code === "string" && Array.isArray(value.items) ? (value as PendingPayment) : null;
  } catch {
    return null;
  }
}

function subscribe(onChange: () => void) {
  window.addEventListener(CHANGE_EVENT, onChange);
  window.addEventListener("storage", onChange); // outra aba da mesma loja
  return () => {
    window.removeEventListener(CHANGE_EVENT, onChange);
    window.removeEventListener("storage", onChange);
  };
}

/** Pedido aguardando pagamento salvo para esta loja (null = nenhum). */
export function usePendingPayment(slug: string): PendingPayment | null {
  // O texto cru é o "snapshot" (comparável entre leituras); o objeto sai dele.
  const raw = useSyncExternalStore(
    subscribe,
    () => read(slug),
    () => null
  );
  return useMemo(() => parse(raw), [raw]);
}

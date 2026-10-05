"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import { getPendingOrdersSummary, type OrderSummary } from "@/lib/api_order";

// De quanto em quanto tempo o painel confere se chegou pedido.
const CHECK_INTERVAL_MS = 30_000;
// Quanto tempo o aviso de pedido novo fica na tela.
const TOAST_DURATION_MS = 10_000;

/*
  Alerta de pedido novo no painel do lojista (sem e-mail, sem som).
  Confere os pedidos pendentes a cada 30s e quando a aba volta ao foco. Se aparecer um
  pedido mais novo do que o último visto, mostra um aviso discreto por alguns segundos.
  Na primeira consulta só guarda a referência: pedidos que já existiam não viram aviso.
*/
export function useOrderAlerts(storeId: number | null) {
  const [summary, setSummary] = useState<{ storeId: number; pendingCount: number } | null>(null);
  const [newOrder, setNewOrder] = useState<OrderSummary | null>(null);
  const [checkCount, setCheckCount] = useState(0);

  // Último pedido visto, por loja (trocar de loja recomeça do zero).
  const lastSeen = useRef<{ storeId: number; orderId: number } | null>(null);

  useEffect(() => {
    if (!storeId) return;
    let active = true;

    const check = () =>
      getPendingOrdersSummary(storeId)
        .then(({ dados }) => {
          if (!active || !dados) return;

          const latestId = dados.latest?.id ?? 0;
          const seen = lastSeen.current?.storeId === storeId ? lastSeen.current.orderId : null;
          if (seen !== null && latestId > seen && dados.latest) setNewOrder(dados.latest);
          lastSeen.current = { storeId, orderId: Math.max(seen ?? 0, latestId) };

          setSummary({ storeId, pendingCount: dados.pendingCount });
        })
        .catch(() => {
          // sem rede ou sessão expirando: tenta de novo na próxima rodada
        });

    check();
    const timer = setInterval(check, CHECK_INTERVAL_MS);
    window.addEventListener("focus", check);
    return () => {
      active = false;
      clearInterval(timer);
      window.removeEventListener("focus", check);
    };
  }, [storeId, checkCount]);

  // O aviso some sozinho depois de alguns segundos.
  useEffect(() => {
    if (!newOrder) return;
    const timer = setTimeout(() => setNewOrder(null), TOAST_DURATION_MS);
    return () => clearTimeout(timer);
  }, [newOrder]);

  // Confere na hora (ex.: depois de mudar o status de um pedido).
  const refresh = useCallback(() => setCheckCount((n) => n + 1), []);
  const dismiss = useCallback(() => setNewOrder(null), []);

  return {
    pendingCount: summary?.storeId === storeId ? summary.pendingCount : 0,
    newOrder,
    dismiss,
    refresh,
  };
}

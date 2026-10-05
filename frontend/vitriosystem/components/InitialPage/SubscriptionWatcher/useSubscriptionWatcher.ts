"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import {
  SUBSCRIPTION_CHANGED_EVENT,
  checkSubscription,
  forgetCheckoutPlan,
  readCheckoutPlan,
} from "@/lib/api_subscription";

const POLL_MS = 15_000;
const MAX_POLLS = 80; // ~20 minutos com a aba visível

/*
  Para as telas fora da página de assinatura. Se houver pagamento de assinatura pendente,
  confere ao abrir a tela, ao voltar para a aba e a cada 15s (só com a aba visível). Quando o
  pagamento é confirmado, devolve o nome do plano novo (para um aviso discreto) e dispara
  SUBSCRIPTION_CHANGED_EVENT para as telas recarregarem o que depende do plano.
  O backend também confere sozinho a cada minuto; isto só deixa a tela em dia mais rápido.
*/
export function useSubscriptionWatcher(enabled: boolean) {
  const [confirmedPlan, setConfirmedPlan] = useState<string | null>(null);
  const [hasPending, setHasPending] = useState(false);
  const pendingRef = useRef<string | null>(null);
  const lastCheck = useRef(0);

  const check = useCallback(() => {
    lastCheck.current = Date.now();
    return checkSubscription()
      .then(({ dados }) => {
        if (!dados) return;
        // Plano que estava sendo pago: o pendente visto antes ou o lembrado ao ir para o checkout
        // (cobre a aba nova aberta depois de pagar, quando o pendente já pode ter sido resolvido).
        const before = pendingRef.current ?? readCheckoutPlan();
        pendingRef.current = dados.pendingPlanCode;
        setHasPending(dados.pendingPlanCode !== null);
        if (dados.pendingPlanCode || !before) return;

        // O pagamento deixou de estar pendente: foi pago (o plano virou esse) ou foi desistido.
        forgetCheckoutPlan();
        if (dados.planCode === before) {
          setConfirmedPlan(dados.planName);
          window.dispatchEvent(new Event(SUBSCRIPTION_CHANGED_EVENT));
        }
      })
      .catch(() => {
        // sem rede: tenta de novo na próxima
      });
  }, []);

  // Ao abrir a tela e ao voltar para a aba.
  useEffect(() => {
    if (!enabled) return;
    check();
    const onVisible = () => {
      if (document.visibilityState === "visible" && Date.now() - lastCheck.current > 5_000) check();
    };
    document.addEventListener("visibilitychange", onVisible);
    window.addEventListener("focus", onVisible);
    return () => {
      document.removeEventListener("visibilitychange", onVisible);
      window.removeEventListener("focus", onVisible);
    };
  }, [enabled, check]);

  // Enquanto houver pagamento pendente.
  useEffect(() => {
    if (!enabled || !hasPending) return;
    let polls = 0;
    const timer = setInterval(() => {
      polls += 1;
      if (polls > MAX_POLLS) clearInterval(timer);
      else if (document.visibilityState === "visible") check();
    }, POLL_MS);
    return () => clearInterval(timer);
  }, [enabled, hasPending, check]);

  // O aviso some sozinho.
  useEffect(() => {
    if (!confirmedPlan) return;
    const timer = setTimeout(() => setConfirmedPlan(null), 12_000);
    return () => clearTimeout(timer);
  }, [confirmedPlan]);

  const dismiss = useCallback(() => setConfirmedPlan(null), []);
  return { confirmedPlan, dismiss };
}

// Chama "reload" quando um pagamento de assinatura é confirmado (ex.: recarregar as lojas,
// que podem ter voltado ao ar com o plano novo). "reload" deve ser estável (useCallback).
export function useOnSubscriptionChanged(reload: () => void) {
  useEffect(() => {
    window.addEventListener(SUBSCRIPTION_CHANGED_EVENT, reload);
    return () => window.removeEventListener(SUBSCRIPTION_CHANGED_EVENT, reload);
  }, [reload]);
}

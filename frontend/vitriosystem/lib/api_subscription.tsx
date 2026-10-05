// ===== Assinatura do lojista =====

import { request } from "./api";

export type SubscriptionStatus = "Trial" | "Active" | "PastDue" | "Suspended" | "Canceled";

export interface Plan {
  id: number;
  code: string;
  name: string;
  description: string | null;
  priceMonthly: number;
  maxStores: number;
  /** null = ilimitado */
  maxProductsPerStore: number | null;
  allowsOnlinePayment: boolean;
}

export interface StoreUsage {
  storeId: number;
  storeName: string;
  productCount: number;
}

/** O que o lojista pode fazer com cada plano (decidido pelo backend) */
export type PlanAction = "Current" | "Checkout" | "ScheduleDowngrade" | "KeepCurrent" | "CancelToFree" | "Unavailable";

export interface PlanOption {
  plan: Plan;
  action: PlanAction;
  /** Texto do botão ou da etiqueta */
  label: string;
}

export interface MySubscription {
  /** Plano que vale agora (o grátis, se a assinatura estiver suspensa/cancelada) */
  plan: Plan;
  /** null = nunca assinou */
  status: SubscriptionStatus | null;
  currentPeriodEnd: string | null;
  canceledAt: string | null;
  pastDueSince: string | null;
  /** Pagamento aberto no Mercado Pago, ainda não confirmado */
  pendingPlan: Plan | null;
  /** Plano menor que entra no fim do período atual */
  scheduledPlan: Plan | null;
  /** Tem assinatura paga que pode ser cancelada */
  canCancel: boolean;
  /** Um item por plano, já com a ação permitida */
  options: PlanOption[];
  storeCount: number;
  stores: StoreUsage[];
}

export const SUBSCRIPTION_STATUS_LABELS: Record<SubscriptionStatus, string> = {
  Trial: "Em teste",
  Active: "Ativa",
  PastDue: "Pagamento pendente",
  Suspended: "Suspensa",
  Canceled: "Cancelada",
};

export function getPlans() {
  return request<Plan[]>("/api/Subscription/plans", "GET");
}

export function getMySubscription() {
  return request<MySubscription>("/api/Subscription/me", "GET", undefined, true);
}

export interface CheckoutResult {
  /** Link do Mercado Pago. null = a troca foi feita direto na assinatura atual. */
  checkoutUrl: string | null;
}

export function startCheckout(planCode: string) {
  return request<CheckoutResult>("/api/Subscription/checkout", "POST", { planCode }, true);
}

export function cancelSubscription() {
  return request<MySubscription>("/api/Subscription/cancel", "POST", undefined, true);
}

/**
 * Confere a assinatura no Mercado Pago e devolve o estado atualizado.
 * force = false: o backend pula se já conferiu há menos de 1 minuto (usado ao abrir a página).
 * force = true: botão "Já paguei, verificar agora".
 */
export function syncSubscription(force = false) {
  return request<MySubscription>(`/api/Subscription/sync?force=${force}`, "POST", undefined, true);
}
// ===== Verificação leve (outras telas do painel) =====

export interface SubscriptionCheck {
  planCode: string;
  planName: string;
  /** Plano do checkout ainda não confirmado (null = nenhum) */
  pendingPlanCode: string | null;
}

/** Se houver checkout pendente, o backend confere no Mercado Pago antes de responder. */
export function checkSubscription() {
  return request<SubscriptionCheck>("/api/Subscription/check", "GET", undefined, true);
}

/** Disparado no window quando um pagamento de assinatura é confirmado (telas recarregam o que depende do plano). */
export const SUBSCRIPTION_CHANGED_EVENT = "vitrio:subscription-changed";

// ===== Checkout em andamento =====
// O plano escolhido ao sair para o Mercado Pago fica lembrado neste navegador (todas as abas)
// por 2 horas. Serve para saber, depois, se o pagamento pendente que sumiu foi pago (o plano
// virou esse) ou se o checkout foi cancelado/expirou.

const CHECKOUT_PLAN_KEY = "vitrio_checkout_plan";
const CHECKOUT_MEMORY_MS = 2 * 60 * 60 * 1000;

export function rememberCheckoutPlan(planCode: string) {
  try {
    localStorage.setItem(CHECKOUT_PLAN_KEY, JSON.stringify({ planCode, at: Date.now() }));
  } catch {
    // storage bloqueado: só não dá para mostrar o aviso de confirmação depois
  }
}

export function readCheckoutPlan(): string | null {
  try {
    const raw = localStorage.getItem(CHECKOUT_PLAN_KEY);
    if (!raw) return null;
    const saved = JSON.parse(raw) as { planCode?: unknown; at?: unknown };
    if (typeof saved.planCode !== "string" || typeof saved.at !== "number" || Date.now() - saved.at > CHECKOUT_MEMORY_MS) {
      localStorage.removeItem(CHECKOUT_PLAN_KEY);
      return null;
    }
    return saved.planCode;
  } catch {
    return null;
  }
}

export function forgetCheckoutPlan() {
  try {
    localStorage.removeItem(CHECKOUT_PLAN_KEY);
  } catch {
    // nada a fazer
  }
}

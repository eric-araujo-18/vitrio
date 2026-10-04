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

export interface MySubscription {
  /** Plano que vale agora (o grátis, se a assinatura estiver suspensa/cancelada) */
  plan: Plan;
  /** null = nunca assinou */
  status: SubscriptionStatus | null;
  trialEndsAt: string | null;
  currentPeriodEnd: string | null;
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
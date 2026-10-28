// ===== Painel do Admin =====

import { request } from "./api";
import type { MySubscription, SubscriptionStatus } from "./api_subscription";

export interface AdminOverview {
  shopkeepers: number;
  unconfirmedShopkeepers: number;
  clients: number;
  activeStores: number;
  /** Pedidos dos últimos 30 dias que chegaram às lojas (sem cancelados e sem os que esperam pagamento). */
  ordersLast30Days: number;
  paidSubscriptions: { planCode: string; planName: string; count: number }[];
  monthlyRecurringRevenue: number;
}

export interface AdminUser {
  id: number;
  name: string;
  email: string;
  phone: string | null;
  role: string;
  emailConfirmed: boolean;
  creationDate: string;
  /** Plano que vale agora */
  planCode: string;
  planName: string;
  subscriptionStatus: SubscriptionStatus | null;
  currentPeriodEnd: string | null;
  /** Plano definido pelo Admin, sem cobrança */
  manualPlan: boolean;
  stores: { id: number; name: string; slug: string; isActive: boolean }[];
}

/** "Shopkeeper" (padrão), "Client", "Admin" ou "all" */
export type AdminRoleFilter = "Shopkeeper" | "Client" | "Admin" | "all";

export function getAdminOverview() {
  return request<AdminOverview>("/api/Admin/overview", "GET", undefined, true);
}

/** Até 50 contas, das mais novas para as mais antigas. Busca por nome, e-mail, CPF ou id. */
export function searchAdminUsers(search: string, role: AdminRoleFilter) {
  const params = new URLSearchParams({ role });
  if (search.trim()) params.set("search", search.trim());
  return request<AdminUser[]>(`/api/Admin/users?${params}`, "GET", undefined, true);
}

export function adminConfirmEmail(userId: number) {
  return request<AdminUser>(`/api/Admin/users/${userId}/confirm-email`, "POST", undefined, true);
}

/** Troca manual de plano: sem cobrança até o Admin mudar de novo (cancela a cobrança no Mercado Pago). */
export function adminSetPlan(userId: number, planCode: string) {
  return request<MySubscription>(`/api/Subscription/admin/users/${userId}/plan`, "PUT", { planCode }, true);
}

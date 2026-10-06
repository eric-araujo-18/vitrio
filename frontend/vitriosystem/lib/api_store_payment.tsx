// ===== Pagamento online da loja (conta do Mercado Pago conectada por OAuth) =====

import { request } from "./api";

export interface StorePaymentStatus {
  /** O Vitrio tem as credenciais do app do Mercado Pago configuradas. */
  configured: boolean;
  /** O plano do lojista inclui pagamento online. */
  planAllows: boolean;
  connected: boolean;
  /** A vitrine oferece "pagar agora" (configurado, plano permite e conta conectada). */
  available: boolean;
  mercadoPagoUserId: string | null;
  /** false = conta de teste (sandbox) */
  liveMode: boolean;
  connectedAt: string | null;
  /** Pedidos esperando pagamento: enquanto houver, a conta não pode ser desconectada. */
  awaitingPaymentOrders: number;
}

export function getStorePaymentStatus(storeId: number) {
  return request<StorePaymentStatus>(`/api/StorePayment/${storeId}`, "GET", undefined, true);
}

/** Devolve o link do Mercado Pago onde o lojista autoriza o Vitrio. */
export function connectStorePayment(storeId: number) {
  return request<{ authorizationUrl: string }>(`/api/StorePayment/${storeId}/connect`, "POST", undefined, true);
}

export function disconnectStorePayment(storeId: number) {
  return request<StorePaymentStatus>(`/api/StorePayment/${storeId}`, "DELETE", undefined, true);
}

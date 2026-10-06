// ===== Order (painel do lojista) =====

import { request } from "./api";
import type { ShippingAddress } from "./address";

/** AwaitingPayment: pagamento online ainda não aprovado (o pedido vira Pending quando for pago). */
export type OrderStatus = "AwaitingPayment" | "Pending" | "Confirmed" | "Shipped" | "Delivered" | "Canceled";

/** Arrange: combinar com a loja. Online: Mercado Pago, direto na conta da loja. */
export type OrderPaymentMethod = "Arrange" | "Online";
export type OrderPaymentStatus = "None" | "Pending" | "Approved" | "Refunded" | "Canceled";

export interface OrderItem {
  id: number;
  productId: number | null;
  productName: string;
  size: string | null;
  color: string | null;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  subtotal: number;
}

export interface Order {
  id: number;
  storeId: number;
  code: string;
  customerName: string;
  customerPhone: string;
  customerEmail: string | null;
  notes: string | null;
  status: OrderStatus;
  total: number;
  paymentMethod: OrderPaymentMethod;
  paymentStatus: OrderPaymentStatus;
  paidAt: string | null;
  creationDate: string;
  updatedDate: string | null;
  /** Nulo em pedidos antigos, de antes do endereço existir */
  shippingAddress: ShippingAddress | null;
  items: OrderItem[];
}

export interface OrderSummary {
  id: number;
  code: string;
  customerName: string;
  status: OrderStatus;
  paymentStatus: OrderPaymentStatus;
  total: number;
  itemCount: number;
  creationDate: string;
}

export interface StoreDashboard {
  totalProducts: number;
  activeProducts: number;
  outOfStockProducts: number;
  totalCategories: number;
  pendingOrders: number;
  ordersLast30Days: number;
  revenueLast30Days: number;
  recentOrders: OrderSummary[];
}

export function getOrdersByStore(storeId: number, status?: OrderStatus) {
  const query = status ? `?status=${status}` : "";
  return request<Order[]>(`/api/Order/store/${storeId}${query}`, "GET", undefined, true);
}

export function getOrderById(id: number) {
  return request<Order>(`/api/Order/${id}`, "GET", undefined, true);
}

export function updateOrderStatus(id: number, status: OrderStatus) {
  return request<Order>(`/api/Order/${id}/status`, "PUT", { status }, true);
}

// Alerta de pedido novo no painel (consultado de tempos em tempos).
export interface PendingOrdersSummary {
  pendingCount: number;
  /** Pedido pendente mais recente; o id serve para saber se chegou um novo. */
  latest: OrderSummary | null;
}

export function getPendingOrdersSummary(storeId: number) {
  return request<PendingOrdersSummary>(`/api/Order/store/${storeId}/pending-summary`, "GET", undefined, true);
}

export function getStoreDashboard(storeId: number) {
  return request<StoreDashboard>(`/api/Store/${storeId}/dashboard`, "GET", undefined, true);
}

// ===== Área do cliente da vitrine =====

export interface CustomerOrder {
  id: number;
  code: string;
  status: OrderStatus;
  total: number;
  paymentMethod: OrderPaymentMethod;
  paymentStatus: OrderPaymentStatus;
  /** Link para pagar, enquanto o pedido espera o pagamento e o prazo não acabou. */
  paymentCheckoutUrl: string | null;
  paymentDeadline: string | null;
  creationDate: string;
  storeName: string;
  storeSlug: string;
  storePhone: string | null;
  shippingAddress: ShippingAddress | null;
  items: OrderItem[];
}

/** Pedidos do cliente logado. Com storeSlug, só os daquela loja. */
export function getMyOrders(storeSlug?: string) {
  const query = storeSlug ? `?storeSlug=${encodeURIComponent(storeSlug)}` : "";
  return request<CustomerOrder[]>(`/api/Customer/orders${query}`, "GET", undefined, true);
}
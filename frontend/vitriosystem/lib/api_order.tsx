// ===== Order (painel do lojista) =====

import { request } from "./api";
import type { ShippingAddress } from "./address";

export type OrderStatus = "Pending" | "Confirmed" | "Shipped" | "Delivered" | "Canceled";

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
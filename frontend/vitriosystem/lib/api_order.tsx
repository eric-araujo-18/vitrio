// ===== Order (painel do lojista) =====

import { request } from "./api";

export type OrderStatus = "Pending" | "Confirmed" | "Shipped" | "Delivered" | "Canceled";

export interface OrderItem {
  id: number;
  productId: number | null;
  productName: string;
  size: string | null;
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

export function getStoreDashboard(storeId: number) {
  return request<StoreDashboard>(`/api/Store/${storeId}/dashboard`, "GET", undefined, true);
}
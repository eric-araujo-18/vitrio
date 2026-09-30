// ===== Vitrine pública (sem login) =====

import { request } from "./api";
import type { ProductCategory, ProductImage } from "./api_product";

export interface PublicStore {
  name: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  phone: string | null;
  primaryColor: string;
  secondaryColor: string;
  tertiaryColor: string;
}

export interface PublicCategory {
  id: number;
  name: string;
  slug: string;
  parentCategoryId: number | null;
}

export interface PublicProduct {
  id: number;
  name: string;
  slug: string;
  description: string | null;
  price: number;
  promotionalPrice: number | null;
  stockQuantity: number;
  isFeatured: boolean;
  category: ProductCategory | null;
  images: ProductImage[];
}

export interface CreateOrderPayload {
  customerName: string;
  customerPhone: string;
  customerEmail?: string;
  notes?: string;
  items: { productId: number; quantity: number }[];
}

export interface OrderCreated {
  code: string;
  total: number;
  storePhone: string | null;
}

const base = (slug: string) => `/api/Public/stores/${encodeURIComponent(slug)}`;

export function getPublicStore(slug: string) {
  return request<PublicStore>(base(slug), "GET");
}

export function getPublicCategories(slug: string) {
  return request<PublicCategory[]>(`${base(slug)}/categories`, "GET");
}

export function getPublicProducts(slug: string, filters?: { category?: string; search?: string }) {
  const params = new URLSearchParams();
  if (filters?.category) params.set("category", filters.category);
  if (filters?.search) params.set("search", filters.search);
  const query = params.toString();
  return request<PublicProduct[]>(`${base(slug)}/products${query ? `?${query}` : ""}`, "GET");
}

export function getPublicProduct(slug: string, productSlug: string) {
  return request<PublicProduct>(`${base(slug)}/products/${encodeURIComponent(productSlug)}`, "GET");
}

export function createPublicOrder(slug: string, payload: CreateOrderPayload) {
  return request<OrderCreated>(`${base(slug)}/orders`, "POST", payload);
}

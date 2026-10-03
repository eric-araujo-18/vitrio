// ===== Product =====

import { request } from "./api";

export interface ProductImage {
  id: number;
  url: string;
  order: number;
}

// Tamanho do produto, com estoque próprio
export interface ProductVariant {
  id: number;
  size: string;
  stockQuantity: number;
}

export interface ProductCategory {
  id: number;
  name: string;
  slug: string | null;
}

export interface Product {
  id: number;
  storeId: number;
  categoryId: number | null;
  category: ProductCategory | null;
  name: string;
  slug: string;
  description: string | null;
  sku: string | null;
  price: number;
  promotionalPrice: number | null;
  stockQuantity: number;
  isActive: boolean;
  isFeatured: boolean;
  creationDate: string;
  updatedDate: string | null;
  images: ProductImage[];
  variants: ProductVariant[];
}

export interface ProductImagePayload {
  url: string;
  order: number;
}

export interface ProductVariantPayload {
  size: string;
  stockQuantity: number;
}

export interface CreateProductPayload {
  storeId: number;
  categoryId?: number;
  name: string;
  slug?: string;
  description?: string;
  sku?: string;
  price: number;
  promotionalPrice?: number;
  stockQuantity?: number;
  isActive?: boolean;
  isFeatured?: boolean;
  images?: ProductImagePayload[];
  /** Vazio = produto sem tamanho. Com itens, o estoque vira a soma dos tamanhos. */
  variants?: ProductVariantPayload[];
}

// Edição é "completa": o formulário manda todos os campos de novo.
export type UpdateProductPayload = Omit<CreateProductPayload, "storeId">;

export function getProductsByStore(storeId: number) {
  return request<Product[]>(`/api/Product/store/${storeId}`, "GET", undefined, true);
}

export function getProductById(id: number) {
  return request<Product>(`/api/Product/${id}`, "GET", undefined, true);
}

export function createProduct(payload: CreateProductPayload) {
  return request<Product>("/api/Product", "POST", payload, true);
}

export function updateProduct(id: number, payload: UpdateProductPayload) {
  return request<Product>(`/api/Product/${id}`, "PUT", payload, true);
}

export function deleteProduct(id: number) {
  return request<string>(`/api/Product/${id}`, "DELETE", undefined, true);
}
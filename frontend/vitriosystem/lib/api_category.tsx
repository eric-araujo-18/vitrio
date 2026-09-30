// ===== Category =====

import { request } from "./api";

export interface Category {
  id: number;
  storeId: number;
  name: string;
  slug: string;
  parentCategoryId: number | null;
  isActive: boolean;
  productCount: number;
}

export interface CreateCategoryPayload {
  storeId: number;
  name: string;
  slug?: string;
  parentCategoryId?: number;
}

export interface UpdateCategoryPayload {
  name?: string;
  slug?: string;
  parentCategoryId?: number;
  removeParent?: boolean;
  isActive?: boolean;
}

export function getCategoriesByStore(storeId: number) {
  return request<Category[]>(`/api/Category/store/${storeId}`, "GET", undefined, true);
}

export function createCategory(payload: CreateCategoryPayload) {
  return request<Category>("/api/Category", "POST", payload, true);
}

export function updateCategory(id: number, payload: UpdateCategoryPayload) {
  return request<Category>(`/api/Category/${id}`, "PUT", payload, true);
}

export function deleteCategory(id: number) {
  return request<string>(`/api/Category/${id}`, "DELETE", undefined, true);
}

// ===== Área do cliente da vitrine: endereços salvos =====

import { request } from "./api";
import type { SavedAddress } from "./address";

export interface AddressPayload {
  label?: string;
  cep: string;
  state: string;
  city: string;
  neighborhood?: string;
  street: string;
  number: string;
  complement?: string;
  isDefault?: boolean;
}

export function getMyAddresses() {
  return request<SavedAddress[]>("/api/Customer/addresses", "GET", undefined, true);
}

export function createAddress(payload: AddressPayload) {
  return request<SavedAddress>("/api/Customer/addresses", "POST", payload, true);
}

export function updateAddress(id: number, payload: AddressPayload) {
  return request<SavedAddress>(`/api/Customer/addresses/${id}`, "PUT", payload, true);
}

export function deleteAddress(id: number) {
  return request<string>(`/api/Customer/addresses/${id}`, "DELETE", undefined, true);
}
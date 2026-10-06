// ===== Vitrine pública (sem login) =====

import { request } from "./api";
import type { AddressPayload } from "./api_customer";
import type { OrderPaymentMethod, OrderPaymentStatus, OrderStatus } from "./api_order";
import type { ProductCategory, ProductImage, ProductVariant } from "./api_product";

export interface PublicStore {
  name: string;
  slug: string;
  description: string | null;
  logoUrl: string | null;
  phone: string | null;
  /** A loja aceita "pagar agora" pelo Mercado Pago no checkout. */
  onlinePayment: boolean;
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
  /** Produtos com o mesmo colorGroupId são a mesma peça em outras cores. */
  colorName: string | null;
  colorHex: string | null;
  colorGroupId: string | null;
  category: ProductCategory | null;
  images: ProductImage[];
  /** Tamanhos (vazio = produto sem tamanho) */
  variants: ProductVariant[];
}

export interface CreateOrderPayload {
  customerName: string;
  customerPhone: string;
  customerEmail?: string;
  notes?: string;
  items: { productId: number; variantId?: number; quantity: number }[];
  /** Endereço salvo na conta (cliente logado) */
  addressId?: number;
  /** Endereço digitado no checkout */
  shippingAddress?: AddressPayload;
  /** "Online" só se a loja oferece (PublicStore.onlinePayment). Padrão: combinar com a loja. */
  paymentMethod?: OrderPaymentMethod;
}

export interface OrderCreated {
  code: string;
  total: number;
  storePhone: string | null;
  paymentMethod: OrderPaymentMethod;
  /** Pagamento online: link do checkout do Mercado Pago. */
  checkoutUrl: string | null;
  paymentDeadline: string | null;
}

/** Situação do pagamento de um pedido, para a vitrine. */
export interface OrderPaymentInfo {
  code: string;
  status: OrderStatus;
  paymentMethod: OrderPaymentMethod;
  paymentStatus: OrderPaymentStatus;
  total: number;
  paymentDeadline: string | null;
  /** Link para pagar, enquanto o pedido espera o pagamento e o prazo não acabou. */
  checkoutUrl: string | null;
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

/** Confere o pagamento online do pedido (a API consulta o Mercado Pago se ainda estiver esperando). */
export function getOrderPayment(slug: string, code: string) {
  return request<OrderPaymentInfo>(`${base(slug)}/orders/${encodeURIComponent(code)}/payment`, "GET");
}

export function createPublicOrder(slug: string, payload: CreateOrderPayload) {
  // auth = true: se o cliente estiver logado, o token vai junto e o pedido fica
  // ligado à conta dele. Sem login, o pedido é feito normalmente.
  return request<OrderCreated>(`${base(slug)}/orders`, "POST", payload, true);
}
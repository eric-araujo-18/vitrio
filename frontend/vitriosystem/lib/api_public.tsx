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

/** Produtos por página na vitrine (PublicService.ProductsPageSize no backend). */
export const PRODUCTS_PAGE_SIZE = 200;

/** Uma página de produtos (a partir de 1), na ordem da vitrine: destaques e depois os mais novos. */
export function getPublicProducts(slug: string, filters?: { category?: string; search?: string; page?: number }) {
  const params = new URLSearchParams();
  if (filters?.category) params.set("category", filters.category);
  if (filters?.search) params.set("search", filters.search);
  if (filters?.page && filters.page > 1) params.set("page", String(filters.page));
  const query = params.toString();
  return request<PublicProduct[]>(`${base(slug)}/products${query ? `?${query}` : ""}`, "GET");
}

/**
 * Continua a lista a partir da primeira página: busca as próximas enquanto vierem cheias.
 * onPage recebe a lista acumulada a cada página. Lança se uma página falhar.
 */
export async function getRemainingPublicProducts(
  slug: string,
  firstPage: PublicProduct[],
  onPage?: (soFar: PublicProduct[]) => void
): Promise<PublicProduct[]> {
  let all = firstPage;
  for (let page = 2, last = firstPage.length; last === PRODUCTS_PAGE_SIZE; page++) {
    const res = await getPublicProducts(slug, { page });
    if (!res.status || !res.dados) throw new Error(res.mensagem ?? "Erro ao carregar os produtos.");
    // Um produto criado entre duas páginas empurra a lista: o id evita repetir.
    const seen = new Set(all.map((p) => p.id));
    all = [...all, ...res.dados.filter((p) => !seen.has(p.id))];
    last = res.dados.length;
    onPage?.(all);
  }
  return all;
}

/** Todos os produtos visíveis da loja, página por página. */
export async function getAllPublicProducts(slug: string): Promise<PublicProduct[]> {
  const first = await getPublicProducts(slug);
  if (!first.status || !first.dados) throw new Error(first.mensagem ?? "Erro ao carregar os produtos.");
  return getRemainingPublicProducts(slug, first.dados);
}

export function getPublicProduct(slug: string, productSlug: string) {
  return request<PublicProduct>(`${base(slug)}/products/${encodeURIComponent(productSlug)}`, "GET");
}

/** Confere o pagamento online do pedido (a API consulta o Mercado Pago se ainda estiver esperando). */
export function getOrderPayment(slug: string, code: string) {
  return request<OrderPaymentInfo>(`${base(slug)}/orders/${encodeURIComponent(code)}/payment`, "GET");
}

/**
 * O cliente desiste de um pedido que ainda espera pagamento (o estoque volta na hora). Devolve a
 * situação final: cancelado, ou pago, se o pagamento chegou antes.
 */
export function cancelUnpaidOrder(slug: string, code: string) {
  return request<OrderPaymentInfo>(`${base(slug)}/orders/${encodeURIComponent(code)}/cancel`, "POST");
}

export function createPublicOrder(slug: string, payload: CreateOrderPayload) {
  // auth = true: se o cliente estiver logado, o token vai junto e o pedido fica
  // ligado à conta dele. Sem login, o pedido é feito normalmente.
  return request<OrderCreated>(`${base(slug)}/orders`, "POST", payload, true);
}
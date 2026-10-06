import type { OrderPaymentStatus, OrderStatus } from "./api_order";

export function formatPrice(value: number) {
  return value.toLocaleString("pt-BR", { style: "currency", currency: "BRL" });
}

export function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString("pt-BR", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

// Só a hora: "14:35"
export function formatTime(iso: string) {
  return new Date(iso).toLocaleTimeString("pt-BR", { hour: "2-digit", minute: "2-digit" });
}

// "5588999998888" -> link do WhatsApp (adiciona 55 se vier só DDD + número)
export function whatsappLink(phone: string, text?: string) {
  const digits = phone.replace(/\D/g, "");
  const full = digits.length <= 11 ? `55${digits}` : digits;
  const query = text ? `?text=${encodeURIComponent(text)}` : "";
  return `https://wa.me/${full}${query}`;
}

// Preço que o cliente paga de fato (promocional, se houver).
export function effectivePrice(p: { price: number; promotionalPrice: number | null }) {
  return p.promotionalPrice != null && p.promotionalPrice < p.price ? p.promotionalPrice : p.price;
}

export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  AwaitingPayment: "Aguardando pagamento",
  Pending: "Pendente",
  Confirmed: "Confirmado",
  Shipped: "Enviado",
  Delivered: "Entregue",
  Canceled: "Cancelado",
};

// Próximos passos possíveis a partir de cada status (espelha a regra do backend).
export const ORDER_NEXT_STATUS: Record<OrderStatus, OrderStatus[]> = {
  // Muda sozinho quando o pagamento é aprovado; o lojista só pode cancelar.
  AwaitingPayment: ["Canceled"],
  Pending: ["Confirmed", "Canceled"],
  Confirmed: ["Shipped", "Delivered", "Canceled"],
  Shipped: ["Delivered", "Canceled"],
  Delivered: [],
  Canceled: [],
};

// Situação do pagamento online (pedidos "combinar com a loja" ficam com "None" e não mostram nada).
export const PAYMENT_STATUS_LABELS: Record<OrderPaymentStatus, string> = {
  None: "",
  Pending: "Aguardando pagamento",
  Approved: "Pago online",
  Refunded: "Pagamento estornado",
  Canceled: "Pagamento não concluído",
};

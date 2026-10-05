"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import type { PublicProduct } from "./api_public";
import type { ProductVariant } from "./api_product";
import { effectivePrice } from "./format";

export interface CartItem {
  /** Identifica a linha do carrinho: produto + tamanho. Ver cartItemKey(). */
  key: string;
  productId: number;
  variantId: number | null;
  size: string | null;
  color: string | null;
  name: string;
  imageUrl: string | null;
  unitPrice: number;
  quantity: number;
  maxQuantity: number; // estoque conhecido no momento em que foi adicionado
}

interface CartContextValue {
  items: CartItem[];
  totalItems: number;
  totalPrice: number;
  /** Para produto com tamanhos, informe a variação escolhida. */
  addItem: (product: PublicProduct, quantity?: number, variant?: ProductVariant | null) => void;
  setQuantity: (key: string, quantity: number) => void;
  removeItem: (key: string) => void;
  clear: () => void;
}

const CartContext = createContext<CartContextValue | undefined>(undefined);

/** A mesma camisa em M e em G são duas linhas diferentes no carrinho. */
export function cartItemKey(productId: number, variantId?: number | null) {
  return `${productId}:${variantId ?? ""}`;
}

// Carrinhos salvos antes dos tamanhos existirem não têm key/variantId/size.
function normalize(raw: unknown): CartItem[] {
  if (!Array.isArray(raw)) return [];
  return raw.map((i) => {
    const variantId = i.variantId ?? null;
    return {
      ...i,
      variantId,
      size: i.size ?? null,
      color: i.color ?? null,
      key: cartItemKey(i.productId, variantId),
    } as CartItem;
  });
}

// Carrinho separado por loja e salvo no navegador do visitante
// (não é dado sensível: só IDs e quantidades). O preço aqui é só
// pra exibição — o backend recalcula tudo no checkout.
export function CartProvider({ storeSlug, children }: { storeSlug: string; children: ReactNode }) {
  const storageKey = `vitrio_cart_${storeSlug}`;
  const [items, setItems] = useState<CartItem[]>([]);
  const [hydrated, setHydrated] = useState(false);

  // Lê do localStorage só no cliente, depois da hidratação (ler na renderização faria o HTML
  // do servidor, sempre vazio, divergir do cliente). Sincronizar com um sistema externo na
  // montagem é justamente o caso de uso de um efeito, por isso a regra é desligada aqui.
  useEffect(() => {
    let stored: CartItem[] = [];
    try {
      const raw = localStorage.getItem(storageKey);
      stored = raw ? normalize(JSON.parse(raw)) : [];
    } catch {
      // storage inacessível ou JSON inválido: começa vazio
    }
    // eslint-disable-next-line react-hooks/set-state-in-effect -- ver comentário acima
    setItems(stored);
    setHydrated(true);
  }, [storageKey]);

  useEffect(() => {
    if (!hydrated) return;
    try {
      localStorage.setItem(storageKey, JSON.stringify(items));
    } catch {
      // modo anônimo / storage cheio: o carrinho só não persiste
    }
  }, [items, hydrated, storageKey]);

  const addItem = useCallback(
    (product: PublicProduct, quantity = 1, variant: ProductVariant | null = null) => {
      const key = cartItemKey(product.id, variant?.id);
      const max = variant ? variant.stockQuantity : product.stockQuantity;

      setItems((prev) => {
        const existing = prev.find((i) => i.key === key);

        if (existing) {
          return prev.map((i) =>
            i.key === key ? { ...i, maxQuantity: max, quantity: Math.min(i.quantity + quantity, max) } : i
          );
        }

        return [
          ...prev,
          {
            key,
            productId: product.id,
            variantId: variant?.id ?? null,
            size: variant?.size ?? null,
            color: product.colorName ?? null,
            name: product.name,
            imageUrl: product.images[0]?.url ?? null,
            unitPrice: effectivePrice(product),
            quantity: Math.min(quantity, max),
            maxQuantity: max,
          },
        ];
      });
    },
    []
  );

  const setQuantity = useCallback((key: string, quantity: number) => {
    setItems((prev) =>
      prev
        .map((i) => (i.key === key ? { ...i, quantity: Math.max(0, Math.min(quantity, i.maxQuantity)) } : i))
        .filter((i) => i.quantity > 0)
    );
  }, []);

  const removeItem = useCallback((key: string) => {
    setItems((prev) => prev.filter((i) => i.key !== key));
  }, []);

  const clear = useCallback(() => setItems([]), []);

  const value = useMemo<CartContextValue>(
    () => ({
      items,
      totalItems: items.reduce((sum, i) => sum + i.quantity, 0),
      totalPrice: items.reduce((sum, i) => sum + i.quantity * i.unitPrice, 0),
      addItem,
      setQuantity,
      removeItem,
      clear,
    }),
    [items, addItem, setQuantity, removeItem, clear]
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart() {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error("useCart precisa ser usado dentro de <CartProvider>");
  return ctx;
}
"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import type { PublicProduct } from "./api_public";
import type { ProductVariant } from "./api_product";
import { effectivePrice, formatPrice } from "./format";

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
  /** Devolve itens ao carrinho (ex.: pedido cancelado sem pagar), somando com os que já estão lá. */
  restoreItems: (items: CartItem[]) => void;
  /**
   * Acerta o carrinho com o catálogo atual da loja (lista completa): tira o que saiu da vitrine ou
   * esgotou, limita a quantidade ao estoque e atualiza os preços. O que mudou vai para `notice`.
   */
  syncWithCatalog: (products: PublicProduct[]) => void;
  /** O que mudou no último acerto com o catálogo (vazio = nada a avisar). */
  notice: string[];
  dismissNotice: () => void;
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

// Compara o carrinho com o catálogo atual. Pura: devolve o carrinho acertado e o que mudou.
function reconcile(items: CartItem[], catalog: PublicProduct[]): { items: CartItem[]; changes: string[] } {
  const byId = new Map(catalog.map((p) => [p.id, p]));
  const next: CartItem[] = [];
  const changes: string[] = [];

  for (const item of items) {
    const product = byId.get(item.productId);
    const variant = item.variantId == null ? null : (product?.variants.find((v) => v.id === item.variantId) ?? null);
    const label = item.size ? `${item.name} (tamanho ${item.size})` : item.name;

    // Saiu da vitrine, o tamanho foi removido, ou o produto passou a ter tamanhos.
    if (!product || (item.variantId != null && !variant) || (item.variantId == null && product.variants.length > 0)) {
      changes.push(`${label} não está mais disponível e saiu do carrinho.`);
      continue;
    }

    const stock = variant ? variant.stockQuantity : product.stockQuantity;
    if (stock <= 0) {
      changes.push(`${label} esgotou e saiu do carrinho.`);
      continue;
    }

    const quantity = Math.min(item.quantity, stock);
    if (quantity < item.quantity) changes.push(`${label}: só restam ${stock}, a quantidade foi ajustada.`);

    const unitPrice = effectivePrice(product);
    if (unitPrice !== item.unitPrice) changes.push(`${label}: o preço agora é ${formatPrice(unitPrice)}.`);

    next.push({
      ...item,
      name: product.name,
      color: product.colorName ?? null,
      imageUrl: product.images[0]?.url ?? null,
      unitPrice,
      quantity,
      maxQuantity: stock,
    });
  }

  return { items: next, changes };
}

// Carrinho separado por loja e salvo no navegador do visitante
// (não é dado sensível: só IDs e quantidades). O preço aqui é só
// pra exibição — o backend recalcula tudo no checkout.
export function CartProvider({ storeSlug, children }: { storeSlug: string; children: ReactNode }) {
  const storageKey = `vitrio_cart_${storeSlug}`;
  const [items, setItems] = useState<CartItem[]>([]);
  const [hydrated, setHydrated] = useState(false);
  const [notice, setNotice] = useState<string[]>([]);

  // O acerto com o catálogo usa o carrinho atual, e o catálogo pode chegar antes de o carrinho ser
  // lido do navegador: os dois ficam guardados aqui até poderem ser comparados.
  const itemsRef = useRef<CartItem[]>([]);
  const catalogRef = useRef<PublicProduct[] | null>(null);
  const hydratedRef = useRef(false);
  useEffect(() => {
    itemsRef.current = items;
  }, [items]);

  // Lê do localStorage só no cliente, depois da hidratação (ler na renderização faria o HTML
  // do servidor, sempre vazio, divergir do cliente). Sincronizar com um sistema externo na
  // montagem é justamente o caso de uso de um efeito.
  useEffect(() => {
    let stored: CartItem[] = [];
    try {
      const raw = localStorage.getItem(storageKey);
      stored = raw ? normalize(JSON.parse(raw)) : [];
    } catch {
      // storage inacessível ou JSON inválido: começa vazio
    }
    // O catálogo chegou antes: acerta o carrinho salvo agora.
    if (catalogRef.current) {
      const result = reconcile(stored, catalogRef.current);
      stored = result.items;
      if (result.changes.length > 0) setNotice(result.changes);
    }
    itemsRef.current = stored;
    hydratedRef.current = true;
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

  const clear = useCallback(() => {
    setItems([]);
    setNotice([]);
  }, []);

  const restoreItems = useCallback((restored: CartItem[]) => {
    setItems((prev) => {
      const next = [...prev];
      for (const item of restored) {
        const at = next.findIndex((i) => i.key === item.key);
        if (at === -1) next.push(item);
        else next[at] = { ...next[at], quantity: next[at].quantity + item.quantity };
      }
      return next;
    });
  }, []);

  const syncWithCatalog = useCallback((products: PublicProduct[]) => {
    catalogRef.current = products;
    if (!hydratedRef.current) return; // a leitura do navegador faz o acerto (efeito acima)
    const result = reconcile(itemsRef.current, products);
    itemsRef.current = result.items;
    setItems(result.items);
    if (result.changes.length > 0) setNotice(result.changes);
  }, []);

  const dismissNotice = useCallback(() => setNotice([]), []);

  const value = useMemo<CartContextValue>(
    () => ({
      items,
      totalItems: items.reduce((sum, i) => sum + i.quantity, 0),
      totalPrice: items.reduce((sum, i) => sum + i.quantity * i.unitPrice, 0),
      addItem,
      setQuantity,
      removeItem,
      clear,
      restoreItems,
      syncWithCatalog,
      notice,
      dismissNotice,
    }),
    [items, addItem, setQuantity, removeItem, clear, restoreItems, syncWithCatalog, notice, dismissNotice]
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}

export function useCart() {
  const ctx = useContext(CartContext);
  if (!ctx) throw new Error("useCart precisa ser usado dentro de <CartProvider>");
  return ctx;
}
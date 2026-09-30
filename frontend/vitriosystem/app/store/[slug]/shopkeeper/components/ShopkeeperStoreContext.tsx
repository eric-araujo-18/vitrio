"use client";

import { createContext, useContext, type ReactNode } from "react";
import type { Store } from "@/lib/api";

interface ShopkeeperStoreValue {
  store: Store;
  // Atualiza a loja no contexto depois de editar (personalização/configurações),
  // pra sidebar e demais telas refletirem sem recarregar.
  setStore: (store: Store) => void;
}

const ShopkeeperStoreContext = createContext<ShopkeeperStoreValue | undefined>(undefined);

export function ShopkeeperStoreProvider({
  value,
  children,
}: {
  value: ShopkeeperStoreValue;
  children: ReactNode;
}) {
  return <ShopkeeperStoreContext.Provider value={value}>{children}</ShopkeeperStoreContext.Provider>;
}

// Disponível em qualquer página dentro de /store/[slug]/shopkeeper.
// O layout só renderiza as páginas depois que a loja foi carregada,
// então aqui "store" nunca é null.
export function useShopkeeperStore() {
  const ctx = useContext(ShopkeeperStoreContext);
  if (!ctx) throw new Error("useShopkeeperStore precisa estar dentro do layout do lojista");
  return ctx;
}

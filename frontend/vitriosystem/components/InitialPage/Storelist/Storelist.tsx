"use client";

import { useEffect, useState } from "react";
import { Plus } from "lucide-react";
import { getMyStores, type Store } from "@/lib/api";
import CreateStoreModal from "./CreateStoreModal";
import {
  StoreCard,
  StoresEmpty,
  StoresError,
  StoresLoading,
  StoresPanel,
  primaryButtonClass,
} from "./StoreCard";

export default function Storelist() {
  const [stores, setStores] = useState<Store[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [showCreateModal, setShowCreateModal] = useState(false);

  useEffect(() => {
    let cancelled = false;

    getMyStores()
      .then(({ dados }) => {
        if (!cancelled) setStores(dados ?? []);
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : "Erro ao carregar lojas.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  function handleStoreCreated(store: Store) {
    // Insere a nova loja no topo da lista sem precisar refazer o fetch.
    setStores((prev) => [store, ...prev]);
    setShowCreateModal(false);
  }

  return (
    <>
      <StoresPanel
        title="Minhas lojas"
        action={
          <button
            type="button"
            onClick={() => setShowCreateModal(true)}
            className={`${primaryButtonClass} w-full sm:w-auto`}
          >
            <Plus size={18} aria-hidden="true" />
            Nova loja
          </button>
        }
      >
        {loading && <StoresLoading />}

        {!loading && error && <StoresError message={error} />}

        {!loading && !error && stores.length === 0 && (
          <StoresEmpty>
            Você ainda não tem nenhuma loja. Clique em &quot;Nova loja&quot; para criar a primeira.
          </StoresEmpty>
        )}

        {!loading && !error && stores.map((store) => <StoreCard key={store.id} store={store} />)}
      </StoresPanel>

      {showCreateModal && (
        <CreateStoreModal
          onClose={() => setShowCreateModal(false)}
          onCreated={handleStoreCreated}
        />
      )}
    </>
  );
}
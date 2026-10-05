"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { LoaderCircle, Plus, Power, TriangleAlert } from "lucide-react";
import { getMyStores, goOnlineStore, unwrap, type Store } from "@/lib/api";
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

  // Escolha de qual loja fica no ar quando o plano permite menos lojas do que o lojista tem
  const [confirmingId, setConfirmingId] = useState<number | null>(null);
  const [switchingId, setSwitchingId] = useState<number | null>(null);

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

  async function handleGoOnline(id: number) {
    setSwitchingId(id);
    setError(null);
    try {
      setStores(await unwrap(goOnlineStore(id)));
      setConfirmingId(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao colocar a loja no ar.");
    } finally {
      setSwitchingId(null);
    }
  }

  const hasBlocked = stores.some((s) => s.blockedByPlan);

  // Lojas hoje no ar que sairiam do ar para "target" entrar. Mesma regra do backend
  // (StoreService.GoOnlineAsync): das outras lojas ativas ficam as mais antigas, até completar
  // o limite do plano junto com "target"; as demais são pausadas.
  function onlineStoresPausedFor(target: Store) {
    // Só dá para trocar com o limite cheio, então as lojas no ar são exatamente o limite do plano.
    const maxStores = stores.filter((s) => s.isActive && !s.blockedByPlan).length;
    const others = stores
      .filter((s) => s.id !== target.id && s.isActive)
      .sort((a, b) => new Date(a.creationDate).getTime() - new Date(b.creationDate).getTime() || a.id - b.id);
    return others
      .slice(Math.max(maxStores - 1, 0))
      .filter((s) => !s.blockedByPlan)
      .map((s) => s.name);
  }

  function planAction(store: Store) {
    // Fora do ar pelo plano, ou pausada quando o limite de lojas no ar já está cheio.
    const canSwitch = store.blockedByPlan || (!store.isActive && store.storeLimitReached);
    if (!canSwitch) return undefined;

    if (confirmingId !== store.id) {
      return (
        <button
          type="button"
          onClick={() => setConfirmingId(store.id)}
          className="inline-flex items-center gap-1.5 text-label-md font-semibold text-primary-container hover:underline"
        >
          <Power size={15} aria-hidden="true" />
          Deixar esta loja no ar
        </button>
      );
    }

    const busy = switchingId === store.id;
    const pausedNames = onlineStoresPausedFor(store);
    return (
      <div className="flex flex-col gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3 text-body-sm text-amber-900">
        <p>
          {pausedNames.length > 0 ? (
            <>
              <strong>{pausedNames.join(", ")}</strong> {pausedNames.length === 1 ? "será pausada" : "serão pausadas"}{" "}
              para esta loja entrar no ar.
            </>
          ) : (
            "Esta loja vai entrar no ar."
          )}{" "}
          Dá para trocar de novo quando quiser.
        </p>
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => handleGoOnline(store.id)}
            disabled={busy}
            className="inline-flex h-8 items-center gap-1.5 rounded-md bg-primary-container px-3 text-label-md font-semibold text-on-primary disabled:opacity-60"
          >
            {busy && <LoaderCircle size={14} aria-hidden="true" className="animate-spin" />}
            Confirmar
          </button>
          <button
            type="button"
            onClick={() => setConfirmingId(null)}
            disabled={busy}
            className="inline-flex h-8 items-center rounded-md border border-slate-200 bg-white px-3 text-label-md text-on-surface"
          >
            Cancelar
          </button>
        </div>
      </div>
    );
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

        {!loading && hasBlocked && (
          <div
            role="status"
            className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-body-md text-amber-800"
          >
            <TriangleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
            <span>
              Seu plano permite menos lojas no ar do que você tem. Escolha qual fica no ar em &quot;Deixar esta loja
              no ar&quot;, ou{" "}
              <Link href="/menu/subscription" className="font-semibold underline">
                veja os planos
              </Link>
              . Nenhum dado é apagado.
            </span>
          </div>
        )}

        {!loading && stores.length === 0 && !error && (
          <StoresEmpty>
            Você ainda não tem nenhuma loja. Clique em &quot;Nova loja&quot; para criar a primeira.
          </StoresEmpty>
        )}

        {!loading &&
          stores.map((store) => <StoreCard key={store.id} store={store} planAction={planAction(store)} />)}
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

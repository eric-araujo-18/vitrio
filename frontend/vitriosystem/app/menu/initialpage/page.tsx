"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { ChevronRight, Plus } from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import { useOnSubscriptionChanged } from "@/components/InitialPage/SubscriptionWatcher/useSubscriptionWatcher";
import { getMyStores, unwrap, type Store } from "@/lib/api";
import {
  StoreCard,
  StoresEmpty,
  StoresLoading,
  StoresPanel,
  primaryButtonClass,
} from "@/components/InitialPage/Storelist/StoreCard";

export default function InitialPage() {
  return (
    <DashboardShell>
      {(user) => (
        <>
          <div className="mb-9">
            <h1 className="mb-2 text-display-lg-mobile text-on-surface md:text-display-lg">
              Bem-vindo, {user.name.split(" ")[0]} 👋
            </h1>
            <p className="text-body-lg text-on-surface-variant">
              Gerencie sua conta e todas as suas lojas em um único lugar.
            </p>
          </div>

          <StoresOverview />
        </>
      )}
    </DashboardShell>
  );
}

function StoresOverview() {
  const [stores, setStores] = useState<Store[] | null>(null);

  useEffect(() => {
    unwrap(getMyStores())
      .then(setStores)
      .catch(() => setStores([]));
  }, []);

  // Pagamento de assinatura confirmado: lojas fora do ar pelo plano podem ter voltado.
  const reloadStores = useCallback(() => {
    unwrap(getMyStores())
      .then(setStores)
      .catch(() => {});
  }, []);
  useOnSubscriptionChanged(reloadStores);

  const isEmpty = stores !== null && stores.length === 0;

  return (
    <StoresPanel
      title={stores === null ? "Suas lojas" : `Suas lojas (${stores.length})`}
      action={
        stores !== null && (
          <Link href="/menu/stores" className={`${primaryButtonClass} w-full sm:w-auto`}>
            {isEmpty ? (
              <>
                <Plus size={18} aria-hidden="true" />
                Criar loja
              </>
            ) : (
              <>
                Ver todas
                <ChevronRight
                  size={18}
                  aria-hidden="true"
                  className="transition-transform duration-200 group-hover/btn:translate-x-1"
                />
              </>
            )}
          </Link>
        )
      }
    >
      {stores === null && <StoresLoading />}

      {isEmpty && (
        <StoresEmpty>Você ainda não tem lojas. Crie a primeira para montar sua vitrine.</StoresEmpty>
      )}

      {stores?.slice(0, 3).map((store) => <StoreCard key={store.id} store={store} />)}
    </StoresPanel>
  );
}
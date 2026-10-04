"use client";

import { useEffect, useState, type ReactNode } from "react";
import { Check, CircleAlert, CreditCard, LoaderCircle, Minus } from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import { unwrap } from "@/lib/api";
import {
  SUBSCRIPTION_STATUS_LABELS,
  getMySubscription,
  getPlans,
  type MySubscription,
  type Plan,
} from "@/lib/api_subscription";
import { formatPrice } from "@/lib/format";

/*
  Etapa 1 da assinatura: mostra o plano atual, o uso (lojas e produtos) e os planos
  disponíveis. A cobrança pelo Mercado Pago entra na etapa 2 — até lá o botão de
  assinar fica desativado e a troca de plano é feita manualmente.
*/

const card =
  "rounded-2xl border border-slate-200/85 bg-white p-5 shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)] sm:p-6";

export default function SubscriptionPage() {
  return (
    <DashboardShell title="Assinatura" subtitle="Seu plano, o que você está usando e os planos disponíveis.">
      {() => <SubscriptionContent />}
    </DashboardShell>
  );
}

function SubscriptionContent() {
  const [mine, setMine] = useState<MySubscription | null>(null);
  const [plans, setPlans] = useState<Plan[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    Promise.all([unwrap(getMySubscription()), unwrap(getPlans())])
      .then(([m, p]) => {
        if (!active) return;
        setMine(m);
        setPlans(p);
      })
      .catch((err) => active && setError(err instanceof Error ? err.message : "Erro ao carregar a assinatura."));
    return () => {
      active = false;
    };
  }, []);

  if (error) {
    return (
      <p role="alert" className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-body-md text-red-700">
        <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
        {error}
      </p>
    );
  }

  if (!mine) {
    return (
      <div role="status" className="flex items-center gap-2 text-body-md text-on-surface-variant">
        <LoaderCircle size={18} aria-hidden="true" className="animate-spin text-primary-container" />
        Carregando...
      </div>
    );
  }

  const { plan } = mine;

  return (
    <div className="flex flex-col gap-8">
      {/* Plano atual + uso */}
      <section className={card} aria-labelledby="current-plan">
        <div className="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p className="text-body-sm text-on-surface-variant">Seu plano</p>
            <h2 id="current-plan" className="text-headline-sm text-on-surface">
              {plan.name}
            </h2>
            <p className="mt-1 text-body-md text-on-surface-variant">
              {plan.priceMonthly > 0 ? `${formatPrice(plan.priceMonthly)} por mês` : "Sem custo"}
              {mine.status && `, assinatura ${SUBSCRIPTION_STATUS_LABELS[mine.status].toLowerCase()}`}
            </p>
          </div>
          <CreditCard size={28} aria-hidden="true" className="text-primary-container" />
        </div>

        <div className="mt-6 grid gap-5 sm:grid-cols-2">
          <UsageBar label="Lojas" used={mine.storeCount} limit={plan.maxStores} />
          {mine.stores.map((s) => (
            <UsageBar
              key={s.storeId}
              label={`Produtos em ${s.storeName}`}
              used={s.productCount}
              limit={plan.maxProductsPerStore}
            />
          ))}
        </div>

        {mine.storeCount > plan.maxStores && (
          <p className="mt-5 rounded-lg bg-amber-500/10 px-3 py-2.5 text-body-sm text-amber-800">
            Você tem mais lojas do que o plano atual permite. Elas continuam funcionando, mas não dá para criar
            novas até voltar ao limite.
          </p>
        )}
      </section>

      {/* Planos */}
      <section aria-labelledby="plans-title">
        <h2 id="plans-title" className="mb-4 text-title-md font-bold text-on-surface">
          Planos
        </h2>
        <div className="grid gap-4 md:grid-cols-3">
          {plans.map((p) => {
            const isCurrent = p.id === plan.id;
            return (
              <article
                key={p.id}
                className={`${card} flex flex-col ${isCurrent ? "border-primary-container ring-1 ring-primary-container" : ""}`}
              >
                <h3 className="text-title-md font-bold text-on-surface">{p.name}</h3>
                {p.description && <p className="mt-1 text-body-sm text-on-surface-variant">{p.description}</p>}
                <p className="mt-4 text-on-surface">
                  <span className="text-headline-sm font-extrabold tabular-nums">
                    {p.priceMonthly > 0 ? formatPrice(p.priceMonthly) : "Grátis"}
                  </span>
                  {p.priceMonthly > 0 && <span className="text-body-sm text-on-surface-variant"> /mês</span>}
                </p>

                <ul className="mt-5 flex flex-1 flex-col gap-2.5 text-body-md text-on-surface">
                  <Feature ok>
                    {p.maxStores} {p.maxStores === 1 ? "loja" : "lojas"}
                  </Feature>
                  <Feature ok>
                    {p.maxProductsPerStore === null
                      ? "Produtos ilimitados"
                      : `Até ${p.maxProductsPerStore} produtos por loja`}
                  </Feature>
                  <Feature ok={p.allowsOnlinePayment}>Pagamento online (Pix e cartão)</Feature>
                </ul>

                {isCurrent ? (
                  <span className="mt-6 inline-flex h-10 items-center justify-center rounded-lg bg-surface-container-low text-label-md font-semibold text-on-surface-variant">
                    Plano atual
                  </span>
                ) : (
                  <button
                    type="button"
                    disabled
                    title="O pagamento pelo Mercado Pago chega na próxima etapa"
                    className="mt-6 inline-flex h-10 items-center justify-center rounded-lg bg-primary-container text-label-md font-semibold text-on-primary disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Em breve
                  </button>
                )}
              </article>
            );
          })}
        </div>
        <p className="mt-4 text-body-sm text-on-surface-variant">
          A assinatura pelo Mercado Pago está chegando. Até lá, fale com o suporte para trocar de plano.
        </p>
      </section>
    </div>
  );
}

function UsageBar({ label, used, limit }: { label: string; used: number; limit: number | null }) {
  const percent = limit ? Math.min(100, Math.round((used / limit) * 100)) : 0;
  const tone = limit && used >= limit ? "bg-red-500" : percent >= 80 ? "bg-amber-500" : "bg-primary-container";

  return (
    <div>
      <div className="mb-1.5 flex items-baseline justify-between gap-3 text-body-sm">
        <span className="truncate text-on-surface">{label}</span>
        <span className="shrink-0 text-on-surface-variant tabular-nums">
          {used} {limit === null ? "(ilimitado)" : `de ${limit}`}
        </span>
      </div>
      {limit !== null && (
        <div
          role="progressbar"
          aria-label={label}
          aria-valuenow={used}
          aria-valuemin={0}
          aria-valuemax={limit}
          className="h-2 overflow-hidden rounded-full bg-slate-100"
        >
          <div className={`h-full rounded-full ${tone}`} style={{ width: `${percent}%` }} />
        </div>
      )}
    </div>
  );
}

function Feature({ ok, children }: { ok: boolean; children: ReactNode }) {
  return (
    <li className={`flex items-start gap-2 ${ok ? "" : "text-on-surface-variant"}`}>
      {ok ? (
        <Check size={18} aria-hidden="true" className="mt-0.5 shrink-0 text-emerald-600" />
      ) : (
        <Minus size={18} aria-hidden="true" className="mt-0.5 shrink-0 text-slate-400" />
      )}
      <span>{children}</span>
    </li>
  );
}
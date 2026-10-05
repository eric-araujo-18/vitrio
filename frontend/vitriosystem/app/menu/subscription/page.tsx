"use client";

import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { Check, CircleAlert, CircleCheck, CreditCard, ExternalLink, LoaderCircle, Minus, TriangleAlert } from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import { unwrap } from "@/lib/api";
import {
  SUBSCRIPTION_STATUS_LABELS,
  cancelSubscription,
  startCheckout,
  syncSubscription,
  getMySubscription,
  type MySubscription,
  type PlanOption,
} from "@/lib/api_subscription";
import { formatPrice } from "@/lib/format";

/*
  Página de assinatura. Ela NÃO decide regra nenhuma: o backend devolve, para cada
  plano, qual ação é permitida (options[].action) e o texto do botão. Aqui só
  desenhamos isso. Assim o front nunca oferece uma troca que o backend recusaria.
*/

const GRACE_DAYS = 7; // mesmo valor do backend (SubscriptionService.GraceDays)

// Conferência do pagamento pendente: rápida logo depois de voltar do checkout, lenta depois.
const FAST_POLL_MS = 5_000;
const FAST_POLL_COUNT = 12; // 1 minuto
const SLOW_POLL_MS = 30_000;
const SLOW_POLL_COUNT = 30; // 15 minutos

// Plano escolhido ao sair para o checkout (mesma aba). Na volta, diz qual plano estava sendo
// pago mesmo se o Mercado Pago já tiver confirmado antes de a página carregar.
const CHECKOUT_PLAN_KEY = "vitrio_checkout_plan";

function rememberCheckout(planCode: string) {
  try {
    sessionStorage.setItem(CHECKOUT_PLAN_KEY, planCode);
  } catch {
    // storage bloqueado: na volta só não dá para mostrar o aviso de confirmação
  }
}

function readCheckoutPlan(): string | null {
  try {
    return sessionStorage.getItem(CHECKOUT_PLAN_KEY);
  } catch {
    return null;
  }
}

// Fim da volta do checkout: esquece o plano guardado e tira o ?preapproval_id da URL.
function forgetCheckout() {
  try {
    sessionStorage.removeItem(CHECKOUT_PLAN_KEY);
  } catch {
    // nada a fazer
  }
  if (window.location.search) window.history.replaceState(null, "", window.location.pathname);
}

const card =
  "rounded-2xl border border-slate-200/85 bg-white p-5 shadow-[0_1px_3px_0_rgba(15,23,42,0.03),0_4px_12px_-2px_rgba(15,23,42,0.05)] sm:p-6";

const primaryBtn =
  "inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-primary-container px-4 text-label-md font-semibold text-on-primary transition-colors hover:bg-[#1d4ed8] disabled:cursor-not-allowed disabled:opacity-50";

const secondaryBtn =
  "inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-slate-200 bg-white px-4 text-label-md font-semibold text-on-surface transition-colors hover:border-slate-300 hover:bg-surface disabled:cursor-not-allowed disabled:opacity-50";

const tag =
  "inline-flex min-h-10 items-center justify-center rounded-lg bg-surface-container-low px-3 py-2 text-center text-label-md font-semibold text-on-surface-variant";

function formatDate(iso: string) {
  return new Date(iso).toLocaleDateString("pt-BR", { day: "2-digit", month: "2-digit", year: "numeric" });
}

// Resultado de um checkout que deixou de estar pendente. "Pendente sumiu" não quer dizer
// "pago": o backend também limpa o pendente quando o checkout é cancelado ou expira.
function paymentOutcome(expectedPlanCode: string | null, data: MySubscription) {
  return data.plan.code === expectedPlanCode
    ? `Pagamento confirmado! Seu plano agora é ${data.plan.name}.`
    : `O pagamento não foi concluído (o checkout foi cancelado ou expirou). Seu plano continua ${data.plan.name}.`;
}

export default function SubscriptionPage() {
  return (
    <DashboardShell title="Assinatura" subtitle="Seu plano, o que você está usando e os planos disponíveis.">
      {() => <SubscriptionContent />}
    </DashboardShell>
  );
}

function SubscriptionContent() {
  const [mine, setMine] = useState<MySubscription | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null); // código do plano em ação, ou "cancel" / "sync"
  const [confirmCancel, setConfirmCancel] = useState(false);
  // true logo depois de voltar do checkout: confere rápido (a cada 5s) por 1 minuto.
  const [waitingPayment, setWaitingPayment] = useState(false);

  // Plano do pagamento em andamento: o pendente visto por último ou, ao voltar do Mercado Pago,
  // o plano guardado antes de ir para o checkout. Quando o pendente some, compara com o plano
  // atual para saber se foi pago ou se o checkout foi cancelado/expirou.
  const pendingPlanRef = useRef<string | null>(null);

  // Cada consulta ganha um número; só a resposta da consulta mais recente vale. Sem isso, uma
  // resposta antiga ("pendente") chegando depois de uma nova ("pago") desfaria a tela.
  const requestSeq = useRef(0);
  const lastRefresh = useRef(0);

  // Busca o estado (conferindo no Mercado Pago). null = chegou uma resposta mais nova; ignore.
  const fetchState = useCallback(async (force: boolean) => {
    const seq = ++requestSeq.current;
    lastRefresh.current = Date.now();
    const data = await unwrap(syncSubscription(force)).catch(() => unwrap(getMySubscription()));
    return seq === requestSeq.current ? data : null;
  }, []);

  // Aplica um estado novo e, se o pagamento que estava pendente deixou de estar, avisa o resultado.
  const receive = useCallback((data: MySubscription) => {
    setMine(data);
    const pendingBefore = pendingPlanRef.current;
    pendingPlanRef.current = data.pendingPlan?.code ?? null;
    if (data.pendingPlan) return;

    setWaitingPayment(false);
    if (pendingBefore) {
      setNotice(paymentOutcome(pendingBefore, data));
      forgetCheckout();
    }
  }, []);

  // Para ações que mudam o pendente por outro motivo (ex.: cancelar a assinatura): aplica o
  // estado sem o aviso de "pagamento concluído/não concluído".
  function applyQuietly(data: MySubscription) {
    requestSeq.current++; // descarta consultas que ainda estejam em andamento
    setMine(data);
    pendingPlanRef.current = data.pendingPlan?.code ?? null;
  }

  // Ao abrir: confere no Mercado Pago (pega, por ex., cancelamento feito direto no app deles).
  useEffect(() => {
    let active = true;
    // Voltou do checkout (o back_url traz ?preapproval_id=...)
    const returning = new URLSearchParams(window.location.search).has("preapproval_id");
    const checkoutPlan = returning ? readCheckoutPlan() : null;
    if (checkoutPlan) pendingPlanRef.current = checkoutPlan;

    fetchState(false)
      .then((data) => {
        if (!active || !data) return;
        receive(data);
        if (returning && data.pendingPlan) setWaitingPayment(true);
        else if (returning) forgetCheckout();
      })
      .catch((err) => active && setError(err instanceof Error ? err.message : "Erro ao carregar a assinatura."));
    return () => {
      active = false;
    };
  }, [fetchState, receive]);

  // Enquanto houver pagamento pendente, confere sozinho: a cada 5s no primeiro minuto depois
  // de voltar do checkout e, depois disso, a cada 30s (só com a aba visível) por até 15 min.
  // Uma consulta só começa depois que a anterior terminou.
  const hasPending = !!mine?.pendingPlan;
  useEffect(() => {
    if (!hasPending) return;
    let stopped = false;
    let timer: ReturnType<typeof setTimeout>;
    let polls = 0;
    const delay = waitingPayment ? FAST_POLL_MS : SLOW_POLL_MS;
    const maxPolls = waitingPayment ? FAST_POLL_COUNT : SLOW_POLL_COUNT;

    const tick = async () => {
      polls += 1;
      if (waitingPayment || document.visibilityState === "visible") {
        try {
          const data = await fetchState(waitingPayment);
          if (stopped) return;
          if (data) receive(data);
        } catch {
          // sem rede: tenta de novo na próxima
        }
      }
      if (stopped) return;
      if (polls < maxPolls) timer = setTimeout(tick, delay);
      else if (waitingPayment) setWaitingPayment(false); // passa para a conferência lenta
    };

    timer = setTimeout(tick, delay);
    return () => {
      stopped = true;
      clearTimeout(timer);
    };
  }, [hasPending, waitingPayment, fetchState, receive]);

  // Voltou para a aba (ex.: pagou no Mercado Pago em outra aba): confere na hora.
  useEffect(() => {
    const onVisible = () => {
      if (document.visibilityState !== "visible" || Date.now() - lastRefresh.current < 2000) return;
      fetchState(false)
        .then((data) => data && receive(data))
        .catch(() => {});
    };
    document.addEventListener("visibilitychange", onVisible);
    window.addEventListener("focus", onVisible);
    return () => {
      document.removeEventListener("visibilitychange", onVisible);
      window.removeEventListener("focus", onVisible);
    };
  }, [fetchState, receive]);

  function startAction(key: string) {
    setError(null);
    setNotice(null);
    setBusy(key);
  }

  async function handleOption(option: PlanOption) {
    if (option.action === "CancelToFree") {
      setConfirmCancel(true);
      document.getElementById("cancel-title")?.scrollIntoView({ behavior: "smooth", block: "center" });
      return;
    }

    startAction(option.plan.code);
    try {
      const res = await startCheckout(option.plan.code);
      if (!res.status || !res.dados) throw new Error(res.mensagem ?? "Não foi possível trocar de plano.");

      if (res.dados.checkoutUrl) {
        rememberCheckout(option.plan.code); // para saber, na volta, qual plano estava sendo pago
        window.location.assign(res.dados.checkoutUrl); // vai pagar no Mercado Pago
        return;
      }
      applyQuietly(await unwrap(getMySubscription()));
      setNotice(res.mensagem ?? "Plano atualizado.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível trocar de plano.");
    }
    setBusy(null);
  }

  async function handleSync() {
    const hadPending = pendingPlanRef.current !== null;
    startAction("sync");
    try {
      const data = await fetchState(true);
      if (data) {
        receive(data); // se o pendente foi resolvido, já mostra se foi pago ou não
        if (data.pendingPlan)
          setNotice("O Mercado Pago ainda não confirmou o pagamento. Se você acabou de pagar, aguarde um pouco e tente de novo.");
        else if (!hadPending) setNotice(`Tudo certo! Seu plano é ${data.plan.name}.`);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível verificar o pagamento.");
    }
    setBusy(null);
  }

  async function handleCancel() {
    startAction("cancel");
    try {
      const data = await unwrap(cancelSubscription());
      applyQuietly(data);
      setConfirmCancel(false);
      setNotice(
        data.currentPeriodEnd
          ? `Assinatura cancelada. Seu plano continua valendo até ${formatDate(data.currentPeriodEnd)}.`
          : "Assinatura cancelada."
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível cancelar.");
    }
    setBusy(null);
  }

  if (!mine) {
    return error ? (
      <Banner tone="error">{error}</Banner>
    ) : (
      <div role="status" className="flex items-center gap-2 text-body-md text-on-surface-variant">
        <LoaderCircle size={18} aria-hidden="true" className="animate-spin text-primary-container" />
        Carregando...
      </div>
    );
  }

  const { plan } = mine;
  const periodEnd = mine.currentPeriodEnd ? formatDate(mine.currentPeriodEnd) : null;
  const canceledInPeriod =
    mine.status === "Canceled" && !!mine.currentPeriodEnd && new Date(mine.currentPeriodEnd) > new Date();
  const graceEnds = mine.pastDueSince
    ? formatDate(new Date(new Date(mine.pastDueSince).getTime() + GRACE_DAYS * 86_400_000).toISOString())
    : null;

  return (
    <div className="flex flex-col gap-6">
      {/* Avisos */}
      <div aria-live="polite" className="flex flex-col gap-3 empty:hidden">
        {error && <Banner tone="error">{error}</Banner>}
        {notice && <Banner tone="success">{notice}</Banner>}
        {waitingPayment ? (
          <Banner tone="info">
            <LoaderCircle size={16} aria-hidden="true" className="mr-1.5 inline animate-spin" />
            Confirmando seu pagamento com o Mercado Pago. Isso costuma levar alguns segundos.
          </Banner>
        ) : (
          mine.pendingPlan && (
            <Banner tone="info">
              Há um pagamento do plano {mine.pendingPlan.name} aguardando confirmação. Enquanto isso, você continua no{" "}
              {plan.name}. Esta página confere sozinha de tempos em tempos.{" "}
              <button
                type="button"
                onClick={handleSync}
                disabled={busy !== null}
                className="font-semibold underline underline-offset-2 hover:no-underline disabled:opacity-60"
              >
                {busy === "sync" ? "Verificando..." : "Já paguei, verificar agora"}
              </button>
            </Banner>
          )
        )}
        {mine.scheduledPlan && periodEnd && (
          <Banner tone="info">
            Seu plano muda para {mine.scheduledPlan.name} em {periodEnd}. Até lá, você continua com o {plan.name}.
          </Banner>
        )}
        {mine.status === "PastDue" && graceEnds && (
          <Banner tone="warning">
            Não conseguimos cobrar sua mensalidade. Regularize o pagamento no Mercado Pago até {graceEnds} para não
            voltar ao plano Grátis. Até lá, não é possível trocar de plano.
          </Banner>
        )}
        {mine.status === "Suspended" && (
          <Banner tone="error">
            Sua assinatura foi suspensa por falta de pagamento e você voltou ao plano Grátis. Suas lojas e produtos
            continuam salvos; assine de novo para recuperar os limites.
          </Banner>
        )}
        {canceledInPeriod && periodEnd && (
          <Banner tone="info">
            Assinatura cancelada. O plano {plan.name} vale até {periodEnd}; depois disso você volta ao Grátis e pode
            assinar qualquer plano. Antes dessa data, só é possível fazer upgrade.
          </Banner>
        )}
      </div>

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
              {mine.status && plan.priceMonthly > 0 && `, assinatura ${SUBSCRIPTION_STATUS_LABELS[mine.status].toLowerCase()}`}
              {mine.status === "Active" && periodEnd && `, próxima cobrança em ${periodEnd}`}
            </p>
          </div>
          <CreditCard size={28} aria-hidden="true" className="text-primary-container" />
        </div>

        <div className="mt-6 grid gap-5 sm:grid-cols-2">
          <UsageBar label="Lojas" used={mine.storeCount} limit={plan.maxStores} />
          {mine.stores.map((s) => (
            <UsageBar key={s.storeId} label={`Produtos em ${s.storeName}`} used={s.productCount} limit={plan.maxProductsPerStore} />
          ))}
        </div>

        {mine.storeCount > plan.maxStores && (
          <p className="mt-5 rounded-lg bg-amber-500/10 px-3 py-2.5 text-body-sm text-amber-800">
            Você tem mais lojas do que o plano atual permite. Elas continuam funcionando, mas não dá para criar novas
            até voltar ao limite.
          </p>
        )}
      </section>

      {/* Planos */}
      <section aria-labelledby="plans-title">
        <h2 id="plans-title" className="mb-4 text-title-md font-bold text-on-surface">
          Planos
        </h2>
        <div className="grid gap-4 md:grid-cols-3">
          {mine.options.map((option) => {
            const p = option.plan;
            const isFree = p.priceMonthly <= 0;
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
                    {isFree ? "Grátis" : formatPrice(p.priceMonthly)}
                  </span>
                  {!isFree && <span className="text-body-sm text-on-surface-variant"> /mês</span>}
                </p>

                <ul className="mt-5 flex flex-1 flex-col gap-2.5 text-body-md text-on-surface">
                  <Feature ok>
                    {p.maxStores} {p.maxStores === 1 ? "loja" : "lojas"}
                  </Feature>
                  <Feature ok>
                    {p.maxProductsPerStore === null ? "Produtos ilimitados" : `Até ${p.maxProductsPerStore} produtos por loja`}
                  </Feature>
                  <Feature ok={p.allowsOnlinePayment}>Pagamento online (Pix e cartão)</Feature>
                </ul>

                <div className="mt-6 flex flex-col gap-2">
                  <OptionButton
                    option={option}
                    isCurrent={isCurrent}
                    busy={busy === p.code}
                    disabled={busy !== null}
                    onClick={() => handleOption(option)}
                  />
                </div>
              </article>
            );
          })}
        </div>
        <p className="mt-4 text-body-sm text-on-surface-variant">
          O pagamento é feito no Mercado Pago, com renovação automática todo mês. Upgrade: o plano novo é liberado assim
          que o pagamento for confirmado. Plano menor: a troca vale a partir da próxima cobrança.
        </p>
      </section>

      {/* Cancelar */}
      {mine.canCancel && (
        <section className={card} aria-labelledby="cancel-title">
          <h2 id="cancel-title" className="text-title-md font-bold text-on-surface">
            Cancelar assinatura
          </h2>
          <p className="mt-1 text-body-md text-on-surface-variant">
            As cobranças param{periodEnd ? ` e o plano continua valendo até ${periodEnd}` : ""}. Depois disso você volta ao
            Grátis, sem perder lojas nem produtos. O cancelamento é definitivo: até essa data, só será possível fazer
            upgrade.
          </p>
          {confirmCancel ? (
            <div className="mt-4 flex flex-wrap items-center gap-3">
              <span className="text-body-md font-semibold text-on-surface">Confirmar o cancelamento?</span>
              <button
                type="button"
                onClick={handleCancel}
                disabled={busy !== null}
                className="inline-flex h-10 items-center justify-center gap-2 rounded-lg border border-red-200 bg-red-50 px-4 text-label-md font-semibold text-red-600 hover:bg-red-100 disabled:opacity-60"
              >
                {busy === "cancel" && <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />}
                Sim, cancelar
              </button>
              <button type="button" onClick={() => setConfirmCancel(false)} disabled={busy !== null} className={secondaryBtn}>
                Manter assinatura
              </button>
            </div>
          ) : (
            <button type="button" onClick={() => setConfirmCancel(true)} className={`${secondaryBtn} mt-4`}>
              Cancelar assinatura
            </button>
          )}
        </section>
      )}
    </div>
  );
}

/** Desenha exatamente a ação que o backend permitiu para o plano. */
function OptionButton({
  option,
  isCurrent,
  busy,
  disabled,
  onClick,
}: {
  option: PlanOption;
  isCurrent: boolean;
  busy: boolean;
  disabled: boolean;
  onClick: () => void;
}) {
  const spinner = busy && <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />;

  switch (option.action) {
    case "Checkout":
      return (
        <button type="button" onClick={onClick} disabled={disabled} className={primaryBtn}>
          {spinner || <ExternalLink size={16} aria-hidden="true" />}
          {option.label}
        </button>
      );
    case "ScheduleDowngrade":
    case "KeepCurrent":
    case "CancelToFree":
      return (
        <>
          {isCurrent && <span className={tag}>Plano atual</span>}
          <button type="button" onClick={onClick} disabled={disabled} className={secondaryBtn}>
            {spinner}
            {option.label}
          </button>
        </>
      );
    default:
      // Current / Unavailable: só a etiqueta, sem ação
      return <span className={tag}>{option.label}</span>;
  }
}

function Banner({ tone, children }: { tone: "error" | "success" | "info" | "warning"; children: ReactNode }) {
  const styles = {
    error: "border-red-200 bg-red-50 text-red-700",
    success: "border-emerald-200 bg-emerald-50 text-emerald-800",
    info: "border-blue-200 bg-blue-50 text-blue-800",
    warning: "border-amber-200 bg-amber-50 text-amber-800",
  }[tone];
  const Icon = tone === "success" ? CircleCheck : tone === "warning" ? TriangleAlert : CircleAlert;

  return (
    <div role={tone === "error" ? "alert" : "status"} className={`flex items-start gap-2 rounded-lg border px-4 py-3 text-body-md ${styles}`}>
      {tone !== "info" && <Icon size={18} aria-hidden="true" className="mt-px shrink-0" />}
      <span>{children}</span>
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
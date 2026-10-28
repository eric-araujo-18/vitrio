"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ExternalLink, LoaderCircle, MailCheck, Search, ShieldAlert } from "lucide-react";
import DashboardShell from "@/components/InitialPage/DashboardShell/DashboardShell";
import { ROLES, unwrap } from "@/lib/api";
import {
  adminConfirmEmail,
  adminSetPlan,
  getAdminOverview,
  searchAdminUsers,
  type AdminOverview,
  type AdminRoleFilter,
  type AdminUser,
} from "@/lib/api_admin";
import { getPlans, SUBSCRIPTION_STATUS_LABELS, type Plan } from "@/lib/api_subscription";
import { formatDateTime, formatPrice } from "@/lib/format";
import {
  ErrorBox,
  SuccessBox,
  btnPrimary,
  btnSecondary,
  card,
  cardSubtitle,
  cardTitle,
  hint,
  input,
} from "@/app/store/[slug]/shopkeeper/components/Ui";

// Painel do Admin: números gerais e busca de contas para o suporte (trocar plano sem cobrança,
// confirmar um e-mail que não chegou). A API só responde para quem tem o papel Admin.
export default function AdminPage() {
  return (
    <DashboardShell title="Admin" subtitle="Números da plataforma e suporte às contas.">
      {(user) =>
        user.role === ROLES.ADMIN ? (
          <AdminPanel />
        ) : (
          <div className={`${card} flex items-start gap-3`}>
            <ShieldAlert size={20} aria-hidden="true" className="mt-0.5 shrink-0 text-outline" />
            <p className="text-body-md text-on-surface-variant">Esta área é só para administradores.</p>
          </div>
        )
      }
    </DashboardShell>
  );
}

function AdminPanel() {
  const [overview, setOverview] = useState<AdminOverview | null>(null);
  const [plans, setPlans] = useState<Plan[]>([]);

  useEffect(() => {
    unwrap(getAdminOverview()).then(setOverview).catch(() => {});
    unwrap(getPlans()).then(setPlans).catch(() => {});
  }, []);

  return (
    <div className="flex max-w-[1100px] flex-col gap-6">
      <section className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
        <Stat label="Lojistas" value={overview?.shopkeepers} detail={overview && `${overview.unconfirmedShopkeepers} sem confirmar`} />
        <Stat label="Clientes" value={overview?.clients} />
        <Stat label="Lojas no ar" value={overview?.activeStores} />
        <Stat label="Pedidos (30 dias)" value={overview?.ordersLast30Days} />
        <Stat
          label="Assinaturas pagas"
          value={overview?.paidSubscriptions.reduce((sum, p) => sum + p.count, 0)}
          detail={overview?.paidSubscriptions.map((p) => `${p.planName}: ${p.count}`).join(" · ") || undefined}
        />
        <Stat label="Receita mensal" value={overview && formatPrice(overview.monthlyRecurringRevenue)} />
      </section>

      <UserSearch plans={plans} />
    </div>
  );
}

function Stat({ label, value, detail }: { label: string; value: number | string | undefined | null; detail?: string | null | false }) {
  return (
    <div className={card}>
      <p className={hint}>{label}</p>
      <p className="mt-1 text-headline-md text-on-surface tabular-nums">{value ?? "—"}</p>
      {detail && <p className={`${hint} mt-1`}>{detail}</p>}
    </div>
  );
}

const ROLE_OPTIONS: { value: AdminRoleFilter; label: string }[] = [
  { value: "Shopkeeper", label: "Lojistas" },
  { value: "Client", label: "Clientes" },
  { value: "Admin", label: "Admins" },
  { value: "all", label: "Todos" },
];

const ROLE_LABELS: Record<string, string> = { Shopkeeper: "Lojista", Client: "Cliente", Admin: "Admin" };

function UserSearch({ plans }: { plans: Plan[] }) {
  const [search, setSearch] = useState("");
  const [role, setRole] = useState<AdminRoleFilter>("Shopkeeper");
  const [users, setUsers] = useState<AdminUser[] | null>(null);
  // Começa carregando: a tela abre com os lojistas mais recentes.
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async (term: string, roleFilter: AdminRoleFilter) => {
    setLoading(true);
    setError(null);
    try {
      setUsers(await unwrap(searchAdminUsers(term, roleFilter)));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao buscar as contas.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    unwrap(searchAdminUsers("", "Shopkeeper"))
      .then((list) => active && setUsers(list))
      .catch((err) => active && setError(err instanceof Error ? err.message : "Erro ao buscar as contas."))
      .finally(() => active && setLoading(false));
    return () => {
      active = false;
    };
  }, []);

  function handleSubmit(e: FormEvent) {
    e.preventDefault();
    void load(search, role);
  }

  const replaceUser = (updated: AdminUser) =>
    setUsers((list) => list?.map((u) => (u.id === updated.id ? updated : u)) ?? null);

  return (
    <section className={card}>
      <h2 className={cardTitle}>Contas</h2>
      <p className={cardSubtitle}>Busque por nome, e-mail, CPF ou id. Mostra até 50 contas, das mais novas para as mais antigas.</p>

      <form onSubmit={handleSubmit} className="mt-4 flex flex-col gap-2 sm:flex-row">
        <input
          aria-label="Buscar contas"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="maria@exemplo.com"
          className={`${input} sm:flex-1`}
        />
        <select
          aria-label="Tipo de conta"
          value={role}
          onChange={(e) => setRole(e.target.value as AdminRoleFilter)}
          className={`${input} sm:w-40`}
        >
          {ROLE_OPTIONS.map((o) => (
            <option key={o.value} value={o.value}>
              {o.label}
            </option>
          ))}
        </select>
        <button type="submit" disabled={loading} className={btnPrimary}>
          {loading ? <LoaderCircle size={16} aria-hidden="true" className="animate-spin" /> : <Search size={16} aria-hidden="true" />}
          Buscar
        </button>
      </form>

      {error && (
        <div className="mt-4">
          <ErrorBox>{error}</ErrorBox>
        </div>
      )}

      <div className="mt-4 flex flex-col gap-3">
        {users?.length === 0 && <p className="text-body-md text-on-surface-variant">Nenhuma conta encontrada.</p>}
        {users?.map((u) => <UserRow key={u.id} user={u} plans={plans} onChanged={replaceUser} />)}
      </div>
    </section>
  );
}

function UserRow({ user, plans, onChanged }: { user: AdminUser; plans: Plan[]; onChanged: (u: AdminUser) => void }) {
  const [planCode, setPlanCode] = useState(user.planCode);
  const [confirming, setConfirming] = useState(false);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ ok: boolean; text: string } | null>(null);

  const targetPlan = plans.find((p) => p.code === planCode);
  const isShopkeeper = user.role === "Shopkeeper";

  async function handleConfirmEmail() {
    setBusy(true);
    setMessage(null);
    try {
      onChanged(await unwrap(adminConfirmEmail(user.id)));
      setMessage({ ok: true, text: "E-mail confirmado." });
    } catch (err) {
      setMessage({ ok: false, text: err instanceof Error ? err.message : "Erro ao confirmar." });
    } finally {
      setBusy(false);
    }
  }

  async function handleSetPlan() {
    setBusy(true);
    setMessage(null);
    try {
      await unwrap(adminSetPlan(user.id, planCode));
      setConfirming(false);
      // Recarrega a linha com o plano e a situação novos.
      const fresh = await unwrap(searchAdminUsers(String(user.id), "all"));
      const updated = fresh.find((f) => f.id === user.id);
      if (updated) onChanged(updated);
      setMessage({ ok: true, text: `Plano trocado para ${targetPlan?.name ?? planCode}, sem cobrança.` });
    } catch (err) {
      setMessage({ ok: false, text: err instanceof Error ? err.message : "Erro ao trocar o plano." });
    } finally {
      setBusy(false);
    }
  }

  return (
    <article className="rounded-xl border border-slate-200 p-4">
      <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
        <div className="min-w-0">
          <p className="flex flex-wrap items-center gap-2 text-body-md font-semibold text-on-surface">
            {user.name}
            <span className="rounded-full bg-surface-container px-2 py-0.5 text-label-sm font-semibold text-on-surface-variant">
              {ROLE_LABELS[user.role] ?? user.role} #{user.id}
            </span>
            {!user.emailConfirmed && (
              <span className="rounded-full bg-amber-100 px-2 py-0.5 text-label-sm font-semibold text-amber-800">
                e-mail não confirmado
              </span>
            )}
          </p>
          <p className="truncate text-body-sm text-on-surface-variant">
            {user.email}
            {user.phone ? ` · ${user.phone}` : ""} · desde {formatDateTime(user.creationDate)}
          </p>
          {isShopkeeper && (
            <p className="mt-1 text-body-sm text-on-surface">
              Plano <strong>{user.planName}</strong>
              {user.subscriptionStatus && ` · ${SUBSCRIPTION_STATUS_LABELS[user.subscriptionStatus]}`}
              {user.manualPlan && " · definido pelo Admin, sem cobrança"}
              {user.currentPeriodEnd && ` · pago até ${formatDateTime(user.currentPeriodEnd)}`}
            </p>
          )}
          {user.stores.length > 0 && (
            <p className="mt-1 flex flex-wrap gap-x-3 gap-y-1 text-body-sm">
              {user.stores.map((s) => (
                <a
                  key={s.id}
                  href={`/store/${s.slug}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="inline-flex items-center gap-1 text-primary-container hover:underline"
                >
                  {s.name}
                  {!s.isActive && " (pausada)"}
                  <ExternalLink size={12} aria-hidden="true" />
                </a>
              ))}
            </p>
          )}
        </div>

        <div className="flex shrink-0 flex-col gap-2 md:items-end">
          {!user.emailConfirmed && (
            <button type="button" onClick={handleConfirmEmail} disabled={busy} className={btnSecondary}>
              <MailCheck size={16} aria-hidden="true" />
              Confirmar e-mail
            </button>
          )}
          {isShopkeeper && plans.length > 0 && (
            <div className="flex gap-2">
              <select
                aria-label={`Plano de ${user.name}`}
                value={planCode}
                onChange={(e) => {
                  setPlanCode(e.target.value);
                  setConfirming(false);
                  setMessage(null);
                }}
                disabled={busy}
                className={`${input} h-10 py-0`}
              >
                {plans.map((p) => (
                  <option key={p.code} value={p.code}>
                    {p.name}
                  </option>
                ))}
              </select>
              <button
                type="button"
                onClick={() => setConfirming(true)}
                disabled={busy || planCode === user.planCode}
                className={btnSecondary}
              >
                Trocar plano
              </button>
            </div>
          )}
        </div>
      </div>

      {confirming && (
        <div className="mt-3 flex flex-col gap-3 rounded-lg border border-amber-200 bg-amber-50 p-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-body-sm text-amber-900">
            Trocar {user.name} para o plano {targetPlan?.name ?? planCode}, sem cobrança? A cobrança no Mercado Pago, se
            houver, é cancelada. O plano vale até um Admin mudar de novo.
          </p>
          <div className="flex shrink-0 gap-2">
            <button type="button" onClick={() => setConfirming(false)} disabled={busy} className={btnSecondary}>
              Cancelar
            </button>
            <button type="button" onClick={handleSetPlan} disabled={busy} className={btnPrimary}>
              {busy && <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />}
              Confirmar
            </button>
          </div>
        </div>
      )}

      {message && <div className="mt-3">{message.ok ? <SuccessBox>{message.text}</SuccessBox> : <ErrorBox>{message.text}</ErrorBox>}</div>}
    </article>
  );
}

"use client";

import { Suspense, useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Check,
  Copy,
  Link as LinkIcon,
  LoaderCircle,
  Mail,
  Pause,
  Play,
  Power,
  Save,
  Trash2,
  Truck,
} from "lucide-react";
import { deleteStore, goOnlineStore, unwrap, updateStore } from "@/lib/api";
import { useAuth } from "@/lib/auth_context";
import { formatCnpj, isValidCnpj } from "@/lib/validators";
import { formatPrice, moneyToInput, parseMoney } from "@/lib/format";
import { useShopkeeperStore } from "../../components/ShopkeeperStoreContext";
import OnlinePaymentCard from "../../components/OnlinePaymentCard";
import {
  ErrorBox,
  PageHeader,
  StatusBadge,
  SuccessBox,
  btnDanger,
  btnPrimary,
  btnSecondary,
  card,
  cardSubtitle,
  cardTitle,
  hint,
  input,
  label,
} from "../../components/Ui";

export default function ConfiguracoesPage() {
  const { store, setStore } = useShopkeeperStore();
  const { user } = useAuth();
  const router = useRouter();
  const [savingNotify, setSavingNotify] = useState(false);
  const [notifyError, setNotifyError] = useState<string | null>(null);

  const [name, setName] = useState(store.name);
  const [cnpj, setCnpj] = useState(store.cnpj ? formatCnpj(store.cnpj) : "");
  const [saving, setSaving] = useState(false);
  const [toggling, setToggling] = useState(false);
  const [confirmingGoOnline, setConfirmingGoOnline] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const [copied, setCopied] = useState(false);
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const [deleteConfirmText, setDeleteConfirmText] = useState("");
  // Fica dentro do quadro de exclusão (ex.: loja com pedidos em andamento), não no topo da página.
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const publicUrl =
    typeof window !== "undefined" ? `${window.location.origin}/store/${store.slug}` : `/store/${store.slug}`;

  const deleteNameMatches = deleteConfirmText.trim() === store.name;

  // Ativa e no ar / ativa mas fora do ar pelo limite de lojas do plano / pausada pelo lojista.
  const visibility = !store.isActive
    ? { badge: "Pending", label: "Pausada" }
    : store.blockedByPlan
      ? { badge: "Blocked", label: "Fora do ar" }
      : { badge: "Active", label: "Ativa" };

  // Pausada com o limite de lojas no ar cheio: só dá para reativar trocando com outra loja.
  const reactivateNeedsSwitch = !store.isActive && store.storeLimitReached;
  const canSwitch = store.blockedByPlan || reactivateNeedsSwitch;

  // Volta o botão "Copiado" para "Copiar" depois de um tempo.
  useEffect(() => {
    if (!copied) return;
    const t = setTimeout(() => setCopied(false), 2000);
    return () => clearTimeout(t);
  }, [copied]);

  async function handleCopy() {
    try {
      await navigator.clipboard?.writeText(publicUrl);
      setCopied(true);
    } catch {
      setError("Não foi possível copiar. Selecione o link e copie manualmente.");
    }
  }

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    if (!name.trim()) {
      setError("Informe o nome da loja.");
      return;
    }
    if (cnpj.trim() && !isValidCnpj(cnpj)) {
      setError("CNPJ inválido.");
      return;
    }

    setSaving(true);
    try {
      const updated = await unwrap(updateStore(store.id, { name: name.trim(), cnpj: cnpj.trim() }));
      setStore(updated);
      setSuccess("Dados salvos.");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao salvar.");
    } finally {
      setSaving(false);
    }
  }

  async function handleToggleNotify(enabled: boolean) {
    setSavingNotify(true);
    setNotifyError(null);
    try {
      setStore(await unwrap(updateStore(store.id, { notifyNewOrdersByEmail: enabled })));
    } catch (err) {
      setNotifyError(err instanceof Error ? err.message : "Erro ao salvar o aviso por e-mail.");
    } finally {
      setSavingNotify(false);
    }
  }

  async function handleToggleActive() {
    setToggling(true);
    setError(null);
    try {
      setStore(await unwrap(updateStore(store.id, { isActive: !store.isActive })));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao alterar status.");
    } finally {
      setToggling(false);
    }
  }

  // Escolhe esta loja para ficar no ar; as outras que passarem do limite do plano são pausadas.
  async function handleGoOnline() {
    setToggling(true);
    setError(null);
    try {
      const stores = await unwrap(goOnlineStore(store.id));
      const updated = stores.find((s) => s.id === store.id);
      if (updated) setStore(updated);
      setConfirmingGoOnline(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao colocar a loja no ar.");
    } finally {
      setToggling(false);
    }
  }

  function cancelDelete() {
    setConfirmingDelete(false);
    setDeleteConfirmText("");
    setDeleteError(null);
  }

  async function handleDelete() {
    if (!deleteNameMatches) return;

    setDeleting(true);
    setDeleteError(null);
    try {
      await unwrap(deleteStore(store.id));
      router.replace("/menu/stores");
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : "Erro ao excluir a loja.");
      setDeleting(false);
    }
  }

  return (
    <>
      <PageHeader title="Configurações" subtitle="Dados da loja, pagamento online, visibilidade e exclusão." />

      <div className="flex max-w-[680px] flex-col gap-5">
        {/* Erro geral no topo, para não passar despercebido */}
        {error && <ErrorBox>{error}</ErrorBox>}

        {/* Link da vitrine */}
        <section className={card}>
          <h2 className={cardTitle}>Link da vitrine</h2>
          <p className={cardSubtitle}>O endereço não muda se você renomear a loja.</p>

          <div className="mt-4 flex flex-col gap-2 sm:flex-row">
            <div className="relative flex-1">
              <LinkIcon
                size={16}
                aria-hidden="true"
                className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-outline"
              />
              <input
                aria-label="Link da vitrine"
                value={publicUrl}
                readOnly
                onFocus={(e) => e.target.select()}
                className={`${input} h-10 bg-surface py-0 pl-9 font-mono text-code-sm`}
              />
            </div>
            <button type="button" onClick={handleCopy} className={`${btnSecondary} sm:w-32`}>
              {copied ? (
                <>
                  <Check size={16} aria-hidden="true" className="text-emerald-600" />
                  Copiado
                </>
              ) : (
                <>
                  <Copy size={16} aria-hidden="true" />
                  Copiar
                </>
              )}
            </button>
          </div>
        </section>

        {/* Dados da loja */}
        <form onSubmit={handleSave} className={card} noValidate>
          <h2 className={cardTitle}>Dados da loja</h2>

          <div className="mt-5 flex flex-col gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="name" className={label}>
                Nome
              </label>
              <input
                id="name"
                value={name}
                onChange={(e) => {
                  setName(e.target.value);
                  setSuccess(null);
                }}
                maxLength={100}
                className={input}
              />
            </div>

            <div className="flex flex-col gap-1.5">
              <label htmlFor="cnpj" className={label}>
                CNPJ <span className="font-normal text-outline">(opcional)</span>
              </label>
              <input
                id="cnpj"
                value={cnpj}
                onChange={(e) => {
                  setCnpj(formatCnpj(e.target.value));
                  setSuccess(null);
                }}
                placeholder="00.000.000/0000-00"
                inputMode="numeric"
                maxLength={18}
                className={input}
              />
            </div>

            {success && <SuccessBox>{success}</SuccessBox>}

            <div className="flex justify-end">
              <button type="submit" disabled={saving} className={btnPrimary}>
                {saving ? (
                  <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
                ) : (
                  <Save size={16} aria-hidden="true" />
                )}
                {saving ? "Salvando..." : "Salvar"}
              </button>
            </div>
          </div>
        </form>

        {/* Entrega */}
        <DeliveryCard />

        {/* Aviso de pedido novo por e-mail */}
        <section className={card}>
          <label htmlFor="notify-email" className="flex cursor-pointer items-start gap-4">
            <span className="min-w-0 flex-1">
              <span className="flex items-center gap-2">
                <Mail size={18} aria-hidden="true" className="text-outline" />
                <span className={cardTitle}>Avisar pedidos novos por e-mail</span>
              </span>
              <span className={`${cardSubtitle} block`}>
                {user?.email ? (
                  <>
                    Enviamos um e-mail para <strong className="text-on-surface">{user.email}</strong> a cada pedido
                    novo
                  </>
                ) : (
                  "Enviamos um e-mail a cada pedido novo"
                )}
                , além do aviso aqui no painel. Pedidos pagos online avisam quando o pagamento é aprovado.
              </span>
            </span>
            {/* Switch: checkbox real escondido + trilho desenhado */}
            <input
              id="notify-email"
              type="checkbox"
              role="switch"
              checked={store.notifyNewOrdersByEmail}
              disabled={savingNotify}
              onChange={(e) => handleToggleNotify(e.target.checked)}
              className="peer sr-only"
            />
            <span
              aria-hidden="true"
              className="relative mt-1 h-6 w-11 shrink-0 rounded-full bg-slate-300 transition-colors peer-checked:bg-primary-container peer-focus-visible:ring-[3px] peer-focus-visible:ring-primary-container/25 peer-disabled:opacity-60 after:absolute after:top-0.5 after:left-0.5 after:h-5 after:w-5 after:rounded-full after:bg-white after:shadow-sm after:transition-transform peer-checked:after:translate-x-5"
            />
          </label>
          {notifyError && (
            <div className="mt-3">
              <ErrorBox>{notifyError}</ErrorBox>
            </div>
          )}
        </section>

        {/* Pagamento online (Mercado Pago) */}
        <Suspense fallback={null}>
          <OnlinePaymentCard storeId={store.id} />
        </Suspense>

        {/* Visibilidade */}
        <section className={card}>
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <div className="flex flex-wrap items-center gap-2">
                <h2 className={cardTitle}>Visibilidade</h2>
                <StatusBadge status={visibility.badge}>{visibility.label}</StatusBadge>
              </div>
              <p className={cardSubtitle}>
                {!store.isActive ? (
                  "A vitrine está fora do ar. Seus dados continuam salvos."
                ) : store.blockedByPlan ? (
                  <>
                    Seu plano permite menos lojas no ar do que você tem, então visitantes não veem esta vitrine nem
                    fazem pedidos. Escolha esta loja para ficar no ar, ou{" "}
                    <Link href="/menu/subscription" className="font-semibold text-primary-container hover:underline">
                      veja os planos
                    </Link>
                    .
                  </>
                ) : (
                  "Visitantes veem a vitrine e podem fazer pedidos."
                )}
              </p>
            </div>

            {!reactivateNeedsSwitch && (
              <button
                type="button"
                onClick={handleToggleActive}
                disabled={toggling}
                className={store.isActive ? btnSecondary : btnPrimary}
              >
                {toggling ? (
                  <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
                ) : store.isActive ? (
                  <Pause size={16} aria-hidden="true" />
                ) : (
                  <Play size={16} aria-hidden="true" />
                )}
                {store.isActive ? "Pausar loja" : "Reativar loja"}
              </button>
            )}
          </div>

          {canSwitch && (
            <div className="mt-4 flex flex-col gap-3 rounded-xl border border-amber-200 bg-amber-50 p-4 sm:flex-row sm:items-center sm:justify-between">
              <p className="text-body-md text-amber-900">
                {confirmingGoOnline
                  ? "As outras lojas que passarem do limite do plano serão pausadas. Dá para trocar de novo quando quiser."
                  : reactivateNeedsSwitch
                    ? "Seu plano já está com o máximo de lojas no ar. Para reativar esta, troque com a loja que está no ar."
                    : "Quer que esta seja a loja no ar?"}
              </p>
              <div className="flex shrink-0 gap-2">
                {confirmingGoOnline ? (
                  <>
                    <button
                      type="button"
                      onClick={() => setConfirmingGoOnline(false)}
                      disabled={toggling}
                      className={btnSecondary}
                    >
                      Cancelar
                    </button>
                    <button type="button" onClick={handleGoOnline} disabled={toggling} className={btnPrimary}>
                      {toggling && <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />}
                      Confirmar
                    </button>
                  </>
                ) : (
                  <button type="button" onClick={() => setConfirmingGoOnline(true)} className={btnPrimary}>
                    <Power size={16} aria-hidden="true" />
                    Deixar esta loja no ar
                  </button>
                )}
              </div>
            </div>
          )}
        </section>

        {/* Zona de perigo */}
        <section className={`${card} border-red-200!`}>
          <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className={cardTitle}>Excluir loja</h2>
              <p className={cardSubtitle}>Remove a loja do seu painel e tira a vitrine do ar.</p>
            </div>

            {!confirmingDelete && (
              <button type="button" onClick={() => setConfirmingDelete(true)} className={btnDanger}>
                <Trash2 size={16} aria-hidden="true" />
                Excluir loja
              </button>
            )}
          </div>

          {/* Confirmação digitando o nome (substitui o prompt do navegador) */}
          {confirmingDelete && (
            <div className="mt-5 flex animate-[fadeIn_0.25s_ease] flex-col gap-3 rounded-xl border border-red-200 bg-red-50/60 p-4">
              <label htmlFor="delete-confirm" className="text-body-md text-red-900">
                Para confirmar, digite o nome da loja:{" "}
                <strong className="font-semibold select-all">{store.name}</strong>
              </label>
              <input
                id="delete-confirm"
                value={deleteConfirmText}
                onChange={(e) => setDeleteConfirmText(e.target.value)}
                autoFocus
                autoComplete="off"
                disabled={deleting}
                className={`${input} focus:border-red-500 focus:ring-red-500/15`}
              />
              <p className={hint}>
                Essa ação não pode ser desfeita pelo painel. Lojas com pedidos em andamento só podem ser excluídas
                depois que eles forem concluídos ou cancelados.
              </p>
              {deleteError && <ErrorBox>{deleteError}</ErrorBox>}

              <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <button type="button" onClick={cancelDelete} disabled={deleting} className={btnSecondary}>
                  Cancelar
                </button>
                <button
                  type="button"
                  onClick={handleDelete}
                  disabled={!deleteNameMatches || deleting}
                  className="inline-flex h-10 items-center justify-center gap-2 rounded-lg bg-red-600 px-4 text-label-md font-semibold text-white transition-colors hover:bg-red-700 disabled:cursor-not-allowed disabled:opacity-50"
                >
                  {deleting ? (
                    <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
                  ) : (
                    <Trash2 size={16} aria-hidden="true" />
                  )}
                  {deleting ? "Excluindo..." : "Excluir definitivamente"}
                </button>
              </div>
            </div>
          )}
        </section>
      </div>
    </>
  );
}
/* ===========================
   ENTREGA
   A combinar (fora do total, como sempre foi), grátis, ou taxa fixa com frete grátis opcional a
   partir de um valor. O checkout soma a taxa ao total, inclusive no pagamento online.
=========================== */

type DeliveryMode = "arrange" | "fixed" | "free";

function DeliveryCard() {
  const { store, setStore } = useShopkeeperStore();
  const [mode, setMode] = useState<DeliveryMode>(
    store.deliveryFee == null ? "arrange" : store.deliveryFee === 0 ? "free" : "fixed"
  );
  const [fee, setFee] = useState(store.deliveryFee ? moneyToInput(store.deliveryFee) : "");
  const [freeFrom, setFreeFrom] = useState(moneyToInput(store.freeDeliveryFrom));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  function change<T>(setter: (value: T) => void) {
    return (value: T) => {
      setter(value);
      setSuccess(false);
    };
  }

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(false);

    let delivery: { fee: number | null; freeFrom: number | null } = { fee: null, freeFrom: null };
    if (mode === "free") delivery = { fee: 0, freeFrom: null };
    if (mode === "fixed") {
      const feeValue = parseMoney(fee);
      if (!Number.isFinite(feeValue) || feeValue <= 0) return setError("Informe o valor da taxa de entrega.");
      const freeValue = freeFrom.trim() ? parseMoney(freeFrom) : null;
      if (freeValue != null && (!Number.isFinite(freeValue) || freeValue <= 0)) {
        return setError("Informe um valor válido para a entrega grátis, ou deixe em branco.");
      }
      delivery = { fee: Math.round(feeValue * 100) / 100, freeFrom: freeValue == null ? null : Math.round(freeValue * 100) / 100 };
    }

    setSaving(true);
    try {
      const updated = await unwrap(updateStore(store.id, { delivery }));
      setStore(updated);
      setSuccess(true);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao salvar a entrega.");
    } finally {
      setSaving(false);
    }
  }

  const options: { value: DeliveryMode; title: string; text: string }[] = [
    {
      value: "arrange",
      title: "Combinar com o cliente",
      text: "A entrega fica fora do total. Você combina o valor depois do pedido, inclusive quando ele paga online.",
    },
    { value: "fixed", title: "Taxa fixa", text: "Um valor único por pedido, somado ao total e ao pagamento online." },
    { value: "free", title: "Entrega grátis", text: "Sem taxa em nenhum pedido." },
  ];

  return (
    <form onSubmit={handleSave} className={card} noValidate>
      <div className="flex items-center gap-2">
        <Truck size={18} aria-hidden="true" className="text-outline" />
        <h2 className={cardTitle}>Entrega</h2>
      </div>
      <p className={cardSubtitle}>O cliente vê a regra no carrinho, antes de fechar o pedido.</p>

      <div role="radiogroup" aria-label="Como cobrar a entrega" className="mt-4 flex flex-col gap-2">
        {options.map((option) => (
          <label
            key={option.value}
            className={`flex cursor-pointer items-start gap-3 rounded-lg border p-3 transition-colors ${
              mode === option.value ? "border-primary-container bg-primary-fixed/20" : "border-slate-200 hover:border-slate-300"
            }`}
          >
            <input
              type="radio"
              name="delivery-mode"
              checked={mode === option.value}
              onChange={() => change(setMode)(option.value)}
              disabled={saving}
              className="mt-1 h-4 w-4 shrink-0 accent-primary-container"
            />
            <span>
              <span className="block text-body-md font-semibold text-on-surface">{option.title}</span>
              <span className="block text-body-sm text-on-surface-variant">{option.text}</span>
            </span>
          </label>
        ))}
      </div>

      {mode === "fixed" && (
        <div className="mt-4 grid grid-cols-1 gap-4 sm:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <label htmlFor="delivery-fee" className={label}>
              Valor da taxa
            </label>
            <input
              id="delivery-fee"
              value={fee}
              onChange={(e) => change(setFee)(e.target.value)}
              placeholder="0,00"
              inputMode="decimal"
              disabled={saving}
              className={input}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="delivery-free-from" className={label}>
              Grátis a partir de <span className="font-normal text-outline">(opcional)</span>
            </label>
            <input
              id="delivery-free-from"
              value={freeFrom}
              onChange={(e) => change(setFreeFrom)(e.target.value)}
              placeholder="Ex.: 200,00"
              inputMode="decimal"
              disabled={saving}
              className={input}
            />
            <p className={hint}>Soma dos produtos do pedido.</p>
          </div>
        </div>
      )}

      {store.deliveryFee != null && store.deliveryFee > 0 && (
        <p className={`${hint} mt-3`}>
          Hoje: {formatPrice(store.deliveryFee)} por pedido
          {store.freeDeliveryFrom != null ? `, grátis a partir de ${formatPrice(store.freeDeliveryFrom)}` : ""}.
        </p>
      )}

      <div className="mt-4 flex flex-col gap-3">
        {error && <ErrorBox>{error}</ErrorBox>}
        {success && <SuccessBox>Entrega salva.</SuccessBox>}
        <div className="flex justify-end">
          <button type="submit" disabled={saving} className={btnPrimary}>
            {saving ? (
              <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
            ) : (
              <Save size={16} aria-hidden="true" />
            )}
            {saving ? "Salvando..." : "Salvar entrega"}
          </button>
        </div>
      </div>
    </form>
  );
}

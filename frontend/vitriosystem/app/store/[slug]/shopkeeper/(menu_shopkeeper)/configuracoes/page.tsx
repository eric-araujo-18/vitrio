"use client";

import { useEffect, useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  Check,
  Copy,
  Link as LinkIcon,
  LoaderCircle,
  Pause,
  Play,
  Power,
  Save,
  Trash2,
} from "lucide-react";
import { deleteStore, goOnlineStore, unwrap, updateStore } from "@/lib/api";
import { formatCnpj, isValidCnpj } from "@/lib/validators";
import { useShopkeeperStore } from "../../components/ShopkeeperStoreContext";
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
  const router = useRouter();

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
  }

  async function handleDelete() {
    if (!deleteNameMatches) return;

    setDeleting(true);
    setError(null);
    try {
      await unwrap(deleteStore(store.id));
      router.replace("/menu/stores");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao excluir a loja.");
      setDeleting(false);
    }
  }

  return (
    <>
      <PageHeader title="Configurações" subtitle="Dados da loja, visibilidade e exclusão." />

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
              <p className={hint}>Essa ação não pode ser desfeita pelo painel.</p>

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
"use client";

import { useCallback, useEffect, useState, type FormEvent } from "react";
import { ArrowLeft, CircleAlert, LoaderCircle, MapPin, Pencil, Plus, Star, Trash2, X } from "lucide-react";
import { unwrap } from "@/lib/api";
import { createAddress, deleteAddress, getMyAddresses, updateAddress } from "@/lib/api_customer";
import { useBackdropDismiss } from "@/lib/backdrop";
import {
  EMPTY_ADDRESS,
  addressLine1,
  addressLine2,
  toAddressPayload,
  toFormValues,
  validateAddress,
  type AddressFormValues,
  type SavedAddress,
} from "@/lib/address";
import AddressFields from "./AddressFields";
import { iconButton, storeOverlay, storePrimaryButton, storeSecondaryButton, useLockBodyScroll } from "./Ui";

interface AddressesDrawerProps {
  onClose: () => void;
}

// "Meus endereços": lista, cadastra, edita, apaga e escolhe o padrão.
export default function AddressesDrawer({ onClose }: AddressesDrawerProps) {
  const [addresses, setAddresses] = useState<SavedAddress[] | null>(null);
  const [error, setError] = useState<string | null>(null);

  // null = lista; "new" = novo; número = editando esse id
  const [editing, setEditing] = useState<"new" | number | null>(null);
  const [form, setForm] = useState<AddressFormValues>(EMPTY_ADDRESS);
  const [makeDefault, setMakeDefault] = useState(false);
  const [saving, setSaving] = useState(false);
  const [busyId, setBusyId] = useState<number | null>(null);

  useLockBodyScroll();
  const backdrop = useBackdropDismiss(() => !saving && onClose());

  const load = useCallback(async () => {
    try {
      setAddresses(await unwrap(getMyAddresses()));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao carregar endereços.");
    }
  }, []);

  // Carga inicial (setState só no retorno da promessa; "load" fica para recarregar depois das ações).
  useEffect(() => {
    let active = true;
    unwrap(getMyAddresses())
      .then((data) => active && setAddresses(data))
      .catch((err) => active && setError(err instanceof Error ? err.message : "Erro ao carregar endereços."));
    return () => {
      active = false;
    };
  }, []);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && !saving && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, saving]);

  function openForm(address?: SavedAddress) {
    setError(null);
    setEditing(address ? address.id : "new");
    setForm(address ? toFormValues(address) : EMPTY_ADDRESS);
    setMakeDefault(address ? address.isDefault : (addresses?.length ?? 0) === 0);
  }

  async function handleSave(e: FormEvent) {
    e.preventDefault();
    const invalid = validateAddress(form);
    if (invalid) return setError(invalid);

    setSaving(true);
    setError(null);
    try {
      const payload = toAddressPayload(form, makeDefault);
      await unwrap(editing === "new" ? createAddress(payload) : updateAddress(editing as number, payload));
      await load();
      setEditing(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível salvar o endereço.");
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete(address: SavedAddress) {
    if (!window.confirm("Remover este endereço? Pedidos já feitos não mudam.")) return;
    setBusyId(address.id);
    setError(null);
    try {
      await unwrap(deleteAddress(address.id));
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível remover.");
    } finally {
      setBusyId(null);
    }
  }

  async function handleMakeDefault(address: SavedAddress) {
    setBusyId(address.id);
    setError(null);
    try {
      await unwrap(updateAddress(address.id, toAddressPayload(toFormValues(address), true)));
      await load();
    } catch (err) {
      setError(err instanceof Error ? err.message : "Não foi possível alterar o padrão.");
    } finally {
      setBusyId(null);
    }
  }

  const isForm = editing !== null;

  return (
    <div className={`${storeOverlay} flex justify-end`} {...backdrop}>
      <aside
        role="dialog"
        aria-modal="true"
        aria-labelledby="addresses-title"
        className="flex h-full w-full max-w-[440px] animate-drawer-in flex-col bg-white shadow-[-10px_0_30px_rgba(0,0,0,0.12)]"
      >
        <div className="flex items-center justify-between gap-3 border-b border-slate-100 px-5 py-4">
          <div className="flex min-w-0 items-center gap-2">
            {isForm && (
              <button
                type="button"
                onClick={() => setEditing(null)}
                disabled={saving}
                aria-label="Voltar para a lista"
                className={`${iconButton} -ml-2`}
              >
                <ArrowLeft size={20} aria-hidden="true" />
              </button>
            )}
            <h2 id="addresses-title" className="truncate text-headline-sm text-slate-900">
              {editing === "new" ? "Novo endereço" : isForm ? "Editar endereço" : "Meus endereços"}
            </h2>
          </div>
          <button type="button" onClick={onClose} disabled={saving} aria-label="Fechar" className={iconButton}>
            <X size={20} aria-hidden="true" />
          </button>
        </div>

        {error && (
          <div
            role="alert"
            className="mx-5 mt-4 flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-body-md text-red-700"
          >
            <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
            <span>{error}</span>
          </div>
        )}

        {isForm ? (
          <form onSubmit={handleSave} noValidate className="flex flex-1 flex-col overflow-hidden">
            <div className="flex-1 overflow-y-auto px-5 py-4">
              <AddressFields idPrefix="addr" value={form} onChange={setForm} disabled={saving} showLabel />
              <label className="mt-4 flex items-center gap-2.5 text-body-md text-slate-700">
                <input
                  type="checkbox"
                  checked={makeDefault}
                  onChange={(e) => setMakeDefault(e.target.checked)}
                  disabled={saving}
                  className="h-4 w-4 accent-[var(--store-primary)]"
                />
                Usar como endereço padrão nas compras
              </label>
            </div>
            <div className="flex flex-col gap-2.5 border-t border-slate-100 px-5 pt-4 pb-[calc(1.25rem+env(safe-area-inset-bottom))]">
              <button type="submit" disabled={saving} className={storePrimaryButton}>
                {saving && <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />}
                {saving ? "Salvando..." : "Salvar endereço"}
              </button>
              <button type="button" onClick={() => setEditing(null)} disabled={saving} className={storeSecondaryButton}>
                Cancelar
              </button>
            </div>
          </form>
        ) : (
          <>
            <div className="flex-1 overflow-y-auto px-5 py-4">
              {addresses === null ? (
                !error && (
                  <div role="status" className="flex items-center justify-center gap-2 py-16 text-body-md text-slate-500">
                    <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />
                    Carregando...
                  </div>
                )
              ) : addresses.length === 0 ? (
                <div className="flex flex-col items-center gap-3 py-16 text-center">
                  <div className="flex h-16 w-16 items-center justify-center rounded-full bg-slate-100 text-slate-400">
                    <MapPin size={28} aria-hidden="true" />
                  </div>
                  <p className="max-w-xs text-body-md text-slate-500">
                    Salve um endereço para não precisar digitar de novo a cada compra.
                  </p>
                </div>
              ) : (
                <ul className="flex flex-col gap-3">
                  {addresses.map((a) => (
                    <li key={a.id} className="rounded-xl border border-slate-200 p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="flex flex-wrap items-center gap-2 text-body-md font-semibold text-slate-900">
                            {a.label || "Endereço"}
                            {a.isDefault && (
                              <span className="rounded-full bg-[color-mix(in_srgb,var(--store-primary)_12%,white)] px-2 py-0.5 text-label-sm text-[var(--store-primary)]">
                                Padrão
                              </span>
                            )}
                          </p>
                          <p className="mt-1 text-body-md text-slate-700">{addressLine1(a)}</p>
                          <p className="text-body-sm text-slate-500">{addressLine2(a)}</p>
                        </div>
                        {busyId === a.id && (
                          <LoaderCircle size={18} aria-label="Salvando" className="shrink-0 animate-spin text-slate-400" />
                        )}
                      </div>
                      <div className="mt-3 flex flex-wrap gap-x-4 gap-y-2 border-t border-slate-100 pt-3 text-label-md font-semibold">
                        <button
                          type="button"
                          onClick={() => openForm(a)}
                          disabled={busyId !== null}
                          className="inline-flex items-center gap-1.5 text-slate-600 hover:text-slate-900"
                        >
                          <Pencil size={15} aria-hidden="true" />
                          Editar
                        </button>
                        {!a.isDefault && (
                          <button
                            type="button"
                            onClick={() => handleMakeDefault(a)}
                            disabled={busyId !== null}
                            className="inline-flex items-center gap-1.5 text-slate-600 hover:text-slate-900"
                          >
                            <Star size={15} aria-hidden="true" />
                            Tornar padrão
                          </button>
                        )}
                        <button
                          type="button"
                          onClick={() => handleDelete(a)}
                          disabled={busyId !== null}
                          className="inline-flex items-center gap-1.5 text-slate-600 hover:text-red-600"
                        >
                          <Trash2 size={15} aria-hidden="true" />
                          Remover
                        </button>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </div>
            <div className="border-t border-slate-100 px-5 pt-4 pb-[calc(1.25rem+env(safe-area-inset-bottom))]">
              <button
                type="button"
                onClick={() => openForm()}
                disabled={addresses === null || addresses.length >= 10}
                className={storePrimaryButton}
              >
                <Plus size={18} aria-hidden="true" />
                Adicionar endereço
              </button>
            </div>
          </>
        )}
      </aside>
    </div>
  );
}
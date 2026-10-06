"use client";

import { useEffect, useRef, useState, type ChangeEvent, type FormEvent } from "react";
import { useBackdropDismiss } from "@/lib/backdrop";
import {
  Building2,
  CircleAlert,
  Eye,
  ImagePlus,
  LoaderCircle,
  Plus,
  Store as StoreIcon,
  Trash2,
  X,
} from "lucide-react";
import { createStore, unwrap, type Store, type CreateStorePayload } from "@/lib/api";
import { formatCnpj, isValidCnpj } from "@/lib/validators";
import { uploadImage } from "@/lib/upload";
import StorePreview from "../StorePreview/StorePreview";
import { primaryButtonClass } from "./StoreCard";

interface CreateStoreModalProps {
  onClose: () => void;
  onCreated: (store: Store) => void;
}

const DEFAULT_PRIMARY = "#2563eb";
const DEFAULT_SECONDARY = "#1d4ed8";
const DEFAULT_TERTIARY = "#111827"; // usada no cabeçalho da vitrine (fundo escuro)

/* ===========================
   CLASSES REUTILIZADAS
=========================== */

const labelClass = "text-label-md font-semibold text-on-surface";

const fieldClass =
  "w-full rounded-lg border border-slate-300 bg-white px-3 text-body-lg text-on-surface outline-none transition placeholder:text-slate-400 focus:border-primary-container focus:ring-[3px] focus:ring-primary-container/15";

const secondaryButtonClass =
  "inline-flex h-11 items-center justify-center rounded-lg border border-slate-200 bg-white px-5 text-title-md text-on-surface shadow-[0_1px_2px_rgba(15,23,42,0.04)] transition-colors hover:border-slate-300 hover:bg-surface disabled:cursor-not-allowed disabled:opacity-60";

/* ===========================
   CAMPO DE COR
=========================== */

function ColorField({
  id,
  label,
  hint,
  value,
  onChange,
}: {
  id: string;
  label: string;
  hint: string;
  value: string;
  onChange: (value: string) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className={labelClass}>
        {label}
      </label>
      <div className="flex h-11 items-center gap-2 rounded-lg border border-slate-300 bg-white px-1.5 transition focus-within:border-primary-container focus-within:ring-[3px] focus-within:ring-primary-container/15">
        <input
          id={id}
          type="color"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="h-8 w-9 shrink-0 cursor-pointer rounded-md border-none bg-transparent p-0 [&::-moz-color-swatch]:rounded-md [&::-moz-color-swatch]:border-none [&::-webkit-color-swatch]:rounded-md [&::-webkit-color-swatch]:border-none [&::-webkit-color-swatch-wrapper]:p-0"
        />
        <span className="font-mono text-code-sm text-on-surface-variant uppercase">{value}</span>
      </div>
      <span className="text-body-sm text-outline">{hint}</span>
    </div>
  );
}

/* ===========================
   MODAL
=========================== */

export default function CreateStoreModal({ onClose, onCreated }: CreateStoreModalProps) {
  const [name, setName] = useState("");
  const [cnpj, setCnpj] = useState("");
  const [description, setDescription] = useState("");
  const [logoUrl, setLogoUrl] = useState("");
  const [uploading, setUploading] = useState(false);
  const [primaryColor, setPrimaryColor] = useState(DEFAULT_PRIMARY);
  const [secondaryColor, setSecondaryColor] = useState(DEFAULT_SECONDARY);
  const [tertiaryColor, setTertiaryColor] = useState(DEFAULT_TERTIARY);

  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const busy = loading || uploading;

  // Mesmas props nas duas posições da prévia (lateral no desktop, abaixo das cores no celular).
  const previewProps = {
    name,
    description,
    logoUrl,
    primaryColor,
    secondaryColor,
    tertiaryColor,
  };

  // Não deixa fechar no meio de um envio (evita perder o que está sendo salvo).
  function handleClose() {
    if (!busy) onClose();
  }

  // Fecha clicando fora, mas não quando o mouse só termina fora (ex.: selecionando o texto de um campo).
  const backdrop = useBackdropDismiss(handleClose);

  // Fecha com Esc e trava o scroll da página enquanto o modal está aberto.
  useEffect(() => {
    function onKeyDown(e: KeyboardEvent) {
      if (e.key === "Escape") handleClose();
    }

    const previousOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    window.addEventListener("keydown", onKeyDown);

    return () => {
      document.body.style.overflow = previousOverflow;
      window.removeEventListener("keydown", onKeyDown);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [busy]);

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (!name.trim()) {
      setError("Informe o nome da loja.");
      return;
    }

    // CNPJ é opcional (MEI pode usar só o CPF do dono), mas se preenchido
    // precisa ser válido.
    if (cnpj.trim() && !isValidCnpj(cnpj)) {
      setError("CNPJ inválido. Confira os números digitados.");
      return;
    }

    setLoading(true);

    try {
      const payload: CreateStorePayload = {
        name: name.trim(),
        cnpj: cnpj.trim() || undefined,
        description: description.trim() || undefined,
        logoUrl: logoUrl.trim() || undefined,
        primaryColor,
        secondaryColor,
        tertiaryColor,
      };

      // Antes, quando o backend recusava (nome/CNPJ repetido), o modal
      // simplesmente não fazia nada. unwrap() transforma isso em erro visível.
      onCreated(await unwrap(createStore(payload)));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao criar loja.");
    } finally {
      setLoading(false);
    }
  }

  async function handleLogoChange(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    setError(null);
    setUploading(true);

    try {
      const url = await uploadImage(file);
      setLogoUrl(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao enviar a imagem.");
    } finally {
      setUploading(false);
    }
  }

  function handleRemoveLogo() {
    setLogoUrl("");
    // Limpa o input para permitir escolher o mesmo arquivo de novo.
    if (fileInputRef.current) fileInputRef.current.value = "";
  }

  return (
    <div
      className="fixed inset-0 z-[1000] flex animate-overlay-in items-center justify-center bg-slate-900/40 p-4 backdrop-blur-sm"
      {...backdrop}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="create-store-title"
        className="max-h-[90vh] w-full max-w-[520px] animate-modal-in overflow-y-auto rounded-2xl border border-slate-200/70 bg-white p-6 shadow-[0_25px_50px_-12px_rgba(15,23,42,0.25)] sm:p-8 md:max-w-[880px]"
      >
        {/* Cabeçalho */}
        <div className="mb-2 flex items-start justify-between gap-4">
          <div className="flex items-center gap-3.5">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-primary-fixed/50 text-primary-container">
              <StoreIcon size={20} aria-hidden="true" />
            </div>
            <h2 id="create-store-title" className="text-headline-md text-on-surface">
              Nova loja
            </h2>
          </div>

          <button
            type="button"
            onClick={handleClose}
            disabled={busy}
            aria-label="Fechar"
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container-low hover:text-on-surface disabled:cursor-not-allowed disabled:opacity-60"
          >
            <X size={20} aria-hidden="true" />
          </button>
        </div>

        <p className="mb-6 text-body-md text-on-surface-variant">
          Preencha os dados abaixo para criar sua loja. Você pode ajustar tudo isso depois.
        </p>

        <div className="grid grid-cols-1 items-start gap-8 md:grid-cols-[minmax(0,1fr)_280px]">
          <form onSubmit={handleSubmit} className="flex flex-col gap-5" noValidate>
            {/* Nome */}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="storeName" className={labelClass}>
                Nome da loja <span className="text-red-600">*</span>
              </label>
              <div className="group relative">
                <StoreIcon
                  size={18}
                  aria-hidden="true"
                  className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-outline transition-colors group-focus-within:text-primary-container"
                />
                <input
                  id="storeName"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  placeholder="Ex: Loja da Maria"
                  required
                  autoFocus
                  className={`${fieldClass} h-11 pl-10`}
                />
              </div>
            </div>

            {/* CNPJ */}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="storeCnpj" className={labelClass}>
                CNPJ <span className="font-normal text-outline">(opcional)</span>
              </label>
              <div className="group relative">
                <Building2
                  size={18}
                  aria-hidden="true"
                  className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-outline transition-colors group-focus-within:text-primary-container"
                />
                <input
                  id="storeCnpj"
                  value={cnpj}
                  onChange={(e) => setCnpj(formatCnpj(e.target.value))}
                  placeholder="00.000.000/0000-00"
                  inputMode="numeric"
                  maxLength={18}
                  className={`${fieldClass} h-11 pl-10`}
                />
              </div>
            </div>

            {/* Descrição */}
            <div className="flex flex-col gap-1.5">
              <label htmlFor="storeDescription" className={labelClass}>
                Descrição
              </label>
              <textarea
                id="storeDescription"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Conte um pouco sobre sua loja"
                rows={3}
                className={`${fieldClass} resize-y py-2.5`}
              />
            </div>

            {/* Logo */}
            <div className="flex flex-col gap-1.5">
              <span className={labelClass}>Logo da loja</span>

              <input
                ref={fileInputRef}
                id="storeLogo"
                type="file"
                accept="image/*"
                onChange={handleLogoChange}
                disabled={uploading}
                className="sr-only"
              />

              {logoUrl && !uploading ? (
                <div className="flex items-center gap-4 rounded-xl border border-slate-200 bg-surface p-3">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={logoUrl}
                    alt="Preview do logo"
                    className="h-16 w-16 shrink-0 rounded-lg border border-slate-200 bg-white object-contain"
                  />
                  <div className="flex flex-1 flex-wrap gap-2">
                    <label
                      htmlFor="storeLogo"
                      className="inline-flex h-9 cursor-pointer items-center gap-1.5 rounded-lg border border-slate-200 bg-white px-3 text-label-md text-on-surface transition-colors hover:border-slate-300 hover:bg-surface"
                    >
                      <ImagePlus size={16} aria-hidden="true" />
                      Trocar
                    </label>
                    <button
                      type="button"
                      onClick={handleRemoveLogo}
                      className="inline-flex h-9 items-center gap-1.5 rounded-lg px-3 text-label-md text-red-700 transition-colors hover:bg-red-50"
                    >
                      <Trash2 size={16} aria-hidden="true" />
                      Remover
                    </button>
                  </div>
                </div>
              ) : (
                <label
                  htmlFor="storeLogo"
                  className={`flex flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-slate-300 bg-surface px-4 py-6 text-center transition-colors ${
                    uploading
                      ? "cursor-wait"
                      : "cursor-pointer hover:border-primary-container/50 hover:bg-surface-container-low"
                  }`}
                >
                  {uploading ? (
                    <>
                      <LoaderCircle
                        size={24}
                        aria-hidden="true"
                        className="animate-spin text-primary-container"
                      />
                      <span className="text-body-md text-on-surface-variant">Enviando imagem...</span>
                    </>
                  ) : (
                    <>
                      <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary-fixed/50 text-primary-container">
                        <ImagePlus size={20} aria-hidden="true" />
                      </span>
                      <span className="text-body-md text-on-surface">
                        <span className="font-semibold text-primary">Clique para enviar</span> uma
                        imagem
                      </span>
                      <span className="text-body-sm text-outline">PNG, JPG ou SVG</span>
                    </>
                  )}
                </label>
              )}
            </div>

            {/* Cores */}
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
              <ColorField
                id="primaryColor"
                label="Cor primária"
                hint="Botões e destaques"
                value={primaryColor}
                onChange={setPrimaryColor}
              />
              <ColorField
                id="secondaryColor"
                label="Cor secundária"
                hint="Hover e detalhes"
                value={secondaryColor}
                onChange={setSecondaryColor}
              />
              <ColorField
                id="tertiaryColor"
                label="Cor terciária"
                hint="Cabeçalho da vitrine"
                value={tertiaryColor}
                onChange={setTertiaryColor}
              />
            </div>

            {/* Prévia no celular: logo abaixo das cores, onde o efeito é visto na hora */}
            <div className="flex flex-col gap-2 md:hidden">
              <PreviewLabel />
              <StorePreview {...previewProps} />
            </div>

            {error && (
              <div
                role="alert"
                className="flex items-start gap-2 rounded-lg border border-red-200 bg-red-50 px-3 py-2.5 text-body-md text-red-700"
              >
                <CircleAlert size={18} aria-hidden="true" className="mt-px shrink-0" />
                <span>{error}</span>
              </div>
            )}

            {/* Ações */}
            <div className="mt-1 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={handleClose}
                disabled={busy}
                className={secondaryButtonClass}
              >
                Cancelar
              </button>

              <button
                type="submit"
                disabled={busy}
                className={`${primaryButtonClass} disabled:cursor-not-allowed disabled:opacity-60`}
              >
                {loading ? (
                  <>
                    <LoaderCircle size={18} aria-hidden="true" className="animate-spin" />
                    Criando...
                  </>
                ) : (
                  <>
                    <Plus size={18} aria-hidden="true" />
                    Criar loja
                  </>
                )}
              </button>
            </div>
          </form>

          {/* Prévia no desktop: coluna lateral que acompanha a rolagem do modal */}
          <aside className="sticky top-0 hidden flex-col gap-2 md:flex">
            <PreviewLabel />
            <StorePreview {...previewProps} />
            <p className="text-body-sm text-outline">A vitrine atualiza enquanto você preenche os campos.</p>
          </aside>
        </div>
      </div>
    </div>
  );
}

function PreviewLabel() {
  return (
    <span className="flex items-center gap-1.5 text-label-md font-semibold text-on-surface-variant">
      <Eye size={15} aria-hidden="true" />
      Pré-visualização
    </span>
  );
}
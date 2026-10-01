"use client";

import { useRef, useState, type ChangeEvent, type FormEvent } from "react";
import {
  ImagePlus,
  LoaderCircle,
  MessageCircle,
  Palette,
  Save,
  Store as StoreIcon,
  Trash2,
} from "lucide-react";
import { unwrap, updateStore } from "@/lib/api";
import { uploadImage } from "@/lib/upload";
import { formatPhone, isValidPhone } from "@/lib/validators";
import StorePreview from "../../../../../../components/InitialPage/StorePreview/StorePreview";
import { useShopkeeperStore } from "../../components/ShopkeeperStoreContext";
import {
  ErrorBox,
  PageHeader,
  SuccessBox,
  btnPrimary,
  btnSecondary,
  card,
  hint,
  input,
  label,
} from "../../components/Ui";

export default function PersonalizacaoPage() {
  const { store, setStore } = useShopkeeperStore();

  const [description, setDescription] = useState(store.description ?? "");
  const [phone, setPhone] = useState(formatPhone(store.phone ?? ""));
  const [logoUrl, setLogoUrl] = useState(store.logoUrl ?? "");
  const [primaryColor, setPrimaryColor] = useState(store.primaryColor);
  const [secondaryColor, setSecondaryColor] = useState(store.secondaryColor);
  const [tertiaryColor, setTertiaryColor] = useState(store.tertiaryColor);

  const [uploading, setUploading] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  const fileInputRef = useRef<HTMLInputElement>(null);

  // Qualquer edição esconde o "salvo", para não parecer que a mudança nova já foi salva.
  function edit<T>(setter: (v: T) => void) {
    return (value: T) => {
      setter(value);
      setSuccess(null);
    };
  }

  async function handleLogo(e: ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    e.target.value = "";
    if (!file) return;

    setError(null);
    setSuccess(null);
    setUploading(true);
    try {
      setLogoUrl(await uploadImage(file));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao enviar a imagem.");
    } finally {
      setUploading(false);
    }
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setSuccess(null);

    if (phone.trim() && !isValidPhone(phone.trim())) {
      setError("Telefone inválido. Use o formato (00) 00000-0000.");
      return;
    }

    setSaving(true);
    try {
      const updated = await unwrap(
        updateStore(store.id, {
          description: description.trim(),
          phone: phone.replace(/\D/g, ""),
          logoUrl,
          primaryColor,
          secondaryColor,
          tertiaryColor,
        })
      );
      setStore(updated);
      setSuccess("Personalização salva!");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao salvar.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <PageHeader title="Personalização" subtitle="Como sua vitrine aparece para os clientes." />

      <div className="grid grid-cols-1 items-start gap-6 min-[1101px]:grid-cols-[minmax(0,1fr)_340px]">
        {/* FORMULÁRIO */}
        <form onSubmit={handleSubmit} className={`${card} flex flex-col gap-6`} noValidate>
          {/* Logo */}
          <div className="flex flex-col gap-2">
            <span className={label}>Logo</span>

            <input
              ref={fileInputRef}
              id="logo-input"
              type="file"
              accept="image/jpeg,image/png,image/webp"
              onChange={handleLogo}
              disabled={uploading}
              className="sr-only"
            />

            <div className="flex items-center gap-4">
              <div className="flex h-[72px] w-[72px] shrink-0 items-center justify-center overflow-hidden rounded-xl border border-slate-200 bg-surface text-outline">
                {uploading ? (
                  <LoaderCircle size={22} aria-hidden="true" className="animate-spin text-primary-container" />
                ) : logoUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img src={logoUrl} alt="Logo da loja" className="h-full w-full object-cover" />
                ) : (
                  <StoreIcon size={26} aria-hidden="true" />
                )}
              </div>

              <div className="flex flex-col gap-2">
                <div className="flex flex-wrap gap-2">
                  <label
                    htmlFor="logo-input"
                    className={`${btnSecondary} h-9 ${uploading ? "pointer-events-none opacity-60" : "cursor-pointer"}`}
                  >
                    <ImagePlus size={16} aria-hidden="true" />
                    {logoUrl ? "Trocar logo" : "Enviar logo"}
                  </label>
                  {logoUrl && !uploading && (
                    <button
                      type="button"
                      onClick={() => {
                        setLogoUrl("");
                        setSuccess(null);
                      }}
                      className="inline-flex h-9 items-center gap-1.5 rounded-lg px-3 text-label-md font-semibold text-red-600 transition-colors hover:bg-red-50"
                    >
                      <Trash2 size={16} aria-hidden="true" />
                      Remover
                    </button>
                  )}
                </div>
                <span className={hint}>
                  {uploading ? "Enviando imagem..." : "JPG, PNG ou WEBP. De preferência quadrada."}
                </span>
              </div>
            </div>
          </div>

          {/* Descrição */}
          <div className="flex flex-col gap-1.5">
            <div className="flex items-baseline justify-between gap-2">
              <label htmlFor="desc" className={label}>
                Descrição
              </label>
              <span className="text-body-sm text-outline tabular-nums">{description.length}/500</span>
            </div>
            <textarea
              id="desc"
              rows={3}
              maxLength={500}
              value={description}
              onChange={(e) => edit(setDescription)(e.target.value)}
              placeholder="Conte em poucas palavras o que sua loja vende"
              className={`${input} resize-y`}
            />
          </div>

          {/* WhatsApp */}
          <div className="flex flex-col gap-1.5">
            <label htmlFor="phone" className={label}>
              WhatsApp da loja
            </label>
            <div className="group relative">
              <MessageCircle
                size={16}
                aria-hidden="true"
                className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-outline transition-colors group-focus-within:text-primary-container"
              />
              <input
                id="phone"
                type="tel"
                value={phone}
                onChange={(e) => edit(setPhone)(formatPhone(e.target.value))}
                placeholder="(00) 00000-0000"
                inputMode="numeric"
                maxLength={15}
                aria-describedby="phone-hint"
                className={`${input} pl-9`}
              />
            </div>
            <span id="phone-hint" className={hint}>
              Aparece na vitrine e depois do pedido, pro cliente falar com você.
            </span>
          </div>

          {/* Cores */}
          <fieldset className="flex flex-col gap-3">
            <legend className={`${label} mb-3 flex items-center gap-1.5`}>
              <Palette size={16} aria-hidden="true" className="text-outline" />
              Cores
            </legend>
            <div className="grid grid-cols-1 gap-3 min-[577px]:grid-cols-3">
              <ColorField
                id="primaryColor"
                label="Principal"
                hint="Botões e preços"
                value={primaryColor}
                onChange={edit(setPrimaryColor)}
              />
              <ColorField
                id="secondaryColor"
                label="Secundária"
                hint="Gradiente e hover"
                value={secondaryColor}
                onChange={edit(setSecondaryColor)}
              />
              <ColorField
                id="tertiaryColor"
                label="Cabeçalho"
                hint="Fundo do topo"
                value={tertiaryColor}
                onChange={edit(setTertiaryColor)}
              />
            </div>
          </fieldset>

          {error && <ErrorBox>{error}</ErrorBox>}
          {success && <SuccessBox>{success}</SuccessBox>}

          <div className="flex justify-end border-t border-slate-100 pt-5">
            <button type="submit" disabled={saving || uploading} className={btnPrimary}>
              {saving ? (
                <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
              ) : (
                <Save size={16} aria-hidden="true" />
              )}
              {saving ? "Salvando..." : "Salvar personalização"}
            </button>
          </div>
        </form>

        {/* PRÉ-VISUALIZAÇÃO — usa as mesmas variáveis da vitrine */}
        <aside className="flex max-w-[380px] flex-col gap-2 min-[1101px]:sticky min-[1101px]:top-8 min-[1101px]:max-w-none">
          <span className="text-label-md font-semibold text-on-surface-variant">Pré-visualização</span>

          <StorePreview
            name={store.name}
            description={description}
            logoUrl={logoUrl}
            primaryColor={primaryColor}
            secondaryColor={secondaryColor}
            tertiaryColor={tertiaryColor}
          />
        </aside>
      </div>
    </>
  );
}

function ColorField({
  id,
  label: fieldLabel,
  hint: fieldHint,
  value,
  onChange,
}: {
  id: string;
  label: string;
  hint: string;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-body-sm font-semibold text-on-surface">
        {fieldLabel}
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
      <span className="text-body-sm text-outline">{fieldHint}</span>
    </div>
  );
}
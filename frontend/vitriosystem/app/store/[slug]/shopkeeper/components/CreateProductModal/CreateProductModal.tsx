"use client";

import { useEffect, useMemo, useState, type ChangeEvent, type FormEvent, type ReactNode } from "react";
import {
  Eye,
  ImagePlus,
  LoaderCircle,
  Package,
  Plus,
  Ruler,
  Save,
  Star,
  Trash2,
  X,
} from "lucide-react";
import { unwrap } from "@/lib/api";
import {
  createProduct,
  getProductsByStore,
  updateProduct,
  type Product,
  type UpdateProductPayload,
} from "@/lib/api_product";
import { getCategoriesByStore, type Category } from "@/lib/api_category";
import { uploadImage } from "@/lib/upload";
import { SIZE_GRIDS, detectSizeGrid, type SizeGrid } from "@/lib/sizes";
import { ErrorBox, btnPrimary, btnSecondary, hint, input, label } from "../Ui";

const MAX_IMAGES = 8;

interface CreateProductModalProps {
  storeId: number;
  /** Se vier, o modal abre em modo edição. */
  product?: Product;
  onClose: () => void;
  onSaved: (product: Product) => void;
}

interface PendingImage {
  key: string; // chave estável (não usar índice: a lista muda durante o upload)
  url: string;
  uploading: boolean;
}

function slugify(text: string): string {
  return text
    .toLowerCase()
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^a-z0-9\s-]/g, "")
    .trim()
    .replace(/[\s-]+/g, "-");
}

// "12,50" / "12.50" / "1.234,56" -> 12.5 / 12.5 / 1234.56
function parseMoney(value: string): number {
  const clean = value.trim().replace(/\s|R\$/g, "");
  if (!clean) return NaN;
  const normalized = clean.includes(",") ? clean.replace(/\./g, "").replace(",", ".") : clean;
  return Number(normalized);
}

function moneyToInput(value: number | null | undefined) {
  return value == null ? "" : value.toFixed(2).replace(".", ",");
}

// Atalhos de cor: um clique preenche nome e bolinha.
const COMMON_COLORS: [string, string][] = [
  ["Preto", "#111111"],
  ["Branco", "#FFFFFF"],
  ["Cinza", "#9CA3AF"],
  ["Azul marinho", "#1E3A8A"],
  ["Azul", "#2563EB"],
  ["Vermelho", "#DC2626"],
  ["Verde", "#16A34A"],
  ["Bege", "#D6C3A3"],
  ["Rosa", "#EC4899"],
];

let keySeq = 0;
const newKey = () => `img-${Date.now()}-${keySeq++}`;

export default function CreateProductModal({ storeId, product, onClose, onSaved }: CreateProductModalProps) {
  const isEdit = !!product;

  const [name, setName] = useState(product?.name ?? "");
  const [slug, setSlug] = useState(product?.slug ?? "");
  const [slugTouched, setSlugTouched] = useState(isEdit);
  const [description, setDescription] = useState(product?.description ?? "");
  const [sku, setSku] = useState(product?.sku ?? "");
  const [price, setPrice] = useState(moneyToInput(product?.price));
  const [promotionalPrice, setPromotionalPrice] = useState(moneyToInput(product?.promotionalPrice));
  const [stockQuantity, setStockQuantity] = useState(String(product?.stockQuantity ?? 0));
  const [isActive, setIsActive] = useState(product?.isActive ?? true);
  const [isFeatured, setIsFeatured] = useState(product?.isFeatured ?? false);
  const [categoryId, setCategoryId] = useState<string>(product?.categoryId ? String(product.categoryId) : "");
  // Tamanhos: grade escolhida + estoque digitado em cada tamanho.
  // Campo vazio = a loja não vende esse tamanho; "0" = vende, mas está esgotado.
  const [sizeGrid, setSizeGrid] = useState<SizeGrid>(() =>
    detectSizeGrid((product?.variants ?? []).map((v) => v.size))
  );
  const [sizeStock, setSizeStock] = useState<Record<string, string>>(() =>
    Object.fromEntries((product?.variants ?? []).map((v) => [v.size, String(v.stockQuantity)]))
  );
  // Cor: cada cor de uma peça é um produto próprio, ligado aos outros pelo grupo.
  const [colorName, setColorName] = useState(product?.colorName ?? "");
  const [colorHex, setColorHex] = useState(product?.colorHex ?? "");
  const [linkedProductId, setLinkedProductId] = useState<string>("");
  const [storeProducts, setStoreProducts] = useState<Product[]>([]);
  const [images, setImages] = useState<PendingImage[]>(
    (product?.images ?? []).map((img) => ({ key: newKey(), url: img.url, uploading: false }))
  );

  const [categories, setCategories] = useState<Category[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const gridSizes = sizeGrid === "none" ? [] : SIZE_GRIDS[sizeGrid].sizes;
  const offeredSizes = gridSizes.filter((size) => (sizeStock[size] ?? "").trim() !== "");
  const sizesTotal = offeredSizes.reduce((sum, size) => sum + (Number(sizeStock[size]) || 0), 0);

  const isUploading = images.some((img) => img.uploading);
  const canAddImages = images.length < MAX_IMAGES;

  useEffect(() => {
    let active = true;
    getCategoriesByStore(storeId)
      .then(({ dados }) => active && setCategories(dados ?? []))
      .catch(() => active && setCategories([]));
    return () => {
      active = false;
    };
  }, [storeId]);

  // Produtos da loja para o campo "Mesma peça que". Busca sempre a lista atual,
  // porque ligar uma cor também muda o grupo do outro produto.
  useEffect(() => {
    let active = true;
    getProductsByStore(storeId)
      .then(({ dados }) => {
        if (!active) return;
        const list = dados ?? [];
        setStoreProducts(list);
        if (product) {
          const current = list.find((p) => p.id === product.id);
          const groupId = current?.colorGroupId ?? product.colorGroupId;
          const sibling = groupId ? list.find((p) => p.id !== product.id && p.colorGroupId === groupId) : undefined;
          if (sibling) setLinkedProductId(String(sibling.id));
        }
      })
      .catch(() => active && setStoreProducts([]));
    return () => {
      active = false;
    };
  }, [storeId, product]);

  // Opções do "Mesma peça que": cada grupo de cores aparece UMA vez só (basta ligar
  // a qualquer produto do grupo, o backend coloca este produto no grupo inteiro).
  // O representante do grupo é o primeiro produto dele na lista, o mesmo que a
  // pré-seleção acima escolhe ao editar, então o select já abre marcado.
  const linkOptions = useMemo(() => {
    const groups: { id: number; label: string }[] = [];
    const singles: { id: number; label: string }[] = [];
    const seenGroups = new Map<string, { id: number; name: string; colors: string[] }>();

    for (const p of storeProducts) {
      if (p.id === product?.id) continue;

      if (!p.colorGroupId) {
        singles.push({ id: p.id, label: p.colorName ? `${p.name} (${p.colorName})` : p.name });
        continue;
      }

      const group = seenGroups.get(p.colorGroupId);
      if (group) {
        if (p.colorName) group.colors.push(p.colorName);
      } else {
        seenGroups.set(p.colorGroupId, { id: p.id, name: p.name, colors: p.colorName ? [p.colorName] : [] });
      }
    }

    for (const g of seenGroups.values()) {
      const shown = g.colors.slice(0, 4).join(", ");
      const extra = g.colors.length > 4 ? ` +${g.colors.length - 4}` : "";
      groups.push({ id: g.id, label: g.colors.length ? `${g.name} (${shown}${extra})` : g.name });
    }

    const byLabel = (a: { label: string }, b: { label: string }) => a.label.localeCompare(b.label, "pt-BR");
    return { groups: groups.sort(byLabel), singles: singles.sort(byLabel) };
  }, [storeProducts, product?.id]);

  // Fecha com Esc
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && !loading && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose, loading]);

  // Trava o scroll da página enquanto o modal está aberto
  useEffect(() => {
    const previous = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.body.style.overflow = previous;
    };
  }, []);

  function handleNameChange(value: string) {
    setName(value);
    if (!slugTouched) setSlug(slugify(value));
  }

  async function handleImagesSelected(e: ChangeEvent<HTMLInputElement>) {
    const inputEl = e.target;
    const files = Array.from(inputEl.files ?? []);
    inputEl.value = "";
    if (files.length === 0) return;

    const room = MAX_IMAGES - images.length;
    if (room <= 0) {
      setError(`Máximo de ${MAX_IMAGES} imagens por produto.`);
      return;
    }

    const selected = files.slice(0, room);
    if (files.length > room) setError(`Só ${room} imagem(ns) foram adicionadas (limite de ${MAX_IMAGES}).`);

    const placeholders = selected.map(() => ({ key: newKey(), url: "", uploading: true }));
    setImages((prev) => [...prev, ...placeholders]);

    // Uploads em paralelo; cada um atualiza o próprio placeholder pela key.
    await Promise.all(
      selected.map(async (file, i) => {
        const key = placeholders[i].key;
        try {
          const url = await uploadImage(file);
          setImages((prev) => prev.map((img) => (img.key === key ? { ...img, url, uploading: false } : img)));
        } catch (err) {
          setImages((prev) => prev.filter((img) => img.key !== key));
          setError(err instanceof Error ? err.message : "Falha ao enviar uma das imagens.");
        }
      })
    );
  }

  function changeSizeGrid(grid: SizeGrid) {
    setSizeGrid(grid);
    // Ao trocar de grade, os tamanhos da grade anterior deixam de valer.
    const allowed = new Set(grid === "none" ? [] : SIZE_GRIDS[grid].sizes);
    setSizeStock((prev) => Object.fromEntries(Object.entries(prev).filter(([size]) => allowed.has(size))));
  }

  function setStockForSize(size: string, value: string) {
    // Só dígitos: estoque é inteiro
    setSizeStock((prev) => ({ ...prev, [size]: value.replace(/\D/g, "").slice(0, 5) }));
  }

  function removeImage(key: string) {
    setImages((prev) => prev.filter((img) => img.key !== key));
  }

  function makeCover(key: string) {
    setImages((prev) => {
      const target = prev.find((img) => img.key === key);
      return target ? [target, ...prev.filter((img) => img.key !== key)] : prev;
    });
  }

  async function handleSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);

    if (!name.trim()) {
      setError("Informe o nome do produto.");
      return;
    }

    const priceValue = parseMoney(price);
    if (Number.isNaN(priceValue) || priceValue <= 0) {
      setError("Informe um preço válido.");
      return;
    }

    const promoValue = promotionalPrice.trim() ? parseMoney(promotionalPrice) : undefined;
    if (promoValue !== undefined && (Number.isNaN(promoValue) || promoValue <= 0 || promoValue >= priceValue)) {
      setError("O preço promocional precisa ser menor que o preço normal.");
      return;
    }

    const stockValue = Number(stockQuantity);
    if (sizeGrid === "none" && (!Number.isInteger(stockValue) || stockValue < 0)) {
      setError("O estoque precisa ser um número inteiro maior ou igual a zero.");
      return;
    }

    if (sizeGrid !== "none" && offeredSizes.length === 0) {
      setError("Informe o estoque de pelo menos um tamanho, ou escolha \"Sem tamanho\".");
      return;
    }

    // A ordem da grade vira a ordem de exibição na vitrine.
    const variants = offeredSizes.map((size) => ({ size, stockQuantity: Number(sizeStock[size]) }));

    if (linkedProductId && !colorName.trim()) {
      setError("Informe o nome da cor deste produto para ligá-lo às outras cores.");
      return;
    }

    if (isUploading) {
      setError("Aguarde o envio das imagens terminar.");
      return;
    }

    const payload: UpdateProductPayload = {
      categoryId: categoryId ? Number(categoryId) : undefined,
      name: name.trim(),
      slug: slug.trim() || slugify(name),
      description: description.trim() || undefined,
      sku: sku.trim() || undefined,
      price: priceValue,
      promotionalPrice: promoValue,
      // Com tamanhos, o backend recalcula o total como a soma deles.
      stockQuantity: sizeGrid === "none" ? stockValue : sizesTotal,
      isActive,
      isFeatured,
      images: images.map((img, index) => ({ url: img.url, order: index })),
      variants, // vazio remove os tamanhos do produto
      colorName: colorName.trim() || undefined,
      colorHex: colorHex || undefined,
      // Ausente = o produto sai do grupo de cores
      colorLinkedProductId: linkedProductId ? Number(linkedProductId) : undefined,
    };

    setLoading(true);
    try {
      const saved = product
        ? await unwrap(updateProduct(product.id, payload))
        : await unwrap(createProduct({ ...payload, storeId }));
      onSaved(saved);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao salvar produto.");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div
      className="fixed inset-0 z-[1000] flex animate-overlay-in items-center justify-center bg-slate-900/40 p-4 backdrop-blur-sm"
      onClick={() => !loading && onClose()}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="product-modal-title"
        onClick={(e) => e.stopPropagation()}
        className="flex max-h-[92vh] w-full max-w-[600px] animate-modal-in flex-col overflow-hidden rounded-2xl bg-white shadow-[0_25px_50px_-12px_rgba(15,23,42,0.25)]"
      >
        {/* Cabeçalho */}
        <div className="flex items-start justify-between gap-4 border-b border-slate-100 px-6 pt-6 pb-4">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary-fixed/50 text-primary-container">
              <Package size={20} aria-hidden="true" />
            </div>
            <div>
              <h2 id="product-modal-title" className="text-headline-sm text-on-surface">
                {isEdit ? "Editar produto" : "Novo produto"}
              </h2>
              <p className="text-body-sm text-on-surface-variant">
                {isEdit ? "Altere os dados e salve." : "Você pode ajustar tudo isso depois."}
              </p>
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={loading}
            aria-label="Fechar"
            className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg text-on-surface-variant transition-colors hover:bg-surface-container-low hover:text-on-surface disabled:opacity-60"
          >
            <X size={20} aria-hidden="true" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col" noValidate>
          {/* Corpo com rolagem */}
          <div className="flex flex-col gap-7 overflow-y-auto px-6 py-5">
            {/* Informações */}
            <Section title="Informações">
              <Field id="productName" label="Nome do produto" required>
                <input
                  id="productName"
                  value={name}
                  onChange={(e) => handleNameChange(e.target.value)}
                  placeholder="Ex: Camiseta Básica"
                  maxLength={150}
                  required
                  autoFocus={!isEdit}
                  className={input}
                />
              </Field>

              <Field id="productSlug" label="Slug (URL)" hint="Gerado a partir do nome. Só letras, números e hífens.">
                <input
                  id="productSlug"
                  value={slug}
                  onChange={(e) => {
                    setSlugTouched(true);
                    setSlug(slugify(e.target.value));
                  }}
                  placeholder="camiseta-basica"
                  className={`${input} font-mono text-code-sm`}
                />
              </Field>

              <Field id="productDescription" label="Descrição">
                <textarea
                  id="productDescription"
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Detalhes do produto"
                  rows={3}
                  className={`${input} resize-y`}
                />
              </Field>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
                <Field id="productCategory" label="Categoria">
                  <select
                    id="productCategory"
                    value={categoryId}
                    onChange={(e) => setCategoryId(e.target.value)}
                    className={input}
                  >
                    <option value="">Sem categoria</option>
                    {categories.map((cat) => (
                      <option key={cat.id} value={cat.id}>
                        {cat.name}
                        {!cat.isActive ? " (inativa)" : ""}
                      </option>
                    ))}
                  </select>
                </Field>

                <Field id="productSku" label="SKU">
                  <input
                    id="productSku"
                    value={sku}
                    onChange={(e) => setSku(e.target.value)}
                    placeholder="Ex: CAM-001"
                    className={`${input} font-mono text-code-sm`}
                  />
                </Field>
              </div>
            </Section>

            {/* Preço e estoque */}
            <Section title="Preço e estoque">
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
                <Field id="productPrice" label="Preço" required>
                  <MoneyInput
                    id="productPrice"
                    value={price}
                    onChange={setPrice}
                    required
                  />
                </Field>
                <Field id="productPromoPrice" label="Promocional">
                  <MoneyInput id="productPromoPrice" value={promotionalPrice} onChange={setPromotionalPrice} />
                </Field>
                <Field
                  id="productStock"
                  label="Estoque"
                  hint={sizeGrid !== "none" ? "Soma dos tamanhos." : undefined}
                >
                  <input
                    id="productStock"
                    type="number"
                    min={0}
                    step={1}
                    value={sizeGrid === "none" ? stockQuantity : String(sizesTotal)}
                    onChange={(e) => setStockQuantity(e.target.value)}
                    readOnly={sizeGrid !== "none"}
                    className={`${input} tabular-nums read-only:bg-slate-50 read-only:text-on-surface-variant`}
                  />
                </Field>
              </div>
            </Section>

            {/* Tamanhos */}
            <Section title="Tamanhos">
              <div role="radiogroup" aria-label="Grade de tamanhos" className="grid grid-cols-1 gap-2 sm:grid-cols-3">
                {(
                  [
                    ["none", "Sem tamanho"],
                    ["clothing", SIZE_GRIDS.clothing.label],
                    ["numeric", SIZE_GRIDS.numeric.label],
                  ] as [SizeGrid, string][]
                ).map(([value, optionLabel]) => (
                  <label
                    key={value}
                    className={`flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-2.5 text-body-md transition-colors has-[:focus-visible]:ring-[3px] has-[:focus-visible]:ring-primary-container/25 ${
                      sizeGrid === value
                        ? "border-primary-container bg-primary-container/5 font-semibold text-primary-container"
                        : "border-slate-200 text-on-surface hover:bg-surface-container-low"
                    }`}
                  >
                    <input
                      type="radio"
                      name="sizeGrid"
                      value={value}
                      checked={sizeGrid === value}
                      onChange={() => changeSizeGrid(value)}
                      className="sr-only"
                    />
                    <Ruler size={16} aria-hidden="true" className="shrink-0" />
                    {optionLabel}
                  </label>
                ))}
              </div>

              {sizeGrid !== "none" && (
                <>
                  <p className={hint}>
                    Digite o estoque de cada tamanho que você vende. Deixe em branco os tamanhos que a loja não
                    trabalha; use 0 para mostrar o tamanho como esgotado.
                  </p>
                  <div className="grid grid-cols-[repeat(auto-fill,minmax(76px,1fr))] gap-2">
                    {gridSizes.map((size) => {
                      const id = `size-${size}`;
                      const filled = (sizeStock[size] ?? "") !== "";
                      return (
                        <div
                          key={size}
                          className={`flex flex-col gap-1 rounded-lg border p-2 transition-colors ${
                            filled ? "border-primary-container/40 bg-primary-container/5" : "border-slate-200"
                          }`}
                        >
                          <label htmlFor={id} className="text-center text-body-md font-bold text-on-surface">
                            {size}
                          </label>
                          <input
                            id={id}
                            value={sizeStock[size] ?? ""}
                            onChange={(e) => setStockForSize(size, e.target.value)}
                            inputMode="numeric"
                            placeholder="–"
                            aria-label={`Estoque do tamanho ${size}`}
                            className={`${input} px-1! py-1.5! text-center tabular-nums`}
                          />
                        </div>
                      );
                    })}
                  </div>
                  <p className="text-body-sm text-on-surface-variant tabular-nums">
                    {offeredSizes.length} {offeredSizes.length === 1 ? "tamanho" : "tamanhos"}, {sizesTotal} peças no total
                  </p>
                </>
              )}
            </Section>

            {/* Cor */}
            <Section title="Cor">
              <p className={hint}>
                Vende a mesma peça em outras cores? Cadastre cada cor como um produto, com as fotos dela, e ligue
                os produtos em &quot;Mesma peça que&quot;. Na vitrine eles viram um card só, com bolinhas para trocar a cor.
              </p>

              <div className="grid grid-cols-1 gap-4 sm:grid-cols-[1fr_auto]">
                <Field id="productColorName" label="Nome da cor">
                  <input
                    id="productColorName"
                    value={colorName}
                    onChange={(e) => setColorName(e.target.value)}
                    placeholder="Ex: Azul marinho"
                    maxLength={40}
                    className={input}
                  />
                </Field>
                <Field id="productColorHex" label="Bolinha">
                  <div className="flex items-center gap-2">
                    <input
                      id="productColorHex"
                      type="color"
                      value={colorHex || "#cccccc"}
                      onChange={(e) => setColorHex(e.target.value.toUpperCase())}
                      className="h-11 w-14 cursor-pointer rounded-lg border border-slate-300 bg-white p-1"
                    />
                    {colorHex && (
                      <button
                        type="button"
                        onClick={() => setColorHex("")}
                        className="text-body-sm text-on-surface-variant hover:text-on-surface hover:underline"
                      >
                        Limpar
                      </button>
                    )}
                  </div>
                </Field>
              </div>

              <div className="flex flex-wrap gap-1.5" aria-label="Cores comuns">
                {COMMON_COLORS.map(([colorLabel, hex]) => (
                  <button
                    key={hex}
                    type="button"
                    onClick={() => {
                      setColorName(colorLabel);
                      setColorHex(hex);
                    }}
                    className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 px-2.5 py-1 text-body-sm text-on-surface transition-colors hover:bg-surface-container-low"
                  >
                    <span
                      aria-hidden="true"
                      className="h-3.5 w-3.5 rounded-full shadow-[inset_0_0_0_1px_rgba(15,23,42,0.2)]"
                      style={{ backgroundColor: hex }}
                    />
                    {colorLabel}
                  </button>
                ))}
              </div>

              <Field
                id="productColorLink"
                label="Mesma peça que"
                hint="Escolha outra cor desta mesma peça. Deixe em branco se o produto não tem outras cores."
              >
                <select
                  id="productColorLink"
                  value={linkedProductId}
                  onChange={(e) => setLinkedProductId(e.target.value)}
                  className={input}
                >
                  <option value="">Nenhuma (produto sem outras cores)</option>
                  {linkOptions.groups.length > 0 && (
                    <optgroup label="Peças que já têm outras cores">
                      {linkOptions.groups.map((o) => (
                        <option key={o.id} value={o.id}>
                          {o.label}
                        </option>
                      ))}
                    </optgroup>
                  )}
                  {linkOptions.singles.length > 0 && (
                    <optgroup label="Outros produtos">
                      {linkOptions.singles.map((o) => (
                        <option key={o.id} value={o.id}>
                          {o.label}
                        </option>
                      ))}
                    </optgroup>
                  )}
                </select>
              </Field>
            </Section>

            {/* Visibilidade */}
            <Section title="Visibilidade">
              <div className="flex flex-col divide-y divide-slate-100 rounded-xl border border-slate-200">
                <Toggle
                  id="productActive"
                  icon={<Eye size={16} aria-hidden="true" />}
                  label="Ativo na vitrine"
                  description="Clientes conseguem ver e comprar."
                  checked={isActive}
                  onChange={setIsActive}
                />
                <Toggle
                  id="productFeatured"
                  icon={<Star size={16} aria-hidden="true" />}
                  label="Produto em destaque"
                  description="Aparece na seção de destaques da vitrine."
                  checked={isFeatured}
                  onChange={setIsFeatured}
                />
              </div>
            </Section>

            {/* Imagens */}
            <Section
              title="Imagens"
              aside={
                <span className="text-body-sm text-outline tabular-nums">
                  {images.length}/{MAX_IMAGES}
                </span>
              }
            >
              <input
                id="productImages"
                type="file"
                accept="image/jpeg,image/png,image/webp"
                multiple
                onChange={handleImagesSelected}
                disabled={!canAddImages}
                className="sr-only"
              />

              {images.length === 0 ? (
                <label
                  htmlFor="productImages"
                  className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-slate-300 bg-surface px-4 py-8 text-center transition-colors hover:border-primary-container/50 hover:bg-surface-container-low"
                >
                  <span className="flex h-10 w-10 items-center justify-center rounded-lg bg-primary-fixed/50 text-primary-container">
                    <ImagePlus size={20} aria-hidden="true" />
                  </span>
                  <span className="text-body-md text-on-surface">
                    <span className="font-semibold text-primary">Clique para enviar</span> até {MAX_IMAGES} imagens
                  </span>
                  <span className="text-body-sm text-outline">JPG, PNG ou WEBP. A primeira vira a capa.</span>
                </label>
              ) : (
                <div className="grid grid-cols-[repeat(auto-fill,minmax(88px,1fr))] gap-2.5">
                  {images.map((img, index) => (
                    <div
                      key={img.key}
                      className={`group relative flex aspect-square items-center justify-center overflow-hidden rounded-lg bg-slate-100 ${
                        index === 0 && !img.uploading ? "ring-2 ring-primary-container ring-offset-2" : ""
                      }`}
                    >
                      {img.uploading ? (
                        <LoaderCircle size={20} aria-label="Enviando imagem" className="animate-spin text-primary-container" />
                      ) : (
                        <>
                          {/* eslint-disable-next-line @next/next/no-img-element */}
                          <img src={img.url} alt={`Imagem ${index + 1}`} className="h-full w-full object-cover" />

                          {index === 0 ? (
                            <span className="absolute bottom-1 left-1 rounded-full bg-primary-container px-1.5 py-0.5 text-[10px] font-bold text-white">
                              Capa
                            </span>
                          ) : (
                            <button
                              type="button"
                              onClick={() => makeCover(img.key)}
                              aria-label="Usar como capa"
                              title="Usar como capa"
                              className="absolute bottom-1 left-1 flex h-6 w-6 items-center justify-center rounded-full bg-slate-900/65 text-white transition-colors hover:bg-primary-container"
                            >
                              <Star size={12} aria-hidden="true" />
                            </button>
                          )}

                          <button
                            type="button"
                            onClick={() => removeImage(img.key)}
                            aria-label="Remover imagem"
                            title="Remover"
                            className="absolute top-1 right-1 flex h-6 w-6 items-center justify-center rounded-full bg-slate-900/65 text-white transition-colors hover:bg-red-600"
                          >
                            <Trash2 size={13} aria-hidden="true" />
                          </button>
                        </>
                      )}
                    </div>
                  ))}

                  {canAddImages && (
                    <label
                      htmlFor="productImages"
                      title="Adicionar imagens"
                      className="flex aspect-square cursor-pointer flex-col items-center justify-center gap-1 rounded-lg border border-dashed border-slate-300 text-outline transition-colors hover:border-primary-container/50 hover:bg-surface-container-low hover:text-primary-container"
                    >
                      <Plus size={20} aria-hidden="true" />
                      <span className="text-[11px] font-semibold">Adicionar</span>
                    </label>
                  )}
                </div>
              )}
            </Section>
          </div>

          {/* Rodapé fixo */}
          <div className="flex flex-col gap-3 border-t border-slate-100 bg-white px-6 py-4">
            {error && <ErrorBox>{error}</ErrorBox>}

            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <button type="button" onClick={onClose} disabled={loading} className={btnSecondary}>
                Cancelar
              </button>
              <button type="submit" disabled={loading || isUploading} className={btnPrimary}>
                {loading ? (
                  <LoaderCircle size={16} aria-hidden="true" className="animate-spin" />
                ) : isEdit ? (
                  <Save size={16} aria-hidden="true" />
                ) : (
                  <Plus size={16} aria-hidden="true" />
                )}
                {loading ? "Salvando..." : isUploading ? "Enviando imagens..." : isEdit ? "Salvar alterações" : "Criar produto"}
              </button>
            </div>
          </div>
        </form>
      </div>
    </div>
  );
}

/* ===========================
   PEÇAS DO FORMULÁRIO
=========================== */

function Section({ title, aside, children }: { title: string; aside?: ReactNode; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-4">
      <div className="flex items-baseline justify-between gap-2">
        <h3 className="text-label-sm font-bold tracking-wider text-outline uppercase">{title}</h3>
        {aside}
      </div>
      {children}
    </section>
  );
}

function Field({
  id,
  label: fieldLabel,
  hint: fieldHint,
  required,
  children,
}: {
  id: string;
  label: string;
  hint?: string;
  required?: boolean;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className={label}>
        {fieldLabel}
        {required && <span className="ml-0.5 text-red-600">*</span>}
      </label>
      {children}
      {fieldHint && <span className={hint}>{fieldHint}</span>}
    </div>
  );
}

function MoneyInput({
  id,
  value,
  onChange,
  required,
}: {
  id: string;
  value: string;
  onChange: (v: string) => void;
  required?: boolean;
}) {
  return (
    <div className="relative">
      <span className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-body-md text-outline">
        R$
      </span>
      <input
        id={id}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder="0,00"
        inputMode="decimal"
        required={required}
        className={`${input} pl-9 tabular-nums`}
      />
    </div>
  );
}

function Toggle({
  id,
  icon,
  label: toggleLabel,
  description,
  checked,
  onChange,
}: {
  id: string;
  icon: ReactNode;
  label: string;
  description: string;
  checked: boolean;
  onChange: (v: boolean) => void;
}) {
  return (
    <label htmlFor={id} className="flex cursor-pointer items-center gap-3 px-4 py-3">
      <span
        className={`flex h-8 w-8 shrink-0 items-center justify-center rounded-lg transition-colors ${
          checked ? "bg-primary-container/10 text-primary-container" : "bg-slate-100 text-outline"
        }`}
      >
        {icon}
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-body-md font-semibold text-on-surface">{toggleLabel}</span>
        <span className="block text-body-sm text-on-surface-variant">{description}</span>
      </span>

      {/* Switch: checkbox real escondido + trilho desenhado */}
      <input
        id={id}
        type="checkbox"
        role="switch"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="peer sr-only"
      />
      <span
        aria-hidden="true"
        className="relative h-6 w-11 shrink-0 rounded-full bg-slate-300 transition-colors peer-checked:bg-primary-container peer-focus-visible:ring-[3px] peer-focus-visible:ring-primary-container/25 after:absolute after:top-0.5 after:left-0.5 after:h-5 after:w-5 after:rounded-full after:bg-white after:shadow-sm after:transition-transform peer-checked:after:translate-x-5"
      />
    </label>
  );
}
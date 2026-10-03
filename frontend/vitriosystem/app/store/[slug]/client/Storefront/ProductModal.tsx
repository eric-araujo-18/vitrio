"use client";

import { useEffect, useState, type ReactNode } from "react";
import { CircleAlert, ImageOff, ShoppingBag, X } from "lucide-react";
import type { PublicProduct } from "@/lib/api_public";
import { cartItemKey, useCart } from "@/lib/cart";
import { formatPrice } from "@/lib/format";
import { QuantityStepper, storeOverlay, storePrimaryButton, useLockBodyScroll } from "./Ui";

interface ProductModalProps {
  product: PublicProduct;
  onClose: () => void;
  onAdded: () => void;
}

export default function ProductModal({ product, onClose, onAdded }: ProductModalProps) {
  const { addItem, items } = useCart();
  const [imageIndex, setImageIndex] = useState(0);
  const [quantity, setQuantity] = useState(1);

  // Tamanhos: se o produto tiver, o cliente precisa escolher um antes de adicionar.
  // Com um único tamanho disponível, ele já vem selecionado.
  const variants = product.variants ?? [];
  const hasVariants = variants.length > 0;
  const [variantId, setVariantId] = useState<number | null>(() => {
    const inStock = variants.filter((v) => v.stockQuantity > 0);
    return inStock.length === 1 ? inStock[0].id : null;
  });
  const variant = variants.find((v) => v.id === variantId) ?? null;
  const needsSize = hasVariants && !variant;

  const inCart = items.find((i) => i.key === cartItemKey(product.id, variant?.id))?.quantity ?? 0;
  const stock = hasVariants ? (variant?.stockQuantity ?? 0) : product.stockQuantity;
  const available = Math.max(0, stock - inCart);
  const hasPromo = product.promotionalPrice != null && product.promotionalPrice < product.price;
  const unitPrice = hasPromo ? product.promotionalPrice! : product.price;
  const image = product.images[imageIndex];

  useLockBodyScroll();

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => e.key === "Escape" && onClose();
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  function chooseSize(id: number) {
    setVariantId(id);
    setQuantity(1); // cada tamanho tem um estoque diferente
  }

  function handleAdd() {
    if (needsSize || available <= 0) return;
    addItem(product, Math.min(quantity, available), variant);
    onAdded();
  }

  return (
    <div className={`${storeOverlay} flex items-end justify-center sm:items-center sm:p-4`} onClick={onClose}>
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="product-modal-title"
        onClick={(e) => e.stopPropagation()}
        className="relative grid max-h-[92vh] w-full max-w-[880px] animate-modal-in grid-cols-1 gap-6 overflow-y-auto rounded-t-2xl bg-white p-4 shadow-[0_25px_50px_-12px_rgba(15,23,42,0.25)] sm:rounded-2xl sm:p-6 min-[721px]:grid-cols-2"
      >
        <button
          type="button"
          onClick={onClose}
          aria-label="Fechar"
          className="absolute top-3 right-3 z-10 flex h-9 w-9 items-center justify-center rounded-full bg-white/90 text-slate-700 shadow-sm backdrop-blur transition-colors hover:bg-slate-100"
        >
          <X size={20} aria-hidden="true" />
        </button>

        {/* Galeria */}
        <div className="flex flex-col gap-2.5">
          <div className="flex aspect-square items-center justify-center overflow-hidden rounded-xl bg-slate-100 text-slate-400">
            {image ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={image.url} alt={product.name} className="h-full w-full object-cover" />
            ) : (
              <span className="flex flex-col items-center gap-1.5 text-body-md">
                <ImageOff size={28} aria-hidden="true" />
                Sem imagem
              </span>
            )}
          </div>

          {product.images.length > 1 && (
            <div className="flex gap-2 overflow-x-auto pb-1">
              {product.images.map((img, i) => (
                <button
                  key={img.id}
                  type="button"
                  onClick={() => setImageIndex(i)}
                  aria-label={`Ver imagem ${i + 1}`}
                  aria-pressed={i === imageIndex}
                  className={`h-16 w-16 shrink-0 overflow-hidden rounded-lg border-2 bg-slate-100 transition-colors ${
                    i === imageIndex
                      ? "border-[var(--store-primary)]"
                      : "border-transparent opacity-70 hover:opacity-100"
                  }`}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img src={img.url} alt="" className="h-full w-full object-cover" />
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Informações */}
        <div className="flex flex-col gap-4 min-[721px]:pt-2">
          <div className="flex flex-col gap-1.5 pr-10">
            {product.category && (
              <span className="text-label-sm font-bold tracking-wider text-[var(--store-primary)] uppercase">
                {product.category.name}
              </span>
            )}
            <h2 id="product-modal-title" className="text-headline-md text-slate-900">
              {product.name}
            </h2>
          </div>

          <div className="flex flex-col gap-1">
            <div className="flex flex-wrap items-baseline gap-2.5">
              {hasPromo && (
                <span className="text-body-md text-slate-400 line-through">
                  {formatPrice(product.price * quantity)}
                </span>
              )}
              <span
                aria-live="polite"
                className="text-[28px] leading-tight font-extrabold text-[var(--store-primary)] tabular-nums"
              >
                {formatPrice(unitPrice * quantity)}
              </span>
            </div>
            {quantity > 1 && (
              <span className="text-body-sm text-slate-500">
                {quantity} × {formatPrice(unitPrice)} cada
              </span>
            )}
          </div>

          {product.description && (
            <p className="text-body-md leading-relaxed whitespace-pre-line text-slate-600">
              {product.description}
            </p>
          )}

          <div className="mt-auto flex flex-col gap-4 border-t border-slate-100 pt-4">
            {hasVariants && product.stockQuantity > 0 && (
              <fieldset>
                <legend className="mb-2.5 text-body-md font-semibold text-slate-900">
                  Tamanho
                  {variant && <span className="ml-1.5 font-normal text-slate-500">{variant.size}</span>}
                </legend>
                <div className="flex flex-wrap gap-2">
                  {variants.map((v) => {
                    const soldOut = v.stockQuantity <= 0;
                    const active = v.id === variantId;
                    return (
                      <button
                        key={v.id}
                        type="button"
                        onClick={() => chooseSize(v.id)}
                        disabled={soldOut}
                        aria-pressed={active}
                        aria-label={soldOut ? `Tamanho ${v.size}, esgotado` : `Tamanho ${v.size}`}
                        className={`relative flex h-11 min-w-11 items-center justify-center rounded-lg border px-3 text-body-md font-semibold transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--store-primary)] ${
                          active
                            ? "border-[var(--store-primary)] bg-[var(--store-primary)] text-white"
                            : soldOut
                              ? "cursor-not-allowed border-slate-200 bg-slate-50 text-slate-300 line-through"
                              : "border-slate-300 bg-white text-slate-800 hover:border-slate-500"
                        }`}
                      >
                        {v.size}
                      </button>
                    );
                  })}
                </div>
              </fieldset>
            )}

            {product.stockQuantity <= 0 ? (
              <StockWarning>Produto esgotado.</StockWarning>
            ) : needsSize ? (
              <button type="button" disabled className={storePrimaryButton}>
                Escolha um tamanho
              </button>
            ) : available <= 0 ? (
              <StockWarning>
                {variant
                  ? `Você já adicionou todo o estoque do tamanho ${variant.size}.`
                  : "Você já adicionou todo o estoque disponível."}
              </StockWarning>
            ) : (
              <>
                <div className="flex items-center gap-3">
                  <QuantityStepper
                    value={quantity}
                    onDecrease={() => setQuantity((q) => Math.max(1, q - 1))}
                    onIncrease={() => setQuantity((q) => Math.min(available, q + 1))}
                    canDecrease={quantity > 1}
                    canIncrease={quantity < available}
                  />
                  {available <= 5 && (
                    <span className="text-body-sm font-medium text-amber-700">
                      Só {available} disponível(is)
                    </span>
                  )}
                </div>

                <button type="button" onClick={handleAdd} className={storePrimaryButton}>
                  <ShoppingBag size={18} aria-hidden="true" />
                  Adicionar ao carrinho
                  <span className="font-semibold opacity-80">{formatPrice(unitPrice * quantity)}</span>
                </button>
              </>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

function StockWarning({ children }: { children: ReactNode }) {
  return (
    <p className="flex items-center gap-2 rounded-lg bg-amber-500/10 px-3 py-2.5 text-body-md font-semibold text-amber-700">
      <CircleAlert size={18} aria-hidden="true" className="shrink-0" />
      {children}
    </p>
  );
}
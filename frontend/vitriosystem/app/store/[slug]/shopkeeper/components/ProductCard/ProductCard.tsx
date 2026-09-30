import { ImageOff, PackageX, Pencil, Star, Trash2, TriangleAlert } from "lucide-react";
import type { Product } from "@/lib/api_product";
import { formatPrice } from "@/lib/format";

interface ProductCardProps {
  product: Product;
  onEdit?: () => void;
  onDelete?: () => void;
}

// Abaixo disso o estoque aparece em âmbar como alerta.
const LOW_STOCK = 5;

export default function ProductCard({ product, onEdit, onDelete }: ProductCardProps) {
  const hasPromo = product.promotionalPrice != null && product.promotionalPrice < product.price;
  const outOfStock = product.stockQuantity <= 0;
  const lowStock = !outOfStock && product.stockQuantity <= LOW_STOCK;
  const cover = product.images?.[0];

  const discount = hasPromo
    ? Math.round((1 - (product.promotionalPrice as number) / product.price) * 100)
    : 0;

  return (
    <div
      className={`group relative flex flex-col overflow-hidden rounded-2xl border border-slate-200/85 bg-white transition-all duration-200 hover:-translate-y-0.5 hover:border-primary-container/25 hover:shadow-[0_4px_6px_-1px_rgba(37,99,235,0.04),0_10px_24px_-4px_rgba(15,23,42,0.08)] ${
        !product.isActive ? "opacity-70 hover:opacity-90" : ""
      }`}
    >
      {/* Imagem */}
      <div className="relative flex aspect-square items-center justify-center overflow-hidden bg-linear-135 from-slate-50 to-slate-100">
        {cover ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={cover.url}
            alt={product.name}
            className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
          />
        ) : (
          <span className="flex flex-col items-center gap-1 text-body-sm text-slate-400">
            <ImageOff size={22} aria-hidden="true" />
            Sem imagem
          </span>
        )}

        {/* Sombra suave nas bordas da imagem */}
        <div
          aria-hidden="true"
          className="pointer-events-none absolute inset-0 bg-linear-to-b from-slate-900/5 via-transparent to-slate-900/5"
        />

        {/* Selos */}
        <div className="absolute inset-x-2.5 top-2.5 z-10 flex flex-wrap gap-1.5">
          {product.isFeatured && (
            <span className="inline-flex items-center gap-1 rounded-md border border-white/50 bg-amber-500/95 px-2 py-1 text-[11px] font-bold text-white shadow-sm backdrop-blur-sm">
              <Star size={11} fill="currentColor" aria-hidden="true" />
              Destaque
            </span>
          )}
          {!product.isActive && (
            <span className="inline-flex items-center rounded-md border border-white/50 bg-slate-600/90 px-2 py-1 text-[11px] font-bold text-white shadow-sm backdrop-blur-sm">
              Inativo
            </span>
          )}
          {hasPromo && discount > 0 && (
            <span className="ml-auto inline-flex items-center rounded-md bg-emerald-600 px-2 py-1 text-[11px] font-bold text-white shadow-sm">
              -{discount}%
            </span>
          )}
        </div>

        {/* Ações: aparecem no hover/foco; em telas de toque ficam sempre visíveis */}
        {(onEdit || onDelete) && (
          <div className="absolute right-2.5 bottom-2.5 z-20 flex translate-y-1 gap-1.5 opacity-0 transition-all duration-150 group-hover:translate-y-0 group-hover:opacity-100 focus-within:translate-y-0 focus-within:opacity-100 [@media(hover:none)]:translate-y-0 [@media(hover:none)]:opacity-100">
            {onEdit && (
              <button
                type="button"
                onClick={onEdit}
                aria-label={`Editar ${product.name}`}
                title="Editar"
                className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/95 text-slate-700 shadow-[0_2px_6px_rgba(0,0,0,0.15)] transition-colors hover:text-primary-container"
              >
                <Pencil size={15} aria-hidden="true" />
              </button>
            )}
            {onDelete && (
              <button
                type="button"
                onClick={onDelete}
                aria-label={`Excluir ${product.name}`}
                title="Excluir"
                className="flex h-8 w-8 items-center justify-center rounded-lg bg-white/95 text-slate-700 shadow-[0_2px_6px_rgba(0,0,0,0.15)] transition-colors hover:text-red-600"
              >
                <Trash2 size={15} aria-hidden="true" />
              </button>
            )}
          </div>
        )}
      </div>

      {/* Corpo */}
      <div className="flex min-w-0 flex-1 flex-col gap-1 px-3.5 pt-3 pb-3.5">
        <span className="line-clamp-2 text-body-md font-semibold text-on-surface">{product.name}</span>

        {(product.category || product.sku) && (
          <div className="flex min-w-0 flex-wrap items-center gap-x-2 gap-y-0.5 text-body-sm">
            {product.category && <span className="text-on-surface-variant">{product.category.name}</span>}
            {product.sku && (
              <span className="truncate font-mono text-[11px] text-outline">SKU {product.sku}</span>
            )}
          </div>
        )}

        <div className="mt-auto flex flex-wrap items-baseline gap-2 pt-2">
          {hasPromo && (
            <span className="text-body-sm text-slate-400 line-through">{formatPrice(product.price)}</span>
          )}
          <span className="text-title-md font-bold tracking-tight text-primary-container tabular-nums">
            {formatPrice(hasPromo ? (product.promotionalPrice as number) : product.price)}
          </span>
        </div>

        <span
          className={`mt-1.5 inline-flex w-fit items-center gap-1.5 rounded-md px-2 py-1 text-[11px] font-semibold ${
            outOfStock
              ? "bg-red-50 text-red-600"
              : lowStock
                ? "bg-amber-50 text-amber-700"
                : "bg-emerald-50 text-emerald-700"
          }`}
        >
          {outOfStock ? (
            <>
              <PackageX size={12} aria-hidden="true" />
              Sem estoque
            </>
          ) : lowStock ? (
            <>
              <TriangleAlert size={12} aria-hidden="true" />
              {product.stockQuantity} em estoque
            </>
          ) : (
            `${product.stockQuantity} em estoque`
          )}
        </span>
      </div>
    </div>
  );
}
import type { CSSProperties } from "react";
import { ShoppingBag, Store as StoreIcon } from "lucide-react";

/*
  Miniatura da vitrine, usando as mesmas variáveis de cor da vitrine real.
  Usada no modal de criar loja e na tela de Personalização do lojista.
*/

interface StorePreviewProps {
  name: string;
  description?: string;
  logoUrl?: string;
  primaryColor: string;
  secondaryColor: string;
  tertiaryColor: string;
  className?: string;
}

export default function StorePreview({
  name,
  description,
  logoUrl,
  primaryColor,
  secondaryColor,
  tertiaryColor,
  className = "",
}: StorePreviewProps) {
  const displayName = name.trim() || "Sua loja";

  return (
    <div
      aria-hidden="true"
      className={`overflow-hidden rounded-2xl border border-slate-200 bg-slate-50 shadow-[0_10px_25px_rgba(0,0,0,0.06)] ${className}`}
      style={
        {
          "--store-primary": primaryColor,
          "--store-secondary": secondaryColor,
          "--store-tertiary": tertiaryColor,
        } as CSSProperties
      }
    >
      {/* Cabeçalho */}
      <div className="flex items-center gap-2.5 bg-[var(--store-tertiary)] px-4 py-3 text-white transition-colors duration-300">
        <div className="flex h-7 w-7 shrink-0 items-center justify-center overflow-hidden rounded-md bg-[var(--store-primary)] transition-colors duration-300">
          {logoUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={logoUrl} alt="" className="h-full w-full object-cover" />
          ) : (
            <StoreIcon size={14} />
          )}
        </div>
        <strong className="truncate text-body-md">{displayName}</strong>
        <span className="ml-auto flex h-7 w-7 shrink-0 items-center justify-center rounded-md bg-white/12">
          <ShoppingBag size={15} />
        </span>
      </div>

      {/* Hero */}
      <div className="bg-linear-135 from-[var(--store-primary)] to-[var(--store-secondary)] px-4 py-5 text-white">
        <strong className="block truncate text-title-md">{displayName}</strong>
        <p className="mt-1 line-clamp-3 text-body-sm text-white/90">
          {description?.trim() || "A descrição da sua loja aparece aqui."}
        </p>
      </div>

      {/* Produtos */}
      <div className="grid grid-cols-2 gap-2.5 p-3">
        {[1, 2].map((i) => (
          <div key={i} className="flex flex-col overflow-hidden rounded-lg border border-slate-200 bg-white">
            <div className="aspect-square bg-slate-100" />
            <div className="flex flex-col gap-1 p-2">
              <span className="text-body-sm text-slate-900">Produto {i}</span>
              <strong className="text-body-md text-[var(--store-primary)] transition-colors duration-300">
                R$ 49,90
              </strong>
              <span className="mt-1 rounded-md bg-[var(--store-primary)] py-1.5 text-center text-[11px] font-semibold text-white transition-colors duration-300">
                Adicionar
              </span>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
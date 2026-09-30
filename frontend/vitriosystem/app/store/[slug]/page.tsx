"use client";

import { useEffect, useMemo, useState, type CSSProperties, type ReactNode } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  ImageOff,
  LayoutDashboard,
  LoaderCircle,
  MessageCircle,
  Search,
  SearchX,
  ShoppingBag,
  Star,
  Store as StoreIcon,
} from "lucide-react";
import {
  getPublicCategories,
  getPublicProducts,
  getPublicStore,
  type PublicCategory,
  type PublicProduct,
  type PublicStore,
} from "@/lib/api_public";
import { CartProvider, useCart } from "@/lib/cart";
import { formatPrice, whatsappLink } from "@/lib/format";
import { useAuth } from "@/lib/auth_context";
import ProductModal from "./client/Storefront/ProductModal";
import CartDrawer from "./client/Storefront/CartDrawer";

/*
  As cores vêm da loja via variáveis CSS no elemento raiz:
  --store-primary, --store-secondary, --store-tertiary.
  Nas classes, são usadas como bg-[var(--store-primary)] etc.
  ProductModal e CartDrawer ficam dentro desse elemento, então herdam as variáveis.
*/

const DEFAULT_COLORS = {
  primary: "#2563eb",
  secondary: "#1d4ed8",
  tertiary: "#111827",
};

const CONTAINER = "mx-auto w-full max-w-[1180px] px-4 sm:px-5";

// Vitrine pública: qualquer pessoa acessa, sem login.
// (Antes esta rota exigia login e redirecionava para /client ou /shopkeeper.)
export default function StorefrontPage() {
  const { slug } = useParams<{ slug: string }>();
  return (
    <CartProvider storeSlug={slug}>
      <Storefront slug={slug} />
    </CartProvider>
  );
}

function Storefront({ slug }: { slug: string }) {
  const { user } = useAuth();
  const { totalItems } = useCart();

  const [store, setStore] = useState<PublicStore | null>(null);
  const [categories, setCategories] = useState<PublicCategory[]>([]);
  const [products, setProducts] = useState<PublicProduct[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "notfound">("loading");

  const [activeCategory, setActiveCategory] = useState<string>("");
  const [search, setSearch] = useState("");
  const [selected, setSelected] = useState<PublicProduct | null>(null);
  const [cartOpen, setCartOpen] = useState(false);

  // Carrega loja + categorias + produtos uma vez; filtros são feitos no cliente
  // (vitrines pequenas/médias — o backend limita a 500 produtos).
  useEffect(() => {
    let active = true;
    Promise.all([getPublicStore(slug), getPublicCategories(slug), getPublicProducts(slug)])
      .then(([s, c, p]) => {
        if (!active) return;
        if (!s.status || !s.dados) {
          setStatus("notfound");
          return;
        }
        setStore(s.dados);
        setCategories(c.dados ?? []);
        setProducts(p.dados ?? []);
        setStatus("ready");
        document.title = s.dados.name;
      })
      .catch(() => active && setStatus("notfound"));
    return () => {
      active = false;
    };
  }, [slug]);

  const visible = useMemo(() => {
    const term = search.trim().toLowerCase();
    return products.filter((p) => {
      if (activeCategory && p.category?.slug !== activeCategory) return false;
      if (!term) return true;
      return p.name.toLowerCase().includes(term) || (p.description ?? "").toLowerCase().includes(term);
    });
  }, [products, activeCategory, search]);

  const featured = useMemo(() => products.filter((p) => p.isFeatured).slice(0, 8), [products]);

  if (status === "loading") {
    return (
      <div
        role="status"
        className="flex min-h-screen items-center justify-center gap-2 bg-slate-50 text-body-md text-slate-500"
      >
        <LoaderCircle size={20} aria-hidden="true" className="animate-spin" />
        Carregando loja...
      </div>
    );
  }

  if (status === "notfound" || !store) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-3 bg-slate-50 p-4 text-center">
        <div className="mb-1 flex h-16 w-16 items-center justify-center rounded-2xl bg-slate-200/70 text-slate-500">
          <StoreIcon size={32} aria-hidden="true" />
        </div>
        <h1 className="text-headline-md text-slate-900">Loja não encontrada</h1>
        <p className="max-w-sm text-body-md text-slate-500">
          Ela pode ter sido pausada ou o endereço está incorreto.
        </p>
        <Link
          href="/"
          className="mt-2 inline-flex h-11 items-center rounded-lg bg-primary-container px-5 text-title-md text-white transition-colors hover:bg-[#1d4ed8]"
        >
          Ir para o Vitrio
        </Link>
      </div>
    );
  }

  const themeVars = {
    "--store-primary": store.primaryColor || DEFAULT_COLORS.primary,
    "--store-secondary": store.secondaryColor || DEFAULT_COLORS.secondary,
    "--store-tertiary": store.tertiaryColor || DEFAULT_COLORS.tertiary,
  } as CSSProperties;

  const isOwnerView = user && (user.role === "Shopkeeper" || user.role === "Admin");
  const showFeatured = featured.length > 0 && !activeCategory && !search;

  return (
    <div className="flex min-h-screen flex-col bg-slate-50 text-slate-900 antialiased" style={themeVars}>
      {/* HEADER */}
      <header className="sticky top-0 z-20 bg-[var(--store-tertiary)] text-white shadow-[0_1px_8px_rgba(0,0,0,0.12)]">
        <div className={`${CONTAINER} flex h-16 items-center justify-between gap-4`}>
          <div className="flex min-w-0 items-center gap-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-lg bg-[var(--store-primary)]">
              {store.logoUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={store.logoUrl} alt={store.name} className="h-full w-full object-cover" />
              ) : (
                <StoreIcon size={20} aria-hidden="true" />
              )}
            </div>
            <span className="truncate text-headline-sm font-bold">{store.name}</span>
          </div>

          <div className="flex shrink-0 items-center gap-2">
            {isOwnerView && (
              <Link
                href={`/store/${store.slug}/shopkeeper`}
                className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-white/30 px-3 text-label-md font-semibold transition-colors hover:bg-white/10"
              >
                <LayoutDashboard size={16} aria-hidden="true" />
                <span className="hidden sm:inline">Painel</span>
              </Link>
            )}

            <button
              type="button"
              onClick={() => setCartOpen(true)}
              aria-label={totalItems > 0 ? `Abrir carrinho (${totalItems} itens)` : "Abrir carrinho"}
              className="relative flex h-11 w-11 items-center justify-center rounded-lg bg-white/12 transition-colors hover:bg-white/20"
            >
              <ShoppingBag size={20} aria-hidden="true" />
              {totalItems > 0 && (
                <span className="absolute -top-1 -right-1 flex h-5 min-w-5 items-center justify-center rounded-full border-2 border-[var(--store-tertiary)] bg-[var(--store-primary)] px-1 text-[11px] font-bold">
                  {totalItems}
                </span>
              )}
            </button>
          </div>
        </div>
      </header>

      {/* HERO */}
      <section className="relative isolate overflow-hidden bg-linear-135 from-[var(--store-primary)] to-[var(--store-secondary)] text-white">
        <div className="pointer-events-none absolute -top-24 -right-24 -z-10 h-80 w-80 rounded-full bg-white/10 blur-3xl" />

        <div className={`${CONTAINER} py-10 sm:py-14`}>
          <h1 className="mb-2 text-display-lg-mobile md:text-display-lg">{store.name}</h1>
          {store.description && (
            <p className="max-w-[620px] text-body-lg text-white/90">{store.description}</p>
          )}
          {store.phone && (
            <a
              href={whatsappLink(store.phone)}
              target="_blank"
              rel="noopener noreferrer"
              className="mt-5 inline-flex h-11 items-center gap-2 rounded-lg bg-white px-4 text-title-md text-[var(--store-tertiary)] shadow-lg transition-transform duration-200 hover:-translate-y-0.5"
            >
              <MessageCircle size={18} aria-hidden="true" />
              Fale com a loja
            </a>
          )}
        </div>
      </section>

      {/* CONTEÚDO */}
      <main className={`${CONTAINER} flex-1 pt-7 pb-12`}>
        {showFeatured && (
          <section className="mb-10">
            <h2 className="mb-4 flex items-center gap-2 text-headline-sm text-slate-900">
              <Star
                size={18}
                aria-hidden="true"
                className="fill-[var(--store-primary)] text-[var(--store-primary)]"
              />
              Destaques
            </h2>
            <ProductGrid products={featured} onOpen={setSelected} />
          </section>
        )}

        <section>
          <div className="mb-5 flex flex-col gap-4">
            <div className="relative max-w-[420px]">
              <Search
                size={18}
                aria-hidden="true"
                className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-slate-400"
              />
              <input
                type="search"
                aria-label="Buscar produtos"
                placeholder="Buscar produtos"
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                className="h-11 w-full rounded-lg border border-slate-200 bg-white pr-3 pl-10 text-body-lg outline-none transition placeholder:text-slate-400 focus:border-[var(--store-primary)] focus:ring-[3px] focus:ring-[color-mix(in_srgb,var(--store-primary)_20%,transparent)]"
              />
            </div>

            {categories.length > 0 && (
              <div className="-mx-4 flex gap-2 overflow-x-auto px-4 pb-1 sm:mx-0 sm:px-0">
                <CategoryChip active={!activeCategory} onClick={() => setActiveCategory("")}>
                  Todos
                </CategoryChip>
                {categories.map((c) => (
                  <CategoryChip
                    key={c.id}
                    active={activeCategory === c.slug}
                    onClick={() => setActiveCategory(c.slug)}
                  >
                    {c.name}
                  </CategoryChip>
                ))}
              </div>
            )}
          </div>

          {visible.length === 0 ? (
            <div className="flex flex-col items-center gap-2 rounded-2xl border border-dashed border-slate-300 bg-white px-6 py-12 text-center">
              <SearchX size={28} aria-hidden="true" className="text-slate-400" />
              <p className="text-body-md text-slate-500">
                {products.length === 0 ? "Esta loja ainda não tem produtos." : "Nenhum produto encontrado."}
              </p>
            </div>
          ) : (
            <ProductGrid products={visible} onOpen={setSelected} />
          )}
        </section>
      </main>

      {/* FOOTER */}
      <footer className="border-t border-slate-200 bg-white">
        <div
          className={`${CONTAINER} flex flex-wrap items-center justify-between gap-4 py-6 text-body-sm text-slate-500`}
        >
          <span>{store.name}</span>
          <span>
            Loja criada com{" "}
            <Link href="/" className="font-semibold text-slate-900 hover:underline">
              Vitrio
            </Link>
          </span>
        </div>
      </footer>

      {selected && (
        <ProductModal
          product={selected}
          onClose={() => setSelected(null)}
          onAdded={() => {
            setSelected(null);
            setCartOpen(true);
          }}
        />
      )}

      {cartOpen && <CartDrawer store={store} onClose={() => setCartOpen(false)} />}
    </div>
  );
}

/* ===========================
   COMPONENTES AUXILIARES
=========================== */

function CategoryChip({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-pressed={active}
      className={`h-9 shrink-0 rounded-full border px-4 text-label-md font-semibold transition-colors ${
        active
          ? "border-[var(--store-primary)] bg-[var(--store-primary)] text-white"
          : "border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:text-slate-900"
      }`}
    >
      {children}
    </button>
  );
}

function ProductGrid({
  products,
  onOpen,
}: {
  products: PublicProduct[];
  onOpen: (product: PublicProduct) => void;
}) {
  return (
    <div className="grid grid-cols-2 gap-3 sm:gap-4 min-[721px]:grid-cols-[repeat(auto-fill,minmax(200px,1fr))]">
      {products.map((p) => (
        <ProductTile key={p.id} product={p} onOpen={() => onOpen(p)} />
      ))}
    </div>
  );
}

function ProductTile({ product, onOpen }: { product: PublicProduct; onOpen: () => void }) {
  const hasPromo = product.promotionalPrice != null && product.promotionalPrice < product.price;
  const outOfStock = product.stockQuantity <= 0;
  const cover = product.images[0];

  return (
    <button
      type="button"
      onClick={onOpen}
      className="group flex flex-col overflow-hidden rounded-xl border border-slate-200 bg-white text-left transition-all duration-200 hover:-translate-y-1 hover:shadow-[0_10px_24px_rgba(15,23,42,0.08)] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[var(--store-primary)]"
    >
      <div className="relative flex aspect-square items-center justify-center overflow-hidden bg-slate-100 text-slate-400">
        {cover ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={cover.url}
            alt={product.name}
            loading="lazy"
            className={`h-full w-full object-cover transition-transform duration-300 group-hover:scale-105 ${
              outOfStock ? "opacity-60 grayscale-[40%]" : ""
            }`}
          />
        ) : (
          <span className="flex flex-col items-center gap-1 text-body-sm">
            <ImageOff size={22} aria-hidden="true" />
            Sem imagem
          </span>
        )}

        {hasPromo && (
          <span className="absolute top-2.5 left-2.5 rounded-md bg-[var(--store-primary)] px-2 py-1 text-[11px] font-bold text-white">
            Promoção
          </span>
        )}
        {outOfStock && (
          <span className="absolute top-2.5 right-2.5 rounded-md bg-slate-900/80 px-2 py-1 text-[11px] font-bold text-white">
            Esgotado
          </span>
        )}
      </div>

      <div className="flex flex-col gap-1.5 px-3.5 pt-3 pb-4">
        <span className="line-clamp-2 text-body-md font-semibold text-slate-900">{product.name}</span>
        <div className="flex flex-wrap items-baseline gap-2">
          {hasPromo && (
            <span className="text-body-sm text-slate-400 line-through">{formatPrice(product.price)}</span>
          )}
          <span className="text-title-md font-extrabold text-[var(--store-primary)]">
            {formatPrice(hasPromo ? product.promotionalPrice! : product.price)}
          </span>
        </div>
      </div>
    </button>
  );
}
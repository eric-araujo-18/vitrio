"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { Package, Plus, Search, SearchX } from "lucide-react";
import { unwrap } from "@/lib/api";
import { deleteProduct, getProductsByStore, type Product } from "@/lib/api_product";
import CreateProductModal from "../../components/CreateProductModal/CreateProductModal";
import ProductCard from "../../components/ProductCard/ProductCard";
import { useShopkeeperStore } from "../../components/ShopkeeperStoreContext";
import {
  ConfirmDialog,
  EmptyState,
  ErrorBox,
  LoadingState,
  PageHeader,
  btnPrimary,
  chip,
  input,
} from "../../components/Ui";

type Filter = "all" | "active" | "inactive" | "outOfStock";

const FILTERS: { key: Filter; label: string }[] = [
  { key: "all", label: "Todos" },
  { key: "active", label: "Ativos" },
  { key: "inactive", label: "Inativos" },
  { key: "outOfStock", label: "Sem estoque" },
];

export default function ProductsShopkeeper() {
  const { store } = useShopkeeperStore();

  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState("");
  const [filter, setFilter] = useState<Filter>("all");

  // null = fechado; "new" = criando; Product = editando
  const [editing, setEditing] = useState<Product | "new" | null>(null);

  // Exclusão (substitui confirm()/alert() do navegador)
  const [toDelete, setToDelete] = useState<Product | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const loadProducts = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setProducts(await unwrap(getProductsByStore(store.id)));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Erro ao carregar produtos.");
    } finally {
      setLoading(false);
    }
  }, [store.id]);

  useEffect(() => {
    loadProducts();
  }, [loadProducts]);

  const visibleProducts = useMemo(() => {
    const term = search.trim().toLowerCase();
    return products.filter((p) => {
      if (filter === "active" && !p.isActive) return false;
      if (filter === "inactive" && p.isActive) return false;
      if (filter === "outOfStock" && p.stockQuantity > 0) return false;
      if (!term) return true;
      return p.name.toLowerCase().includes(term) || (p.sku ?? "").toLowerCase().includes(term);
    });
  }, [products, search, filter]);

  function handleSaved(product: Product) {
    setProducts((prev) => {
      const exists = prev.some((p) => p.id === product.id);
      return exists ? prev.map((p) => (p.id === product.id ? product : p)) : [product, ...prev];
    });
    setEditing(null);

    // Ligar cores também muda o grupo do outro produto, então atualiza a lista
    // em segundo plano (sem mostrar o carregando).
    getProductsByStore(store.id)
      .then(({ dados }) => dados && setProducts(dados))
      .catch(() => {});
  }

  const closeDeleteDialog = useCallback(() => {
    setToDelete(null);
    setDeleteError(null);
  }, []);

  async function confirmDelete() {
    if (!toDelete) return;
    setDeleting(true);
    setDeleteError(null);
    try {
      await unwrap(deleteProduct(toDelete.id));
      setProducts((prev) => prev.filter((p) => p.id !== toDelete.id));
      closeDeleteDialog();
    } catch (err) {
      setDeleteError(err instanceof Error ? err.message : "Erro ao excluir produto.");
    } finally {
      setDeleting(false);
    }
  }

  function clearFilters() {
    setSearch("");
    setFilter("all");
  }

  return (
    <>
      <PageHeader
        title="Produtos"
        subtitle={`${products.length} produto(s) cadastrados`}
        actions={
          <button type="button" onClick={() => setEditing("new")} className={btnPrimary}>
            <Plus size={18} strokeWidth={2.5} aria-hidden="true" />
            Novo produto
          </button>
        }
      />

      {products.length > 0 && (
        <div className="mb-5 flex flex-col gap-3 md:flex-row md:items-center">
          <div className="relative w-full md:max-w-[340px] md:flex-1">
            <Search
              size={16}
              aria-hidden="true"
              className="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-outline"
            />
            <input
              type="search"
              aria-label="Buscar produtos"
              placeholder="Buscar por nome ou SKU"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className={`${input} h-10 py-0 pl-9`}
            />
          </div>

          <div className="-mx-5 flex gap-2 overflow-x-auto px-5 pb-1 md:mx-0 md:px-0 md:pb-0">
            {FILTERS.map((f) => (
              <button
                key={f.key}
                type="button"
                onClick={() => setFilter(f.key)}
                aria-pressed={filter === f.key}
                className={chip(filter === f.key)}
              >
                {f.label}
              </button>
            ))}
          </div>
        </div>
      )}

      {error && (
        <div className="mb-4">
          <ErrorBox>{error}</ErrorBox>
        </div>
      )}

      {loading ? (
        <LoadingState>Carregando produtos...</LoadingState>
      ) : products.length === 0 ? (
        <EmptyState icon={<Package size={26} aria-hidden="true" />}>
          <p>Nenhum produto cadastrado ainda.</p>
          <button type="button" onClick={() => setEditing("new")} className={btnPrimary}>
            <Plus size={18} strokeWidth={2.5} aria-hidden="true" />
            Criar primeiro produto
          </button>
        </EmptyState>
      ) : visibleProducts.length === 0 ? (
        <div className="flex flex-col items-center gap-3 rounded-2xl border border-dashed border-slate-300 bg-white px-4 py-12 text-center">
          <SearchX size={26} aria-hidden="true" className="text-outline" />
          <p className="text-body-md text-on-surface-variant">Nenhum produto encontrado com esses filtros.</p>
          <button
            type="button"
            onClick={clearFilters}
            className="text-label-md font-semibold text-primary-container hover:underline"
          >
            Limpar filtros
          </button>
        </div>
      ) : (
        <div className="grid grid-cols-1 gap-4 min-[480px]:grid-cols-2 md:grid-cols-[repeat(auto-fill,minmax(220px,1fr))]">
          {visibleProducts.map((product) => (
            <ProductCard
              key={product.id}
              product={product}
              onEdit={() => setEditing(product)}
              onDelete={() => setToDelete(product)}
            />
          ))}
        </div>
      )}

      {editing && (
        <CreateProductModal
          storeId={store.id}
          product={editing === "new" ? undefined : editing}
          onClose={() => setEditing(null)}
          onSaved={handleSaved}
        />
      )}

      {toDelete && (
        <ConfirmDialog
          title="Excluir produto?"
          message={
            <>
              <strong className="text-on-surface">{toDelete.name}</strong> some da vitrine, mas continua nos
              pedidos antigos.
            </>
          }
          confirmLabel="Excluir produto"
          busy={deleting}
          error={deleteError}
          onConfirm={confirmDelete}
          onCancel={closeDeleteDialog}
        />
      )}
    </>
  );
}
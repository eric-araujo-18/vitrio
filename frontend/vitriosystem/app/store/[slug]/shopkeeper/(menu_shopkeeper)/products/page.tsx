"use client";

import { useEffect, useState, useCallback } from "react";
import { useParams } from "next/navigation";
import { Plus } from "lucide-react";
import SidebarShopkeeper from "../../components/Sidebar/SidebarShopkeeper";
import CreateProductModal from "../../components/CreateProductModal/CreateProductModal";
import ProductCard from "../../components/ProductCard/ProductCard";
import { getProductsByStore, type Product } from "@/lib/api_product";
import { getMyStores, type Store} from "@/lib/api";
import styles from "./Products.module.css";

export default function ProductsShopkeeper() {
  const params = useParams<{ slug: string }>();
  const slug = params?.slug ?? "";

  const [store, setStore] = useState<Store | null>(null);
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const { dados: stores } = await getMyStores();
      const found = stores?.find((s) => s.slug === slug) ?? null;
      setStore(found);

      if (found) {
        const { dados: productList } = await getProductsByStore(found.id);
        setProducts(productList ?? []);
      }
    } finally {
      setLoading(false);
    }
  }, [slug]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  function handleCreated(product: Product) {
    setProducts((prev) => [product, ...prev]);
    setModalOpen(false);
  }

  return (
    <div className={styles.page}>
      <SidebarShopkeeper />

      <main className={styles.content}>
        <div className={styles.header}>
          <div>
            <h1 className={styles.title}>Produtos</h1>
            <p className={styles.subtitle}>Gerencie os produtos da sua loja</p>
          </div>

          <button
            type="button"
            className={styles.addButton}
            onClick={() => setModalOpen(true)}
            disabled={!store}
          >
            <Plus size={18} strokeWidth={2.5} />
            Novo produto
          </button>
        </div>

        {loading ? (
          <p className={styles.stateText}>Carregando produtos...</p>
        ) : products.length === 0 ? (
          <div className={styles.emptyState}>
            <p>Nenhum produto cadastrado ainda.</p>
            <button
              type="button"
              className={styles.addButton}
              onClick={() => setModalOpen(true)}
              disabled={!store}
            >
              <Plus size={18} strokeWidth={2.5} />
              Criar primeiro produto
            </button>
          </div>
        ) : (
          <div className={styles.grid}>
            {products.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </div>
        )}
      </main>

      {modalOpen && store && (
        <CreateProductModal
          storeId={store.id}
          onClose={() => setModalOpen(false)}
          onCreated={handleCreated}
        />
      )}
    </div>
  );
}
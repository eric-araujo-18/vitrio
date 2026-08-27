import { Star, PackageX } from "lucide-react";
import type { Product } from "@/lib/api_product";
import styles from "./ProductCard.module.css";

interface ProductCardProps {
  product: Product;
}

function formatPrice(value: number) {
  return value.toLocaleString("pt-BR", {
    style: "currency",
    currency: "BRL",
  });
}

export default function ProductCard({ product }: ProductCardProps) {
  const hasPromo =
    product.promotionalPrice != null && product.promotionalPrice < product.price;
  const outOfStock = product.stockQuantity <= 0;
  const cover = product.images?.[0];

  return (
    <div className={`${styles.card} ${!product.isActive ? styles.cardInactive : ""}`}>
      <div className={styles.cardImage}>
        {cover ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={cover.url} alt={product.name} />
        ) : (
          <span className={styles.noImage}>Sem imagem</span>
        )}

        <div className={styles.cardBadges}>
          {product.isFeatured && (
            <span className={styles.featuredBadge}>
              <Star size={11} fill="currentColor" />
              Destaque
            </span>
          )}
          {!product.isActive && <span className={styles.inactiveBadge}>Inativo</span>}
        </div>
      </div>

      <div className={styles.cardBody}>
        <span className={styles.cardName}>{product.name}</span>

        {product.sku && <span className={styles.cardSku}>SKU: {product.sku}</span>}

        <div className={styles.cardPriceRow}>
          {hasPromo ? (
            <>
              <span className={styles.cardOldPrice}>{formatPrice(product.price)}</span>
              <span className={styles.cardPrice}>
                {formatPrice(product.promotionalPrice as number)}
              </span>
            </>
          ) : (
            <span className={styles.cardPrice}>{formatPrice(product.price)}</span>
          )}
        </div>

        <span className={outOfStock ? styles.cardStockEmpty : styles.cardStock}>
          {outOfStock ? (
            <>
              <PackageX size={12} />
              Sem estoque
            </>
          ) : (
            `${product.stockQuantity} em estoque`
          )}
        </span>
      </div>
    </div>
  );
}
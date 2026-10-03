namespace BackendSystemVitrio.Models
{
    public class Product
    {
        public int Id { get; set; }

        // Loja dona do produto
        public required int StoreId { get; set; }
        public Store? Store { get; set; }

        // adicionar no Product.cs
        public int? CategoryId { get; set; }
        public Category? Category { get; set; }

        // Informações básicas
        public required string Name { get; set; }

        // Usado na URL do produto
        // Ex: /loja/casa-da-erica/produto/camiseta-basica
        // Único por loja (StoreId, Slug) — não globalmente, já que lojas
        // diferentes podem gerar o mesmo slug a partir do nome do produto.
        public required string Slug { get; set; }

        public string? Description { get; set; }

        // Código interno/SKU do produto
        public string? Sku { get; set; }

        // Preço atual
        public decimal Price { get; set; }

        // Preço promocional opcional
        // (validar PromotionalPrice < Price na camada de service/DTO,
        // não dá pra garantir isso só no model)
        public decimal? PromotionalPrice { get; set; }

        // Quantidade disponível.
        // Se o produto tiver variações (tamanhos), é a soma do estoque delas.
        public int StockQuantity { get; set; } = 0;

        // Permite esconder o produto sem excluí-lo
        public bool IsActive { get; set; } = true;

        // Produto em destaque na vitrine
        public bool IsFeatured { get; set; } = false;

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedDate { get; set; }

        public DateTime? DeletionDate { get; set; }

        // Imagens do produto
        public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        // Tamanhos do produto (vazio = produto sem tamanho, usa StockQuantity direto)
        public ICollection<ProductVariant> Variants { get; set; } = new List<ProductVariant>();
    }
}
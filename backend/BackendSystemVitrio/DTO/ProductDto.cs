namespace BackendSystemVitrio.DTO
{
    public class CreateProductImageDto
    {
        public required string Url { get; set; }
        public int Order { get; set; } = 0;
    }

    public class CreateProductDto
    {
        public required int StoreId { get; set; }
        public int? CategoryId { get; set; }
        public required string Name { get; set; }

        // Opcional: se vier vazio, é gerado a partir do Name.
        public string? Slug { get; set; }

        public string? Description { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? PromotionalPrice { get; set; }
        public int StockQuantity { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public bool IsFeatured { get; set; } = false;
        public List<CreateProductImageDto>? Images { get; set; }
    }

    // Atualização completa (o formulário de edição manda todos os campos).
    // Images: se vier (mesmo vazia), substitui a lista inteira de imagens.
    public class UpdateProductDto
    {
        public int? CategoryId { get; set; }
        public required string Name { get; set; }
        public string? Slug { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? PromotionalPrice { get; set; }
        public int StockQuantity { get; set; }
        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }
        public List<CreateProductImageDto>? Images { get; set; }
    }

    public class ProductImageResponseDto
    {
        public int Id { get; set; }
        public required string Url { get; set; }
        public int Order { get; set; }
    }

    // Resumo da categoria, só o necessário pro card do produto no frontend
    public class ProductCategoryDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string? Slug { get; set; }
    }

    public class ProductResponseDto
    {
        public int Id { get; set; }
        public int StoreId { get; set; }

        public int? CategoryId { get; set; }
        public ProductCategoryDto? Category { get; set; }

        public required string Name { get; set; }
        public required string Slug { get; set; }
        public string? Description { get; set; }
        public string? Sku { get; set; }

        public decimal Price { get; set; }
        public decimal? PromotionalPrice { get; set; }

        public int StockQuantity { get; set; }

        public bool IsActive { get; set; }
        public bool IsFeatured { get; set; }

        public DateTime CreationDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public List<ProductImageResponseDto> Images { get; set; } = new();
    }
}

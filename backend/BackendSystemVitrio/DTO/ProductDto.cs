namespace BackendSystemVitrio.DTO
{
    public class CreateProductImageDto
    {
        public required string Url { get; set; }
        public int Order { get; set; } = 0;
    }

    // Tamanho enviado pelo formulário do lojista.
    // A ordem da lista define a ordem de exibição na vitrine.
    public class ProductVariantInputDto
    {
        public required string Size { get; set; }
        public int StockQuantity { get; set; } = 0;
    }

    public class ProductVariantResponseDto
    {
        public int Id { get; set; }
        public required string Size { get; set; }
        public int StockQuantity { get; set; }
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

        // Se vier com itens, o estoque do produto passa a ser a soma dos tamanhos
        // (StockQuantity acima é ignorado).
        public List<ProductVariantInputDto>? Variants { get; set; }

        // Cor desta peça (opcional). Ex: "Azul marinho" e "#1E3A8A".
        public string? ColorName { get; set; }
        public string? ColorHex { get; set; }

        // Liga este produto a outro como "mesma peça, outra cor".
        // null = produto não faz parte de nenhum grupo de cores.
        public int? ColorLinkedProductId { get; set; }
    }

    // Atualização completa (o formulário de edição manda todos os campos).
    // Images: se vier (mesmo vazia), substitui a lista inteira de imagens.
    // Variants: se vier (mesmo vazia), substitui os tamanhos. Vazia = produto sem tamanho.
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
        public List<ProductVariantInputDto>? Variants { get; set; }

        // Cor desta peça (opcional). Ex: "Azul marinho" e "#1E3A8A".
        public string? ColorName { get; set; }
        public string? ColorHex { get; set; }

        // Liga este produto a outro como "mesma peça, outra cor".
        // null = produto não faz parte de nenhum grupo de cores.
        public int? ColorLinkedProductId { get; set; }
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

        // Ativo, mas fora da vitrine porque a loja passou do limite de produtos do plano.
        public bool HiddenByPlan { get; set; }

        public string? ColorName { get; set; }
        public string? ColorHex { get; set; }
        public Guid? ColorGroupId { get; set; }

        public DateTime CreationDate { get; set; }
        public DateTime? UpdatedDate { get; set; }

        public List<ProductImageResponseDto> Images { get; set; } = new();

        public List<ProductVariantResponseDto> Variants { get; set; } = new();
    }
}
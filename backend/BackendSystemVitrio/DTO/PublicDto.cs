namespace BackendSystemVitrio.DTO
{
    // DTOs da vitrine pública: nunca expõem dados do dono (UserId, CPF etc).

    public class PublicStoreDto
    {
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? Phone { get; set; }
        // A loja aceita "pagar agora" pelo Mercado Pago no checkout.
        public bool OnlinePayment { get; set; }
        public required string PrimaryColor { get; set; }
        public required string SecondaryColor { get; set; }
        public required string TertiaryColor { get; set; }
    }

    public class PublicCategoryDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public int? ParentCategoryId { get; set; }
    }

    public class PublicProductDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public decimal? PromotionalPrice { get; set; }
        public int StockQuantity { get; set; }
        public bool IsFeatured { get; set; }

        // Produtos com o mesmo ColorGroupId são a mesma peça em outras cores.
        public string? ColorName { get; set; }
        public string? ColorHex { get; set; }
        public Guid? ColorGroupId { get; set; }

        public ProductCategoryDto? Category { get; set; }
        public List<ProductImageResponseDto> Images { get; set; } = new();

        // Tamanhos disponíveis (vazio = produto sem tamanho)
        public List<ProductVariantResponseDto> Variants { get; set; } = new();
    }
}
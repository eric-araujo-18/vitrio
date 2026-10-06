namespace BackendSystemVitrio.DTO
{
    public class StoreDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public string? Cnpj { get; set; }
        public string? Description { get; set; }
        public string? LogoUrl { get; set; }
        public string? Phone { get; set; }
        public required string PrimaryColor { get; set; }
        public required string SecondaryColor { get; set; }
        public required string TertiaryColor { get; set; }
        public bool IsActive { get; set; }
        public bool NotifyNewOrdersByEmail { get; set; }
        public DateTime CreationDate { get; set; }

        // Ativa, mas fora do ar porque passou do limite de lojas do plano atual.
        public bool BlockedByPlan { get; set; }

        // O lojista já tem tantas lojas ativas quanto o plano permite no ar: reativar uma
        // loja pausada só é possível trocando (POST /api/Store/{id}/go-online).
        public bool StoreLimitReached { get; set; }

        // Produtos visíveis por loja no plano atual (null = ilimitado).
        public int? MaxProductsPerStore { get; set; }
    }

    // Números da tela inicial do painel do lojista.
    public class StoreDashboardDto
    {
        public int TotalProducts { get; set; }
        public int ActiveProducts { get; set; }
        public int OutOfStockProducts { get; set; }
        public int TotalCategories { get; set; }
        public int PendingOrders { get; set; }
        public int OrdersLast30Days { get; set; }
        public decimal RevenueLast30Days { get; set; }
        public List<OrderSummaryDto> RecentOrders { get; set; } = new();
    }
}

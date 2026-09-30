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
        public DateTime CreationDate { get; set; }
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

using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.DTO
{
    public class PlanDto
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public required string Name { get; set; }
        public string? Description { get; set; }
        public decimal PriceMonthly { get; set; }
        public int MaxStores { get; set; }
        public int? MaxProductsPerStore { get; set; }
        public bool AllowsOnlinePayment { get; set; }
    }

    // Uso atual de cada loja, para mostrar "120 de 300 produtos"
    public class StoreUsageDto
    {
        public int StoreId { get; set; }
        public required string StoreName { get; set; }
        public int ProductCount { get; set; }
    }

    public class MySubscriptionDto
    {
        // Plano que está valendo agora (o grátis, se a assinatura estiver suspensa/cancelada)
        public required PlanDto Plan { get; set; }

        // Null = nunca assinou (está no grátis por padrão)
        public SubscriptionStatus? Status { get; set; }
        public DateTime? TrialEndsAt { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }

        public int StoreCount { get; set; }
        public List<StoreUsageDto> Stores { get; set; } = new();
    }

    // Admin: troca manual de plano (etapa 1, antes da cobrança automática)
    public class AdminSetPlanDto
    {
        public required string PlanCode { get; set; }
    }
}
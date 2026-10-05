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

    // O que fazer com um plano (o front só desenha isso)
    public class PlanOptionDto
    {
        public required PlanDto Plan { get; set; }
        public PlanAction Action { get; set; }
        public required string Label { get; set; }   // texto do botão / etiqueta
    }

    public class MySubscriptionDto
    {
        // Plano que está valendo agora (o grátis, se a assinatura estiver suspensa/cancelada)
        public required PlanDto Plan { get; set; }

        // Null = nunca assinou (está no grátis por padrão)
        public SubscriptionStatus? Status { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        public DateTime? CanceledAt { get; set; }
        public DateTime? PastDueSince { get; set; }

        // Pagamento aberto no Mercado Pago, ainda não confirmado
        public PlanDto? PendingPlan { get; set; }

        // Plano menor agendado para entrar no fim do período (CurrentPeriodEnd)
        public PlanDto? ScheduledPlan { get; set; }

        // Tem assinatura paga no Mercado Pago que pode ser cancelada
        public bool CanCancel { get; set; }

        // Um item por plano, na ordem de exibição
        public List<PlanOptionDto> Options { get; set; } = new();

        public int StoreCount { get; set; }
        public List<StoreUsageDto> Stores { get; set; } = new();
    }

    public class StartCheckoutDto
    {
        public required string PlanCode { get; set; }
    }

    public class CheckoutResultDto
    {
        // Link do Mercado Pago para o lojista pagar. Null quando a troca não precisa
        // de pagamento agora (downgrade agendado ou desistência dele).
        public string? CheckoutUrl { get; set; }
    }

    // Admin: troca manual de plano, sem cobrança
    public class AdminSetPlanDto
    {
        public required string PlanCode { get; set; }
    }
}
using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.DTO
{
    // ===== Painel do Admin =====

    public class AdminOverviewDto
    {
        public int Shopkeepers { get; set; }
        public int UnconfirmedShopkeepers { get; set; }
        public int Clients { get; set; }
        public int ActiveStores { get; set; }
        // Pedidos dos últimos 30 dias que chegaram à loja (sem os cancelados e os que esperam pagamento).
        public int OrdersLast30Days { get; set; }
        // Assinaturas pagas no Mercado Pago, por plano, e quanto somam por mês.
        public List<AdminPlanCountDto> PaidSubscriptions { get; set; } = new();
        public decimal MonthlyRecurringRevenue { get; set; }
    }

    public class AdminPlanCountDto
    {
        public required string PlanCode { get; set; }
        public required string PlanName { get; set; }
        public int Count { get; set; }
    }

    public class AdminUserDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Email { get; set; }
        public string? Phone { get; set; }
        public required string Role { get; set; }
        public bool EmailConfirmed { get; set; }
        public DateTime CreationDate { get; set; }

        // Plano que vale agora (o grátis, sem assinatura) e a situação da assinatura.
        public required string PlanCode { get; set; }
        public required string PlanName { get; set; }
        public SubscriptionStatus? SubscriptionStatus { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }
        // Plano definido pelo Admin, sem cobrança no Mercado Pago.
        public bool ManualPlan { get; set; }

        public List<AdminStoreDto> Stores { get; set; } = new();
    }

    public class AdminStoreDto
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Slug { get; set; }
        public bool IsActive { get; set; }
    }
}

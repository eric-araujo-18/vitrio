using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.Models
{
    // Assinatura do lojista (uma por usuário).
    // Lojista sem assinatura, ou com assinatura suspensa/cancelada, usa o plano grátis.
    public class Subscription
    {
        public int Id { get; set; }

        public required int UserId { get; set; }
        public User? User { get; set; }

        public required int PlanId { get; set; }
        public Plan? Plan { get; set; }

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        public DateTime? TrialEndsAt { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }   // até quando está paga
        public DateTime? CanceledAt { get; set; }

        // Id da assinatura no Mercado Pago (preenchido na etapa de cobrança)
        public string? GatewaySubscriptionId { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }
    }
}
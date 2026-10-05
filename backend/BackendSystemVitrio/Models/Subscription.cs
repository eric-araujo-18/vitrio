using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.Models
{
    // Assinatura do lojista (uma por usuário).
    // Lojista sem assinatura, ou com assinatura suspensa/cancelada, usa o plano grátis.
    //
    // As regras de troca de plano ficam todas em SubscriptionService.DecideAction.
    public class Subscription
    {
        public int Id { get; set; }

        public required int UserId { get; set; }
        public User? User { get; set; }

        // Plano que está valendo (pago)
        public required int PlanId { get; set; }
        public Plan? Plan { get; set; }

        public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;

        // Assinatura paga e ativa no Mercado Pago
        public string? GatewaySubscriptionId { get; set; }

        // Checkout aberto (assinatura nova ou upgrade) que ainda não foi pago
        public int? PendingPlanId { get; set; }
        public Plan? PendingPlan { get; set; }
        public string? PendingGatewaySubscriptionId { get; set; }

        // Quando esse checkout foi aberto. Não muda nas conferências periódicas (UpdatedDate
        // muda), por isso é ele que decide quando um checkout abandonado é descartado.
        public DateTime? PendingSince { get; set; }

        // Assinatura antiga (de antes de um upgrade) cujo cancelamento no Mercado Pago falhou.
        // A manutenção tenta de novo até conseguir, para o lojista nunca ser cobrado duas vezes.
        public string? GatewaySubscriptionIdToCancel { get; set; }

        // Plano menor que entra no fim do período atual (downgrade agendado)
        public int? ScheduledPlanId { get; set; }
        public Plan? ScheduledPlan { get; set; }

        // Quando a cobrança começou a falhar (para contar os dias de tolerância)
        public DateTime? PastDueSince { get; set; }

        public DateTime? TrialEndsAt { get; set; }
        public DateTime? CurrentPeriodEnd { get; set; }   // até quando está paga
        public DateTime? CanceledAt { get; set; }

        // Última vez que o estado foi conferido na API do Mercado Pago
        public DateTime? LastSyncedAt { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }
    }
}
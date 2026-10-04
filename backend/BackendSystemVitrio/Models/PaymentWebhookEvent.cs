namespace BackendSystemVitrio.Models
{
    // Registro de cada notificação recebida do gateway.
    // Serve para não processar o mesmo aviso duas vezes (o Mercado Pago reenvia
    // quando não recebe 200) e para investigar problemas depois.
    public class PaymentWebhookEvent
    {
        public int Id { get; set; }

        public required string Provider { get; set; }        // "mercadopago"
        public required string NotificationId { get; set; }  // id da notificação (único por provider)
        public required string Type { get; set; }            // ex: subscription_preapproval
        public string? Action { get; set; }
        public required string DataId { get; set; }          // id do recurso (assinatura, cobrança...)

        public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }            // null = ainda não processou com sucesso
        public string? Error { get; set; }
    }
}
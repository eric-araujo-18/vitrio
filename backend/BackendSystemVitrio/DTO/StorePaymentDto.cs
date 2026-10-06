using BackendSystemVitrio.Enum;

namespace BackendSystemVitrio.DTO
{
    // Situação do pagamento online de uma loja (tela de configurações da loja).
    public class StorePaymentStatusDto
    {
        // O servidor tem as credenciais do app (sem isso, ninguém consegue conectar).
        public bool Configured { get; set; }
        // O plano do lojista permite pagamento online (Plan.AllowsOnlinePayment).
        public bool PlanAllows { get; set; }
        public bool Connected { get; set; }
        // A vitrine oferece "pagar agora": servidor configurado, plano permite e conta conectada.
        public bool Available { get; set; }
        public string? MercadoPagoUserId { get; set; }
        // false = conta de teste (sandbox)
        public bool LiveMode { get; set; }
        public DateTime? ConnectedAt { get; set; }
        // Pedidos esperando pagamento (enquanto houver, a conta não pode ser desconectada).
        public int AwaitingPaymentOrders { get; set; }
    }

    public class StorePaymentConnectDto
    {
        // Página do Mercado Pago onde o lojista autoriza o Vitrio.
        public required string AuthorizationUrl { get; set; }
    }

    // Situação do pagamento de um pedido, para a vitrine (sem dados do cliente).
    public class OrderPaymentInfoDto
    {
        public required string Code { get; set; }
        public OrderStatus Status { get; set; }
        public OrderPaymentMethod PaymentMethod { get; set; }
        public OrderPaymentStatus PaymentStatus { get; set; }
        public decimal Total { get; set; }
        // Até quando dá para começar a pagar (30 min antes do prazo do pedido, pela validade do Pix).
        public DateTime? PaymentDeadline { get; set; }
        // Link para pagar, enquanto ainda dá para começar a pagar.
        public string? CheckoutUrl { get; set; }
        public string? StorePhone { get; set; }
    }
}

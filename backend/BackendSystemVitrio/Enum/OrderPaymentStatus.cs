namespace BackendSystemVitrio.Enum
{
    // Situação do pagamento online de um pedido (só vale para OrderPaymentMethod.Online).
    public enum OrderPaymentStatus
    {
        None = 0,      // pedido para combinar com a loja: sem pagamento online
        Pending = 1,   // esperando o cliente pagar no Mercado Pago
        Approved = 2,  // pago
        Refunded = 3,  // estornado (cancelamento do pedido pago ou pelo painel do Mercado Pago)
        Canceled = 4   // o prazo acabou ou o pedido foi cancelado antes de ser pago
    }
}

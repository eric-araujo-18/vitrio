namespace BackendSystemVitrio.Enum
{
    // Resultado do estorno de um pedido pago online, quando o lojista cancela.
    public enum RefundResult
    {
        Refunded,   // estornado no Mercado Pago (ou já estava)
        Manual,     // a conta conectada não alcança o pagamento (desconectada, acesso revogado ou
                    // outra conta): o pedido pode ser cancelado, e o lojista devolve pelo Mercado Pago
        Failed      // erro passageiro (Mercado Pago fora do ar...): não cancela, tenta de novo depois
    }
}

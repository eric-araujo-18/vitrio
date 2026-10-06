namespace BackendSystemVitrio.Models
{
    // Conta do Mercado Pago que a loja conectou (OAuth) para receber pagamentos online.
    // O dinheiro dos pedidos cai direto nessa conta; o Vitrio só cria as cobranças em nome dela.
    public class StorePaymentAccount
    {
        public int Id { get; set; }

        public required int StoreId { get; set; }
        public Store? Store { get; set; }

        // Id do usuário no Mercado Pago (user_id devolvido pelo OAuth).
        public required string MercadoPagoUserId { get; set; }

        // Tokens criptografados (PaymentTokenProtector). Nunca são devolvidos pela API.
        public required string AccessTokenEncrypted { get; set; }
        public required string RefreshTokenEncrypted { get; set; }
        public DateTime ExpiresAt { get; set; } // o access token vale ~180 dias; é renovado antes

        // false = credenciais de teste (sandbox)
        public bool LiveMode { get; set; }

        public DateTime CreationDate { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedDate { get; set; }
    }
}

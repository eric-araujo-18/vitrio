namespace BackendSystemVitrio.Services.Payments
{
    // Seção "MercadoPago" da configuração.
    // NUNCA coloque AccessToken nem WebhookSecret no appsettings.json versionado:
    // use "dotnet user-secrets" no desenvolvimento e variáveis de ambiente em produção.
    public class MercadoPagoOptions
    {
        public const string Section = "MercadoPago";

        // Access Token da aplicação (no sandbox: o de teste, começa com "TEST-" ou é do vendedor de teste)
        public string AccessToken { get; set; } = "";

        // "Assinatura secreta" das notificações (painel do app > Webhooks)
        public string WebhookSecret { get; set; } = "";

        // Para onde o Mercado Pago manda o lojista depois do pagamento.
        // O Mercado Pago recusa "localhost", então precisa ser uma URL pública (https).
        // - Desenvolvimento: URL do túnel da API + "/api/Subscription/return"
        //   (esse endpoint redireciona para FrontendReturnUrl).
        // - Produção: pode ser direto a página do front, ex: https://vitrio.com.br/menu/subscription
        public string BackUrl { get; set; } = "";

        // Página do front para onde /api/Subscription/return redireciona.
        // Ex. no desenvolvimento: http://localhost:3000/menu/subscription
        public string FrontendReturnUrl { get; set; } = "";

        // Só sandbox: e-mail do COMPRADOR de teste. O checkout da assinatura exige que
        // quem paga esteja logado com o mesmo e-mail do payer_email.
        public string? TestPayerEmail { get; set; }
    }
}
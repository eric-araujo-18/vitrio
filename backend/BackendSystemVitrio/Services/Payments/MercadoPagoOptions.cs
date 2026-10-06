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

        // Só sandbox: e-mail do usuário de teste que faz o papel do CLIENTE nos pedidos. Precisa ser
        // outra conta: o TestPayerEmail é o do lojista, que é quem recebe. Opcional.
        public string? TestOrderPayerEmail { get; set; }

        // ===== Pagamento online dos pedidos (marketplace: cada loja conecta a própria conta) =====
        // Sem ClientId, ClientSecret, TokenEncryptionKey e PublicApiUrl, a opção não aparece.

        // "Client ID" e "Client Secret" do app (painel do app > Credenciais), usados no OAuth.
        public string ClientId { get; set; } = "";
        public string ClientSecret { get; set; } = "";

        // Chave AES de 32 bytes em base64 que criptografa os tokens das lojas no banco.
        // Se ela mudar, as lojas precisam conectar de novo.
        public string TokenEncryptionKey { get; set; } = "";

        // Endereço público (https) da API. O Mercado Pago chama/redireciona para cá:
        // OAuth ({PublicApiUrl}/api/StorePayment/oauth/callback, cadastrado no painel do app),
        // notificações dos pagamentos e volta do checkout. No desenvolvimento: a URL do túnel.
        public string PublicApiUrl { get; set; } = "";

        // true = o OAuth devolve credenciais "TEST-", que levam ao checkout antigo do sandbox.
        // Com usuários de teste deixe false: a loja recebe o token de produção do vendedor de
        // teste (APP_USR-), como o AccessToken das assinaturas, e o checkout é o normal.
        public bool OAuthTestToken { get; set; }

        // Comissão do Vitrio sobre cada venda online, em % (0 = sem comissão).
        public decimal MarketplaceFeePercent { get; set; }

        // Por quantos minutos o pedido segura o estoque esperando o pagamento. Inclui os 30 minutos
        // que um Pix precisa valer depois de gerado (exigência do Mercado Pago): o cliente só pode
        // começar a pagar até 30 minutos antes do prazo (OrderPaymentService.CheckoutClosesAt).
        public int OrderPaymentMinutes { get; set; } = 60;

        // Com menos de 40 minutos sobraria pouco tempo para começar a pagar.
        public int EffectiveOrderPaymentMinutes => Math.Max(OrderPaymentMinutes, 40);

        public bool OnlinePaymentConfigured =>
            !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret) &&
            !string.IsNullOrWhiteSpace(TokenEncryptionKey) && !string.IsNullOrWhiteSpace(PublicApiUrl);
    }
}
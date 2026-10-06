using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.Payments
{
    // ===== Formatos da API do Mercado Pago usados no pagamento dos pedidos =====

    public class MpOAuthToken
    {
        public string AccessToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public long ExpiresIn { get; set; }              // segundos (~180 dias)
        public long UserId { get; set; }                 // conta do lojista no Mercado Pago
        public bool LiveMode { get; set; }               // false = credenciais de teste
    }

    public class MpPreferenceItem
    {
        public string? Id { get; set; }
        public string Title { get; set; } = "";
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string CurrencyId { get; set; } = "BRL";
        public string? PictureUrl { get; set; }
    }

    public class MpPreferenceRequest
    {
        public List<MpPreferenceItem> Items { get; set; } = new();
        public object? Payer { get; set; }
        public string ExternalReference { get; set; } = "";
        public string? NotificationUrl { get; set; }
        public object? BackUrls { get; set; }
        public string? AutoReturn { get; set; }
        public bool Expires { get; set; }
        public string? ExpirationDateFrom { get; set; }
        public string? ExpirationDateTo { get; set; }
        public string? DateOfExpiration { get; set; }    // vencimento do Pix criado por esta preferência
        public object? PaymentMethods { get; set; }
        public decimal? MarketplaceFee { get; set; }
        public object? Metadata { get; set; }
    }

    public class MpPreference
    {
        public string Id { get; set; } = "";
        public string? InitPoint { get; set; }
        public string? SandboxInitPoint { get; set; }
    }

    public class MpPayment
    {
        public long Id { get; set; }
        public string? Status { get; set; }              // pending, approved, in_process, rejected, cancelled, refunded, charged_back
        public string? StatusDetail { get; set; }
        public string? ExternalReference { get; set; }
        public decimal TransactionAmount { get; set; }
        public string? CurrencyId { get; set; }
        public DateTime? DateApproved { get; set; }
    }

    public class MpPaymentSearch
    {
        public List<MpPayment> Results { get; set; } = new();
    }

    public interface IMercadoPagoMarketplaceClient
    {
        string BuildAuthorizationUrl(string state);
        Task<MpOAuthToken> ExchangeCodeAsync(string code);
        Task<MpOAuthToken> RefreshTokenAsync(string refreshToken);
        Task<MpPreference> CreatePreferenceAsync(string accessToken, MpPreferenceRequest preference, string idempotencyKey);
        Task<MpPayment> GetPaymentAsync(string accessToken, string paymentId);
        Task<List<MpPayment>> SearchPaymentsAsync(string accessToken, string externalReference);
        Task RefundPaymentAsync(string accessToken, string paymentId, string idempotencyKey);
    }

    // Chamadas feitas EM NOME DA LOJA: usam o access token que ela autorizou pelo OAuth
    // (o dinheiro vai para a conta dela), não o token do Vitrio.
    public class MercadoPagoMarketplaceClient : IMercadoPagoMarketplaceClient
    {
        public const string OAuthCallbackPath = "/api/StorePayment/oauth/callback";

        private readonly HttpClient _http;
        private readonly MercadoPagoOptions _options;

        public MercadoPagoMarketplaceClient(HttpClient http, IOptions<MercadoPagoOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        private string RedirectUri => _options.PublicApiUrl.TrimEnd('/') + OAuthCallbackPath;

        // Domínio do Brasil: o genérico (auth.mercadopago.com) abre antes uma tela de escolha de
        // país, e o lojista não chega à autorização. Com o .com.br vai direto para o login.
        public string BuildAuthorizationUrl(string state)
            => "https://auth.mercadopago.com.br/authorization" +
               $"?client_id={Uri.EscapeDataString(_options.ClientId)}" +
               "&response_type=code&platform_id=mp" +
               $"&state={Uri.EscapeDataString(state)}" +
               $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}";

        public Task<MpOAuthToken> ExchangeCodeAsync(string code)
            => SendAsync<MpOAuthToken>(HttpMethod.Post, "oauth/token", null, new
            {
                client_id = _options.ClientId,
                client_secret = _options.ClientSecret,
                grant_type = "authorization_code",
                code,
                redirect_uri = RedirectUri,
                test_token = _options.OAuthTestToken ? "true" : "false",
            });

        public Task<MpOAuthToken> RefreshTokenAsync(string refreshToken)
            => SendAsync<MpOAuthToken>(HttpMethod.Post, "oauth/token", null, new
            {
                client_id = _options.ClientId,
                client_secret = _options.ClientSecret,
                grant_type = "refresh_token",
                refresh_token = refreshToken,
                test_token = _options.OAuthTestToken ? "true" : "false",
            });

        public Task<MpPreference> CreatePreferenceAsync(string accessToken, MpPreferenceRequest preference, string idempotencyKey)
            => SendAsync<MpPreference>(HttpMethod.Post, "checkout/preferences", accessToken, preference, idempotencyKey);

        public Task<MpPayment> GetPaymentAsync(string accessToken, string paymentId)
            => SendAsync<MpPayment>(HttpMethod.Get, $"v1/payments/{Uri.EscapeDataString(paymentId)}", accessToken);

        public async Task<List<MpPayment>> SearchPaymentsAsync(string accessToken, string externalReference)
            => (await SendAsync<MpPaymentSearch>(HttpMethod.Get,
                    $"v1/payments/search?external_reference={Uri.EscapeDataString(externalReference)}&sort=date_created&criteria=desc",
                    accessToken)).Results;

        // Sem corpo = estorno do valor total.
        public Task RefundPaymentAsync(string accessToken, string paymentId, string idempotencyKey)
            => SendAsync<JsonElement>(HttpMethod.Post, $"v1/payments/{Uri.EscapeDataString(paymentId)}/refunds", accessToken, new { }, idempotencyKey);

        private async Task<T> SendAsync<T>(HttpMethod method, string path, string? accessToken, object? body = null, string? idempotencyKey = null)
        {
            using var request = new HttpRequestMessage(method, path);
            if (accessToken is not null)
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            if (idempotencyKey is not null)
                request.Headers.Add("X-Idempotency-Key", idempotencyKey);
            if (body is not null)
                request.Content = JsonContent.Create(body, body.GetType(), options: MercadoPagoClient.Json);

            using var response = await _http.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new MercadoPagoException((int)response.StatusCode, content);

            return JsonSerializer.Deserialize<T>(content, MercadoPagoClient.Json)
                   ?? throw new MercadoPagoException((int)response.StatusCode, "Resposta vazia do Mercado Pago.");
        }
    }
}

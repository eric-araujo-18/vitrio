using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.Payments
{
    // ===== Formatos da API do Mercado Pago (só os campos que o Vitrio usa) =====

    public class MpAutoRecurring
    {
        public int Frequency { get; set; } = 1;
        public string FrequencyType { get; set; } = "months";
        public decimal TransactionAmount { get; set; }
        public string CurrencyId { get; set; } = "BRL";
    }

    public class MpPreapproval
    {
        public string Id { get; set; } = "";
        public string? Status { get; set; }              // pending, authorized, paused, cancelled
        public string? ExternalReference { get; set; }
        public string? InitPoint { get; set; }           // link do checkout para o lojista pagar
        public DateTime? NextPaymentDate { get; set; }
        public MpAutoRecurring? AutoRecurring { get; set; }
    }

    public class MpAuthorizedPaymentInfo
    {
        public long? Id { get; set; }
        public string? Status { get; set; }              // approved, rejected, ...
    }

    // Cada cobrança mensal de uma assinatura
    public class MpAuthorizedPayment
    {
        public long Id { get; set; }
        public string? PreapprovalId { get; set; }
        public string? Status { get; set; }              // scheduled, processed, recycling, cancelled
        public MpAuthorizedPaymentInfo? Payment { get; set; }
    }

    public interface IMercadoPagoClient
    {
        Task<MpPreapproval> CreatePreapprovalAsync(string reason, string externalReference, string payerEmail, decimal amount);
        Task<MpPreapproval> GetPreapprovalAsync(string id);
        Task<MpPreapproval> UpdatePreapprovalAmountAsync(string id, decimal amount);
        Task<MpPreapproval> CancelPreapprovalAsync(string id);
        Task<MpAuthorizedPayment> GetAuthorizedPaymentAsync(string id);
    }

    // Cliente HTTP simples para a API do Mercado Pago (sem SDK: fica claro o que é enviado).
    public class MercadoPagoClient : IMercadoPagoClient
    {
        public static readonly JsonSerializerOptions Json = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly HttpClient _http;
        private readonly MercadoPagoOptions _options;

        public MercadoPagoClient(HttpClient http, IOptions<MercadoPagoOptions> options)
        {
            _http = http;
            _options = options.Value;
        }

        public Task<MpPreapproval> CreatePreapprovalAsync(string reason, string externalReference, string payerEmail, decimal amount)
            => SendAsync<MpPreapproval>(HttpMethod.Post, "preapproval", new
            {
                reason,
                external_reference = externalReference,
                payer_email = payerEmail,
                auto_recurring = new MpAutoRecurring { TransactionAmount = amount },
                back_url = _options.BackUrl,
                // "pending": o lojista escolhe o meio de pagamento no checkout do Mercado Pago
                status = "pending",
            }, idempotencyKey: Guid.NewGuid().ToString());

        public Task<MpPreapproval> GetPreapprovalAsync(string id)
            => SendAsync<MpPreapproval>(HttpMethod.Get, $"preapproval/{Uri.EscapeDataString(id)}");

        public Task<MpPreapproval> UpdatePreapprovalAmountAsync(string id, decimal amount)
            => SendAsync<MpPreapproval>(HttpMethod.Put, $"preapproval/{Uri.EscapeDataString(id)}", new
            {
                auto_recurring = new { transaction_amount = amount, currency_id = "BRL" },
            });

        public Task<MpPreapproval> CancelPreapprovalAsync(string id)
            => SendAsync<MpPreapproval>(HttpMethod.Put, $"preapproval/{Uri.EscapeDataString(id)}", new { status = "cancelled" });

        public Task<MpAuthorizedPayment> GetAuthorizedPaymentAsync(string id)
            => SendAsync<MpAuthorizedPayment>(HttpMethod.Get, $"authorized_payments/{Uri.EscapeDataString(id)}");

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, string? idempotencyKey = null)
        {
            if (string.IsNullOrWhiteSpace(_options.AccessToken))
                throw new InvalidOperationException("MercadoPago:AccessToken não configurado.");

            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            if (idempotencyKey is not null)
                request.Headers.Add("X-Idempotency-Key", idempotencyKey);
            if (body is not null)
                request.Content = JsonContent.Create(body, options: Json);

            using var response = await _http.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new MercadoPagoException((int)response.StatusCode, content);

            return JsonSerializer.Deserialize<T>(content, Json)
                   ?? throw new MercadoPagoException((int)response.StatusCode, "Resposta vazia do Mercado Pago.");
        }
    }

    public class MercadoPagoException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }

        public MercadoPagoException(int statusCode, string responseBody)
            : base($"Mercado Pago respondeu {statusCode}: {responseBody}")
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }
    }
}
using System.Net;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.Captcha
{
    // Seção "Turnstile": captcha da Cloudflare (gratuito) no checkout da vitrine. Sem as duas chaves,
    // o checkout não pede captcha. Chaves de teste da Cloudflare para o desenvolvimento: site
    // 1x00000000000000000000AA e secret 1x0000000000000000000000000000000AA (sempre aprovam).
    public class TurnstileOptions
    {
        public const string Section = "Turnstile";

        // Chave pública do widget (vai para a vitrine em PublicStoreDto.CaptchaSiteKey).
        public string SiteKey { get; set; } = "";

        // Chave secreta, usada só aqui para conferir a resposta. Nunca vai para o navegador.
        public string SecretKey { get; set; } = "";

        public bool Enabled => !string.IsNullOrWhiteSpace(SiteKey) && !string.IsNullOrWhiteSpace(SecretKey);
    }

    // Confere na Cloudflare a resposta do captcha que o navegador mandou. Cada resposta vale uma
    // vez só e por 5 minutos.
    public class TurnstileVerifier
    {
        private readonly HttpClient _http;
        private readonly TurnstileOptions _options;
        private readonly ILogger<TurnstileVerifier> _logger;

        public TurnstileVerifier(HttpClient http, IOptions<TurnstileOptions> options, ILogger<TurnstileVerifier> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        public bool Enabled => _options.Enabled;
        public string? SiteKey => _options.Enabled ? _options.SiteKey : null;

        public async Task<bool> VerifyAsync(string? token, IPAddress? remoteIp, CancellationToken cancellationToken = default)
        {
            if (!_options.Enabled)
                return true;

            // A Cloudflare documenta respostas de até 2048 caracteres.
            if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
                return false;

            var form = new Dictionary<string, string>
            {
                ["secret"] = _options.SecretKey,
                ["response"] = token,
            };
            if (remoteIp is not null)
                form["remoteip"] = remoteIp.ToString();

            try
            {
                using var response = await _http.PostAsync("turnstile/v0/siteverify", new FormUrlEncodedContent(form), cancellationToken);
                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<SiteVerifyResponse>(cancellationToken);
                if (result?.Success == true)
                    return true;

                _logger.LogInformation("Captcha recusado no checkout: {Errors}", string.Join(", ", result?.ErrorCodes ?? []));
                return false;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
            {
                // Cloudflare fora do ar: deixa o pedido passar (o limite por IP continua valendo) em
                // vez de a loja parar de vender por causa do captcha.
                _logger.LogWarning(ex, "Não foi possível conferir o captcha na Cloudflare; o pedido seguiu sem a conferência");
                return true;
            }
        }

        private sealed class SiteVerifyResponse
        {
            [JsonPropertyName("success")]
            public bool Success { get; set; }

            [JsonPropertyName("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}

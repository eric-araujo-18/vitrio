using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace BackendSystemVitrio.Services.Email
{
    // Envia pela API HTTP do Resend. Para trocar de provedor, basta outra implementação
    // de IEmailSender registrada no Program.cs.
    public class ResendEmailSender : IEmailSender
    {
        private readonly HttpClient _http;
        private readonly EmailOptions _options;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<ResendEmailSender> _logger;

        public ResendEmailSender(
            HttpClient http,
            IOptions<EmailOptions> options,
            IHostEnvironment environment,
            ILogger<ResendEmailSender> logger)
        {
            _http = http;
            _options = options.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task SendAsync(string to, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(_options.ResendApiKey))
            {
                // Só no desenvolvimento: mostra o e-mail no log para dar para testar sem provedor.
                // Em produção isso vazaria links de redefinição de senha nos logs, então falha.
                if (!_environment.IsDevelopment())
                    throw new InvalidOperationException("Envio de e-mail não configurado (Email:ResendApiKey).");

                _logger.LogWarning(
                    "E-mail NÃO enviado (Email:ResendApiKey vazio). Para: {To} | Assunto: {Subject}{NewLine}{Html}",
                    to, subject, Environment.NewLine, html);
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "emails")
            {
                Content = JsonContent.Create(new { from = _options.From, to = new[] { to }, subject, html }),
            };
            request.Headers.Authorization = new("Bearer", _options.ResendApiKey);

            using var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Resend recusou o e-mail ({(int)response.StatusCode}): {body}");
            }
        }
    }
}

using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using BackendSystemVitrio.Models;
using BackendSystemVitrio.Services.Email;

namespace BackendSystemVitrio.Services.AuthService
{
    // Link de confirmação do e-mail do lojista. O token não fica no banco: ele leva o id do usuário,
    // o prazo e um resumo do e-mail, assinados com uma chave derivada da Jwt:Key. Trocar o e-mail
    // invalida os links antigos (o resumo deixa de bater); trocar a Jwt:Key também.
    public class EmailConfirmation
    {
        public const int ValidDays = 3;
        // Intervalo mínimo entre dois e-mails de confirmação para a mesma conta.
        public static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(2);
        // Lojista que não confirma em tantos dias tem o cadastro apagado (DatabaseCleanup), para o
        // e-mail e o CPF não ficarem presos por quem usou dados de outra pessoa.
        public const int UnconfirmedAccountDays = 7;

        private readonly byte[] _key;
        private readonly string _frontendUrl;
        private readonly EmailQueue _emailQueue;
        private readonly ILogger<EmailConfirmation> _logger;

        public EmailConfirmation(IConfiguration configuration, EmailQueue emailQueue, ILogger<EmailConfirmation> logger)
        {
            _key = SHA256.HashData(Encoding.UTF8.GetBytes("vitrio:email-confirmation:" + configuration["Jwt:Key"]));
            _frontendUrl = (configuration["App:FrontendUrl"] ?? "http://localhost:3000").TrimEnd('/');
            _emailQueue = emailQueue;
            _logger = logger;
        }

        // Põe o e-mail na fila e marca o envio (quem chama salva o usuário).
        public void Send(User user)
        {
            var link = $"{_frontendUrl}/auth/confirm-email?token={Uri.EscapeDataString(CreateToken(user))}";
            if (_emailQueue.TryEnqueue(new EmailMessage(user.Email, "Confirme seu e-mail - Vitrio", BuildEmail(user.Name, link))))
                user.EmailConfirmationSentAt = DateTime.UtcNow;
            else
                _logger.LogError("Fila de e-mails cheia: confirmação de e-mail do usuário {UserId} não foi enviada", user.Id);
        }

        public string CreateToken(User user)
        {
            var expires = DateTimeOffset.UtcNow.AddDays(ValidDays).ToUnixTimeSeconds();
            var payload = $"{user.Id}.{expires}.{EmailTag(user.Email)}";
            return $"{payload}.{Sign(payload)}";
        }

        // Id do usuário e resumo do e-mail quando o token é válido e não venceu (quem chama confere,
        // com MatchesEmail, se ainda é o e-mail atual). Null para token alterado, vencido ou malformado.
        public (int UserId, string EmailTag)? ReadToken(string? token)
        {
            var parts = token?.Split('.');
            if (parts is not { Length: 4 })
                return null;

            var payload = $"{parts[0]}.{parts[1]}.{parts[2]}";
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Sign(payload)), Encoding.ASCII.GetBytes(parts[3])))
                return null;

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var userId) ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var expires) ||
                DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expires)
                return null;

            return (userId, parts[2]);
        }

        public static bool MatchesEmail(string emailTag, string email)
            => CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(emailTag), Encoding.ASCII.GetBytes(EmailTag(email)));

        // Resumo do e-mail: o link não carrega o endereço em si.
        private static string EmailTag(string email)
            => Base64Url(SHA256.HashData(Encoding.UTF8.GetBytes(email.ToLowerInvariant()))[..12]);

        private string Sign(string payload) => Base64Url(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));

        private static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');

        private static string BuildEmail(string name, string link)
        {
            var safeName = WebUtility.HtmlEncode(name);
            var safeLink = WebUtility.HtmlEncode(link);
            return $"""
                <div style="font-family:Arial,sans-serif;max-width:480px;margin:0 auto;color:#111827">
                  <h2 style="margin-bottom:8px">Confirme seu e-mail</h2>
                  <p>Olá, {safeName}.</p>
                  <p>Confirme que este e-mail é seu para criar lojas, assinar planos e receber os avisos de pedidos na Vitrio.</p>
                  <p style="margin:28px 0">
                    <a href="{safeLink}" style="background:#2563eb;color:#fff;padding:12px 20px;border-radius:8px;text-decoration:none;font-weight:bold">Confirmar e-mail</a>
                  </p>
                  <p style="color:#6b7280;font-size:14px">O link vale por {ValidDays} dias. Se não foi você que criou a conta, é só ignorar
                  este e-mail: cadastros não confirmados são apagados em {UnconfirmedAccountDays} dias.</p>
                </div>
                """;
        }
    }
}

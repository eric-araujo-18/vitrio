using BackendSystemVitrio.Data;
using BackendSystemVitrio.Enum;
using BackendSystemVitrio.Services.AuthService;
using Microsoft.EntityFrameworkCore;

namespace BackendSystemVitrio.Services.Cleanup
{
    // Apaga registros que não servem para mais nada e só fariam as tabelas crescerem.
    public class DatabaseCleanup
    {
        // O Mercado Pago reenvia uma notificação por alguns dias no máximo; depois disso o
        // registro só serve para investigar problemas antigos.
        private const int WebhookEventRetentionDays = 90;

        private readonly AppDbContext _context;
        private readonly ILogger<DatabaseCleanup> _logger;

        public DatabaseCleanup(AppDbContext context, ILogger<DatabaseCleanup> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;

            // Links de "esqueci minha senha" vencidos (inclusive os de e-mails que falharam).
            var resetTokens = await _context.PasswordResetToken
                .Where(t => t.ExpiresAt < now)
                .ExecuteDeleteAsync(cancellationToken);

            // Sessões vencidas. As revogadas ainda dentro da validade ficam até vencer.
            var refreshTokens = await _context.RefreshToken
                .Where(t => t.ExpiresAt < now)
                .ExecuteDeleteAsync(cancellationToken);

            var webhookEvents = await _context.PaymentWebhookEvent
                .Where(e => e.ReceivedAt < now.AddDays(-WebhookEventRetentionDays))
                .ExecuteDeleteAsync(cancellationToken);

            // Cadastros de lojista que nunca confirmaram o e-mail (nem depois do último link enviado),
            // sem loja e sem assinatura: libera o e-mail e o CPF de quem usou dados de outra pessoa.
            var unconfirmedCutoff = now.AddDays(-EmailConfirmation.UnconfirmedAccountDays);
            var unconfirmedUsers = await _context.User
                .Where(u => u.Role == Role.Shopkeeper && u.EmailConfirmedAt == null &&
                            u.CreationDate < unconfirmedCutoff &&
                            (u.EmailConfirmationSentAt == null || u.EmailConfirmationSentAt < unconfirmedCutoff) &&
                            !u.Stores.Any() &&
                            !_context.Subscription.Any(s => s.UserId == u.Id))
                .ExecuteDeleteAsync(cancellationToken);

            if (resetTokens + refreshTokens + webhookEvents + unconfirmedUsers > 0)
                _logger.LogInformation(
                    "Limpeza do banco: {ResetTokens} links de senha, {RefreshTokens} sessões, {WebhookEvents} notificações antigas " +
                    "e {UnconfirmedUsers} cadastros não confirmados apagados",
                    resetTokens, refreshTokens, webhookEvents, unconfirmedUsers);
        }
    }
}

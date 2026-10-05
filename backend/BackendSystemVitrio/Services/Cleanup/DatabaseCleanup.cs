using BackendSystemVitrio.Data;
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

            if (resetTokens + refreshTokens + webhookEvents > 0)
                _logger.LogInformation(
                    "Limpeza do banco: {ResetTokens} links de senha, {RefreshTokens} sessões e {WebhookEvents} notificações antigas apagados",
                    resetTokens, refreshTokens, webhookEvents);
        }
    }
}

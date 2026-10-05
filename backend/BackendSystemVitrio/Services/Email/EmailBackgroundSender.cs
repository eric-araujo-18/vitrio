namespace BackendSystemVitrio.Services.Email
{
    // Envia, em segundo plano, os e-mails colocados na EmailQueue.
    public class EmailBackgroundSender : BackgroundService
    {
        private readonly EmailQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailBackgroundSender> _logger;

        public EmailBackgroundSender(EmailQueue queue, IServiceScopeFactory scopeFactory, ILogger<EmailBackgroundSender> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var message in _queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    // O IEmailSender usa um HttpClient do factory: um escopo por envio.
                    using var scope = _scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<IEmailSender>()
                        .SendAsync(message.To, message.Subject, message.Html);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Falha ao enviar e-mail \"{Subject}\"", message.Subject);
                }
            }
        }
    }
}

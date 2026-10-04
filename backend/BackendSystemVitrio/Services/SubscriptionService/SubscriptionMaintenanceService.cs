namespace BackendSystemVitrio.Services.SubscriptionService
{
    // Roda em segundo plano enquanto a API está no ar:
    // suspende assinaturas que passaram da tolerância e confere checkouts pendentes.
    public class SubscriptionMaintenanceService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SubscriptionMaintenanceService> _logger;

        public SubscriptionMaintenanceService(IServiceScopeFactory scopeFactory, ILogger<SubscriptionMaintenanceService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Espera a aplicação subir antes da primeira rodada
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    // DbContext é "scoped": cria um escopo novo a cada rodada.
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();
                    await service.RunMaintenanceAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro na manutenção das assinaturas");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
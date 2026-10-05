namespace BackendSystemVitrio.Services.SubscriptionService
{
    // Roda em segundo plano enquanto a API está no ar:
    // - a cada minuto: confere os checkouts abertos há pouco (o plano muda logo depois do
    //   pagamento mesmo sem webhook e sem nenhuma tela aberta);
    // - a cada 30 minutos: manutenção completa (suspende quem passou da tolerância, descarta
    //   checkouts abandonados, reconfere assinaturas pagas...).
    public class SubscriptionMaintenanceService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private const int FullMaintenanceEvery = 30; // em rodadas de 1 minuto

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
            var round = 0;
            do
            {
                try
                {
                    // DbContext é "scoped": cria um escopo novo a cada rodada.
                    using var scope = _scopeFactory.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ISubscriptionService>();

                    if (round % FullMaintenanceEvery == 0)
                        await service.RunMaintenanceAsync();
                    else
                        await service.SyncRecentCheckoutsAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro na manutenção das assinaturas");
                }
                round++;
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}

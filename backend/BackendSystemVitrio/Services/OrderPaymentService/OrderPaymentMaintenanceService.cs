using BackendSystemVitrio.Services.StorePaymentService;

namespace BackendSystemVitrio.Services.OrderPaymentService
{
    // Roda em segundo plano enquanto a API está no ar:
    // - a cada minuto: confere os pedidos esperando pagamento e cancela os que passaram do prazo
    //   (o estoque volta), mesmo que nenhuma notificação do Mercado Pago chegue;
    // - a cada hora: renova os tokens das lojas que vencem nos próximos 30 dias.
    public class OrderPaymentMaintenanceService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
        private const int RefreshTokensEvery = 60; // em rodadas de 1 minuto

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OrderPaymentMaintenanceService> _logger;

        public OrderPaymentMaintenanceService(IServiceScopeFactory scopeFactory, ILogger<OrderPaymentMaintenanceService> logger)
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
                // DbContext é "scoped": cria um escopo novo a cada rodada.
                using var scope = _scopeFactory.CreateScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<IOrderPaymentService>().RunMaintenanceAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro na manutenção dos pagamentos de pedidos");
                }

                if (round % RefreshTokensEvery == 0)
                {
                    try
                    {
                        await scope.ServiceProvider.GetRequiredService<IStorePaymentService>().RefreshExpiringTokensAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erro ao renovar os tokens do Mercado Pago das lojas");
                    }
                }
                round++;
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}

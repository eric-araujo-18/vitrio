namespace BackendSystemVitrio.Services.Cleanup
{
    // Roda em segundo plano enquanto a API está no ar: a cada 6 horas apaga do banco os
    // registros vencidos (DatabaseCleanup) e do Cloudinary as imagens sem uso (CloudinaryCleanup).
    public class CleanupService : BackgroundService
    {
        private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CleanupService> _logger;

        public CleanupService(IServiceScopeFactory scopeFactory, ILogger<CleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Espera a aplicação subir antes da primeira rodada
            await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                // DbContext é "scoped": cria um escopo novo a cada rodada.
                using var scope = _scopeFactory.CreateScope();

                // Uma limpeza falhar não impede a outra.
                try
                {
                    await scope.ServiceProvider.GetRequiredService<DatabaseCleanup>().RunAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erro na limpeza do banco");
                }

                try
                {
                    await scope.ServiceProvider.GetRequiredService<CloudinaryCleanup>().RunAsync(stoppingToken);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(ex, "Erro na limpeza das imagens do Cloudinary");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}

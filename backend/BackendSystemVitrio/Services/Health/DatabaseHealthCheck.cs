using BackendSystemVitrio.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BackendSystemVitrio.Services.Health
{
    // GET /health: a API responde e o banco aceita conexão. É o endereço que a hospedagem e o
    // monitor de disponibilidade (UptimeRobot, Better Stack...) consultam.
    public class DatabaseHealthCheck : IHealthCheck
    {
        private readonly AppDbContext _context;

        public DatabaseHealthCheck(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy()
                    : HealthCheckResult.Unhealthy("Sem conexão com o banco.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Sem conexão com o banco.", ex);
            }
        }
    }
}

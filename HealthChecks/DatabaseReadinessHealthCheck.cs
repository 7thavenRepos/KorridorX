using KorridorX.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace KorridorX.HealthChecks;

public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseReadinessHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);

            return canConnect
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL could not be reached.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "PostgreSQL readiness check failed.",
                ex);
        }
    }
}

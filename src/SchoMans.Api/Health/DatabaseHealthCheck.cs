using Microsoft.Extensions.Diagnostics.HealthChecks;
using SchoMans.Infrastructure.Persistence;

namespace SchoMans.Api.Health;

public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await db.Database.CanConnectAsync(cancellationToken);
        return canConnect ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("SQL Server is not reachable.");
    }
}

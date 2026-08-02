using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace EnterpriseAssetLifecycle.Infrastructure;

public sealed class DatabaseHealthCheck(AssetDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default) =>
        await db.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
            : HealthCheckResult.Unhealthy("PostgreSQL is unreachable.");
}


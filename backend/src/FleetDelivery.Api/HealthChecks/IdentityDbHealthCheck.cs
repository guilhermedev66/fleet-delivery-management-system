using FleetDelivery.Modules.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FleetDelivery.Api.HealthChecks;

/// <summary>Readiness check backing <c>/health/ready</c>: can we actually reach Postgres via <see cref="IdentityDbContext"/>?</summary>
public sealed class IdentityDbHealthCheck(IdentityDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("Postgres (identity schema) reachable.")
            : HealthCheckResult.Unhealthy("Cannot connect to Postgres.");
    }
}

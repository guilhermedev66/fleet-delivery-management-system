using FleetDelivery.Modules.Shipments.Infrastructure.Messaging;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace FleetDelivery.Api.HealthChecks;

/// <summary>Readiness check backing <c>/health/ready</c>: can we actually open a channel on the broker via <see cref="RabbitMqConnectionProvider"/>? Never logs/exposes the connection's credentials — only the resulting healthy/unhealthy outcome.</summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnectionProvider connectionProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var channel = await connectionProvider.CreateChannelAsync(cancellationToken);

            return HealthCheckResult.Healthy("RabbitMQ reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to RabbitMQ.", ex);
        }
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Messaging;

/// <summary>
/// Thin polling loop — all the actual claim/publish/retry logic lives in
/// <see cref="OutboxBatchProcessor"/>, resolved fresh from a new DI scope
/// each tick (it depends on <c>ShipmentsDbContext</c>, which is scoped).
/// Uses <see cref="PeriodicTimer"/> rather than a raw <c>while</c> +
/// <c>Task.Delay</c> loop so a slow batch can't cause overlapping ticks, and
/// respects <paramref name="stoppingToken"/> throughout — including inside
/// the batch's own DB calls — so shutdown doesn't wait out a full poll
/// interval or abandon an in-flight transaction uncommitted-but-unaborted.
///
/// Always registered (see <c>AddShipmentsModule</c>) — <see cref="OutboxPublisherOptions.PublisherEnabled"/>
/// is checked here, inside <see cref="ExecuteAsync"/>, via the properly
/// DI-resolved <see cref="IOptions{TOptions}"/> rather than at
/// registration time via raw <c>IConfiguration</c>. That's not a style
/// preference: <c>IConfiguration</c> read at the top of <c>Program.cs</c>
/// runs before <c>WebApplicationFactory</c>'s test config overrides are
/// merged in (same reason the CORS/rate-limiter policies in
/// <c>Program.cs</c> read their config lazily too), so a registration-time
/// check would silently miss <c>ShipmentsApiFactory</c>'s override and
/// start this service against a RabbitMQ broker that test host doesn't
/// have — see the doc comment on <c>DispatchBoardConsumerHostedService</c>
/// for the failure mode this caused there.
/// </summary>
public sealed class OutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxPublisherOptions> options,
    ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private readonly OutboxPublisherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.PublisherEnabled)
        {
            logger.LogInformation("Outbox publisher disabled (Outbox:PublisherEnabled=false) — not starting.");

            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));

        logger.LogInformation(
            "Outbox publisher started (poll interval {PollIntervalSeconds}s, batch size {BatchSize}).",
            _options.PollIntervalSeconds, _options.BatchSize);

        try
        {
            do
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var processor = scope.ServiceProvider.GetRequiredService<OutboxBatchProcessor>();

                    var result = await processor.ProcessBatchAsync(stoppingToken);

                    if (result.Claimed > 0)
                    {
                        logger.LogInformation(
                            "Outbox batch complete: {Claimed} claimed, {Published} published, {Failed} failed.",
                            result.Claimed, result.Published, result.Failed);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    // Shutting down mid-batch — fall through to the loop
                    // condition below, which exits cleanly.
                }
                catch (Exception ex)
                {
                    // A broker outage, a DB outage, or anything else that
                    // escaped OutboxBatchProcessor's own per-row try/catch —
                    // log and keep polling rather than letting one bad tick
                    // kill the whole hosted service for the process's
                    // lifetime.
                    logger.LogError(ex, "Outbox publisher batch failed unexpectedly; will retry next poll.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on graceful shutdown.
        }

        logger.LogInformation("Outbox publisher stopped.");
    }
}

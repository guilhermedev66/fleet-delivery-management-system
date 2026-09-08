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
/// </summary>
public sealed class OutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<OutboxPublisherOptions> options,
    ILogger<OutboxPublisherHostedService> logger) : BackgroundService
{
    private readonly OutboxPublisherOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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

using FleetDelivery.BuildingBlocks.Messaging;
using FleetDelivery.Modules.Shipments.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Messaging;

/// <summary>
/// Claims and publishes one batch of unprocessed <see cref="OutboxMessage"/>
/// rows. Extracted from <see cref="OutboxPublisherHostedService"/> so it's
/// directly callable/testable (including concurrently, from multiple scopes
/// at once, to prove the claiming strategy is actually safe) without needing
/// a running background loop.
///
/// Claiming strategy: <c>SELECT ... FOR UPDATE SKIP LOCKED</c> inside an
/// explicit transaction. Two processors (whether two threads in this
/// process or the same code running in two app instances) racing the same
/// poll never claim the same row — the second one's SELECT simply skips
/// whatever the first has already locked, rather than blocking on it or
/// double-claiming it. No separate lease/claimed-by column needed: the
/// Postgres row lock *is* the claim, and it's automatically released if the
/// process crashes mid-batch (the transaction never commits), so a crash
/// between claiming and publishing just leaves the row to be claimed again
/// next poll — never a stuck "permanently claimed" row.
/// </summary>
public sealed class OutboxBatchProcessor(
    ShipmentsDbContext dbContext,
    IIntegrationEventPublisher publisher,
    IOptions<OutboxPublisherOptions> options,
    ILogger<OutboxBatchProcessor> logger)
{
    private readonly OutboxPublisherOptions _options = options.Value;

    public async Task<OutboxBatchResult> ProcessBatchAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Outbox batch started.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;

        // The schema-qualified table name is a compile-time constant, never
        // user input, so splicing it directly into the SQL text is safe.
        // `now` and the batch size are NOT spliced in — they're bound as
        // real ADO parameters via FromSqlRaw's {0}/{1} placeholders (that
        // substitution happens inside EF Core, not the C# string
        // interpolation below, which is why they're written as the escaped
        // literal "{{0}}"/"{{1}}" here).
        var schemaQualifiedTable = $"\"{ShipmentsDbContext.Schema}\".\"outbox_messages\"";

        var sql = "SELECT * FROM " + schemaQualifiedTable +
            " WHERE processed_on IS NULL AND (next_attempt_on IS NULL OR next_attempt_on <= {0})" +
            " ORDER BY occurred_on LIMIT {1} FOR UPDATE SKIP LOCKED";

        var claimed = await dbContext.OutboxMessages
            .FromSqlRaw(sql, now, _options.BatchSize)
            .ToListAsync(cancellationToken);

        if (claimed.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);

            return new OutboxBatchResult(0, 0, 0);
        }

        logger.LogInformation("Outbox batch claimed {Count} row(s) for publishing.", claimed.Count);

        var published = 0;
        var failed = 0;

        foreach (var message in claimed)
        {
            var routingKey = ShipmentIntegrationEventRoutingKeys.For(message.Type);

            if (routingKey == ShipmentIntegrationEventRoutingKeys.UnknownRoutingKey)
            {
                logger.LogWarning(
                    "Outbox message {MessageId} has event type {Type}, which has no routing-key mapping — publishing to {RoutingKey} instead of a specific topic. Add it to ShipmentIntegrationEventRoutingKeys.",
                    message.Id, message.Type, routingKey);
            }

            try
            {
                await publisher.PublishAsync(routingKey, message, cancellationToken);

                message.ProcessedOn = DateTimeOffset.UtcNow;
                message.Error = null;
                published++;

                logger.LogInformation(
                    "Published outbox message {MessageId} ({Type}) to routing key {RoutingKey}.",
                    message.Id, message.Type, routingKey);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.AttemptCount++;
                message.Error = ex.Message;
                message.NextAttemptOn = DateTimeOffset.UtcNow + Backoff(message.AttemptCount, _options.MaxBackoffSeconds);
                failed++;

                logger.LogWarning(
                    ex,
                    "Failed to publish outbox message {MessageId} ({Type}) — attempt {AttemptCount}, retrying at {NextAttemptOn}.",
                    message.Id, message.Type, message.AttemptCount, message.NextAttemptOn);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OutboxBatchResult(claimed.Count, published, failed);
    }

    /// <summary>Exponential backoff capped at <paramref name="maxBackoffSeconds"/> — a row that keeps failing is retried at most that often, never abandoned entirely (see the doc comment on <see cref="OutboxMessage.NextAttemptOn"/>).</summary>
    private static TimeSpan Backoff(int attemptCount, int maxBackoffSeconds)
    {
        const int baseDelaySeconds = 2;
        var exponentialSeconds = baseDelaySeconds * Math.Pow(2, Math.Min(attemptCount, 16));

        return TimeSpan.FromSeconds(Math.Min(exponentialSeconds, maxBackoffSeconds));
    }
}

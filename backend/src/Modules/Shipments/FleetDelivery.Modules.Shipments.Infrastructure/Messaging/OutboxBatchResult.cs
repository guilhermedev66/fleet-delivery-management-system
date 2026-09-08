namespace FleetDelivery.Modules.Shipments.Infrastructure.Messaging;

/// <summary>Outcome of one <see cref="OutboxBatchProcessor.ProcessBatchAsync"/> call — what the hosted service logs, and what tests assert against.</summary>
public sealed record OutboxBatchResult(int Claimed, int Published, int Failed);

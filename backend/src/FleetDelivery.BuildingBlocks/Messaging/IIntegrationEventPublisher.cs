namespace FleetDelivery.BuildingBlocks.Messaging;

/// <summary>
/// Publishes one outbox row to the message broker. The caller (module
/// Infrastructure) supplies the routing key — routing-key derivation is
/// module-specific knowledge (which event types exist, how they map to
/// topics), while this abstraction stays a generic "publish to the broker"
/// concern so it isn't coupled to any one module's event catalogue.
/// Implementations must throw on failure (never swallow) — the caller
/// (<c>OutboxBatchProcessor</c>) is what decides how a failure affects the
/// outbox row's retry state.
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(string routingKey, OutboxMessage message, CancellationToken cancellationToken = default);
}

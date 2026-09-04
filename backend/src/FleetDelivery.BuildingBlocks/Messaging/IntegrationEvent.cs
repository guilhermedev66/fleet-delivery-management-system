namespace FleetDelivery.BuildingBlocks.Messaging;

/// <summary>
/// Base type for integration events: the ones that cross module or process
/// boundaries over RabbitMQ (e.g. <c>ShipmentCreated</c>, <c>DriverAssigned</c>),
/// as opposed to in-process <see cref="Domain.IDomainEvent"/>s. Carried inside
/// the RabbitMQ envelope <c>{ messageId, type, occurredAt, correlationId, data }</c>.
/// </summary>
public abstract class IntegrationEvent
{
    protected IntegrationEvent(Guid correlationId)
    {
        MessageId = Guid.NewGuid();
        OccurredOn = DateTimeOffset.UtcNow;
        CorrelationId = correlationId;
    }

    public Guid MessageId { get; init; }

    public DateTimeOffset OccurredOn { get; init; }

    /// <summary>Correlates this event with the originating request/message across module and process boundaries.</summary>
    public Guid CorrelationId { get; init; }
}

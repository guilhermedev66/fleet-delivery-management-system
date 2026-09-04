namespace FleetDelivery.BuildingBlocks.Messaging;

/// <summary>
/// A row in a module's outbox table. Written in the same transaction/
/// <c>SaveChanges</c> call as the domain state change it describes, then
/// picked up by an <c>OutboxPublisher</c> background service and published
/// to RabbitMQ. <see cref="ProcessedOn"/> is set only after a successful
/// publish (ack) — a crash between commit and publish simply leaves the row
/// for the next poll, never a silent loss.
/// </summary>
public sealed class OutboxMessage
{
    public required Guid Id { get; init; }

    /// <summary>Assembly-qualified or well-known type name of the integration event.</summary>
    public required string Type { get; init; }

    /// <summary>JSON-serialized payload of the integration event.</summary>
    public required string Content { get; init; }

    public required DateTimeOffset OccurredOn { get; init; }

    public DateTimeOffset? ProcessedOn { get; set; }

    /// <summary>Last publish error, if any. Populated on failure, left null on success.</summary>
    public string? Error { get; set; }
}

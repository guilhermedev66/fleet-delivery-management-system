namespace FleetDelivery.BuildingBlocks.Messaging;

/// <summary>
/// Shape of an inbox row, used by consumers to guarantee idempotent
/// processing: a consumer inserts one of these in the *same transaction* as
/// its side effect. Redelivery of the same message to the same consumer
/// then hits a unique constraint violation and is treated as a no-op ack.
///
/// The conceptual key is the pair (<see cref="Id"/>, <see cref="ConsumerName"/>).
/// This class only describes the row shape — idempotency is actually
/// enforced by a DB-level unique constraint on that pair (configured by
/// each module's EF Core mapping), not by anything in this type itself.
/// </summary>
public sealed class InboxMessage
{
    /// <summary>The inbound message's id (matches the envelope's messageId), not a surrogate key.</summary>
    public required Guid Id { get; init; }

    /// <summary>Logical name of the consumer/handler that processed the message.</summary>
    public required string ConsumerName { get; init; }

    public required DateTimeOffset ProcessedOn { get; init; }
}

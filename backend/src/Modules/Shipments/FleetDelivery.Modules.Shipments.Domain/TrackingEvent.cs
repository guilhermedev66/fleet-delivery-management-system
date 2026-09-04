using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>
/// One entry in a <see cref="Shipment"/>'s audit trail. Owned by (child of)
/// the <see cref="Shipment"/> aggregate — never its own aggregate root, no
/// independent lifecycle. Every single state transition on <see cref="Shipment"/>
/// appends exactly one of these; the audit trail is a side effect of the
/// state machine, not a bolt-on written separately.
/// </summary>
public sealed class TrackingEvent : Entity<Guid>
{
    // Reserved for EF Core materialization.
    private TrackingEvent()
    {
    }

    private TrackingEvent(Guid id, Guid shipmentId, string type, DateTimeOffset occurredAt, Guid? actorUserId, string? notes)
        : base(id)
    {
        ShipmentId = shipmentId;
        Type = type;
        OccurredAt = occurredAt;
        ActorUserId = actorUserId;
        Notes = notes;
    }

    public Guid ShipmentId { get; private set; }

    /// <summary>Short, stable label, e.g. "Created", "ReadyForDispatch", "Assigned", "PickedUp".</summary>
    public string Type { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Null for a system-generated event (none currently — every transition is caller-driven — but kept nullable for future automated transitions).</summary>
    public Guid? ActorUserId { get; private set; }

    public string? Notes { get; private set; }

    internal static TrackingEvent Create(Guid shipmentId, string type, Guid? actorUserId, string? notes) =>
        new(Guid.NewGuid(), shipmentId, type, DateTimeOffset.UtcNow, actorUserId, notes);
}

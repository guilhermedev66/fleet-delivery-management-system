using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>Outcome of a <see cref="DeliveryAttempt"/>.</summary>
public enum DeliveryAttemptOutcome
{
    Failed,
    Successful,
}

/// <summary>
/// Owned by (child of) the <see cref="Shipment"/> aggregate, separate from
/// <see cref="TrackingEvent"/>. Both <see cref="Shipment.MarkFailed"/> and
/// <see cref="Shipment.MarkDelivered"/> append one of these. The Proof of
/// Delivery photo itself is deliberately NOT stored here (or anywhere owned
/// by <see cref="Shipment"/>) — EF Core loads owned collections eagerly with
/// their owner, and a multi-megabyte blob riding along on every shipment
/// list/detail query would be a real performance trap. See
/// <see cref="ProofOfDeliveryPhoto"/>, a genuinely separate, on-demand-only
/// entity, for where it actually lives.
/// </summary>
public sealed class DeliveryAttempt : Entity<Guid>
{
    // Reserved for EF Core materialization.
    private DeliveryAttempt()
    {
    }

    private DeliveryAttempt(Guid id, Guid shipmentId, Guid driverId, DateTimeOffset attemptedAt, DeliveryAttemptOutcome outcome, string? notes)
        : base(id)
    {
        ShipmentId = shipmentId;
        DriverId = driverId;
        AttemptedAt = attemptedAt;
        Outcome = outcome;
        Notes = notes;
    }

    public Guid ShipmentId { get; private set; }

    public Guid DriverId { get; private set; }

    public DateTimeOffset AttemptedAt { get; private set; }

    public DeliveryAttemptOutcome Outcome { get; private set; }

    public string? Notes { get; private set; }

    internal static DeliveryAttempt Failed(Guid shipmentId, Guid driverId, string? notes) =>
        new(Guid.NewGuid(), shipmentId, driverId, DateTimeOffset.UtcNow, DeliveryAttemptOutcome.Failed, notes);

    internal static DeliveryAttempt Successful(Guid shipmentId, Guid driverId, string? notes) =>
        new(Guid.NewGuid(), shipmentId, driverId, DateTimeOffset.UtcNow, DeliveryAttemptOutcome.Successful, notes);
}

using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>Outcome of a <see cref="DeliveryAttempt"/>.</summary>
public enum DeliveryAttemptOutcome
{
    Failed,

    /// <summary>Not produced anywhere yet in M2 — reserved for M5's Proof-of-Delivery work to attach a successful attempt.</summary>
    Successful,
}

/// <summary>
/// Owned by (child of) the <see cref="Shipment"/> aggregate, separate from
/// <see cref="TrackingEvent"/> — this table exists so M5's Proof-of-Delivery
/// work has somewhere to attach a successful attempt later. Deliberately not
/// over-built for M2: only what <see cref="Shipment.MarkFailed"/> needs.
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
}

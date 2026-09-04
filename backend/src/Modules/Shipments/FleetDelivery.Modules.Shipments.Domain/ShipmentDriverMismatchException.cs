namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>
/// Thrown when a driver attempts to transition a <see cref="Shipment"/> that
/// is not currently assigned to them — "a driver can only transition a
/// shipment currently assigned to them" (docs/ARCHITECTURE.md). This is a
/// domain invariant enforced inside the aggregate's transition methods, not
/// only an API-layer check — the Application-layer handlers additionally
/// re-check ownership before ever calling into the domain method (belt and
/// suspenders), so hitting this exception in practice signals a bug in that
/// handler-level check rather than the primary line of defense.
/// </summary>
public sealed class ShipmentDriverMismatchException : Exception
{
    public ShipmentDriverMismatchException(Guid shipmentId, Guid assignedDriverId, Guid attemptedDriverId)
        : base($"Driver '{attemptedDriverId}' is not the driver assigned to shipment '{shipmentId}'.")
    {
        ShipmentId = shipmentId;
        AssignedDriverId = assignedDriverId;
        AttemptedDriverId = attemptedDriverId;
    }

    public Guid ShipmentId { get; }

    public Guid AssignedDriverId { get; }

    public Guid AttemptedDriverId { get; }
}

namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>
/// Thrown when a caller attempts a <see cref="Shipment"/> state transition
/// that the transition table does not allow from the aggregate's current
/// <see cref="ShipmentStatus"/> (e.g. delivering a shipment still in
/// <c>Draft</c>, or transitioning out of a terminal state). Mapped to
/// HTTP 409 at the API layer via <c>ShipmentErrors.InvalidTransition</c>.
/// </summary>
public sealed class InvalidShipmentTransitionException : Exception
{
    public InvalidShipmentTransitionException(ShipmentStatus currentStatus, ShipmentStatus attemptedStatus)
        : base($"Cannot transition shipment from '{currentStatus}' to '{attemptedStatus}'.")
    {
        CurrentStatus = currentStatus;
        AttemptedStatus = attemptedStatus;
    }

    public ShipmentStatus CurrentStatus { get; }

    public ShipmentStatus AttemptedStatus { get; }
}

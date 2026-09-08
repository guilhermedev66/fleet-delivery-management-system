namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>Thrown by <see cref="Shipment.AttachProofOfDelivery"/> when the shipment isn't <see cref="ShipmentStatus.Delivered"/> yet — a photo can't prove a delivery that hasn't happened.</summary>
public sealed class ShipmentNotDeliveredException(Guid shipmentId, ShipmentStatus currentStatus)
    : Exception($"Shipment '{shipmentId}' is not Delivered (current status: {currentStatus}) — cannot attach a Proof of Delivery photo.")
{
    public Guid ShipmentId { get; } = shipmentId;

    public ShipmentStatus CurrentStatus { get; } = currentStatus;
}

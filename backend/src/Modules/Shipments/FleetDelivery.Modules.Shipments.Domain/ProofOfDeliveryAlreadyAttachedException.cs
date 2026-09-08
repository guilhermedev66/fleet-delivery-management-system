namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>Thrown by <see cref="Shipment.AttachProofOfDelivery"/> on a second attempt — a Proof of Delivery photo is immutable once attached (evidence integrity), not something a later upload silently overwrites.</summary>
public sealed class ProofOfDeliveryAlreadyAttachedException(Guid shipmentId)
    : Exception($"Shipment '{shipmentId}' already has a Proof of Delivery photo attached.")
{
    public Guid ShipmentId { get; } = shipmentId;
}

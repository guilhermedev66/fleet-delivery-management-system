using FleetDelivery.Modules.Shipments.Domain;

namespace FleetDelivery.Modules.Shipments.Application.Abstractions;

/// <summary>On-demand persistence for Proof of Delivery bytes; never used by normal shipment list/detail reads.</summary>
public interface IProofOfDeliveryPhotoRepository
{
    Task<ProofOfDeliveryPhoto?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken = default);

    void Add(ProofOfDeliveryPhoto photo);
}

using FleetDelivery.Modules.Shipments.Domain;

namespace FleetDelivery.Modules.Shipments.Application.Abstractions;

/// <summary>Persistence abstraction for <see cref="Shipment"/>, implemented against <c>ShipmentsDbContext</c> in Infrastructure.</summary>
public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Paged, optionally status-filtered, optionally driver-filtered (server-side — never trust a client-supplied driver filter).</summary>
    Task<(IReadOnlyList<Shipment> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        ShipmentStatus? status,
        Guid? assignedDriverId,
        CancellationToken cancellationToken = default);

    /// <summary>Driver ids currently assigned to a shipment that's been dispatched but not yet finished (<see cref="ShipmentStatus.Assigned"/>, <see cref="ShipmentStatus.PickedUp"/>, <see cref="ShipmentStatus.InTransit"/>, or <see cref="ShipmentStatus.OutForDelivery"/>) — used to mark a driver "on a delivery" in the assign picker.</summary>
    Task<IReadOnlyCollection<Guid>> GetBusyDriverIdsAsync(CancellationToken cancellationToken = default);

    void Add(Shipment shipment);
}

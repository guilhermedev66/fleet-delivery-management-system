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

    void Add(Shipment shipment);
}

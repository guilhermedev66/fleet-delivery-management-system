using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Repositories;

public sealed class ShipmentRepository(ShipmentsDbContext dbContext) : IShipmentRepository
{
    // No explicit .Include(...): TrackingEvents/DeliveryAttempts are EF owned
    // collections, which EF Core always loads together with their owner.
    public Task<Shipment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Shipments.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Shipment> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        ShipmentStatus? status,
        Guid? assignedDriverId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Shipments.AsQueryable();

        if (status is not null)
        {
            query = query.Where(s => s.Status == status);
        }

        if (assignedDriverId is not null)
        {
            query = query.Where(s => s.AssignedDriverId == assignedDriverId);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private static readonly ShipmentStatus[] BusyStatuses =
    [
        ShipmentStatus.Assigned,
        ShipmentStatus.PickedUp,
        ShipmentStatus.InTransit,
        ShipmentStatus.OutForDelivery,
    ];

    public async Task<IReadOnlyCollection<Guid>> GetBusyDriverIdsAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Shipments
            .Where(s => s.AssignedDriverId != null && BusyStatuses.Contains(s.Status))
            .Select(s => s.AssignedDriverId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

    public void Add(Shipment shipment) => dbContext.Shipments.Add(shipment);
}

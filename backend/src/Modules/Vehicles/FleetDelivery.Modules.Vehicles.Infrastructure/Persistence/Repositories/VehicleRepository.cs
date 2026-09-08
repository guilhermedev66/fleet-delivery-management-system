using FleetDelivery.Modules.Vehicles.Application.Abstractions;
using FleetDelivery.Modules.Vehicles.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetDelivery.Modules.Vehicles.Infrastructure.Persistence.Repositories;

public sealed class VehicleRepository(VehiclesDbContext dbContext) : IVehicleRepository
{
    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> ListAsync(VehicleStatus? status, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Vehicles.AsQueryable();

        if (status is not null)
        {
            query = query.Where(v => v.Status == status);
        }

        return await query
            .OrderBy(v => v.PlateNumber)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByPlateNumberAsync(string normalizedPlateNumber, CancellationToken cancellationToken = default) =>
        dbContext.Vehicles.AnyAsync(v => v.PlateNumber == normalizedPlateNumber, cancellationToken);

    public void Add(Vehicle vehicle) => dbContext.Vehicles.Add(vehicle);
}

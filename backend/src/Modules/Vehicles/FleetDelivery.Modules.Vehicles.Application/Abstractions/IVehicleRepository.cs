using FleetDelivery.Modules.Vehicles.Domain;

namespace FleetDelivery.Modules.Vehicles.Application.Abstractions;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vehicle>> ListAsync(VehicleStatus? status, CancellationToken cancellationToken = default);

    /// <summary>Case-insensitive by normalization — <see cref="Vehicle.PlateNumber"/> is always stored upper-cased. A fast-path check for a friendly duplicate error; the DB unique index is the real invariant (see <c>VehicleConfiguration</c> and <c>VehiclesDbContext</c>'s translation of the resulting constraint violation).</summary>
    Task<bool> ExistsByPlateNumberAsync(string normalizedPlateNumber, CancellationToken cancellationToken = default);

    void Add(Vehicle vehicle);
}

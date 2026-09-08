using FleetDelivery.Modules.Vehicles.Application.Contracts;
using FleetDelivery.Modules.Vehicles.Domain;

namespace FleetDelivery.Modules.Vehicles.Application.Vehicles;

internal static class VehicleMapper
{
    public static VehicleDto ToDto(this Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.PlateNumber,
        vehicle.Type.ToString(),
        vehicle.CapacityKg,
        vehicle.Status.ToString(),
        vehicle.CreatedAt);
}

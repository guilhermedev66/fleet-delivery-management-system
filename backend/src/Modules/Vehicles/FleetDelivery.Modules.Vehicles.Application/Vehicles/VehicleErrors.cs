using FleetDelivery.BuildingBlocks.Results;

namespace FleetDelivery.Modules.Vehicles.Application.Vehicles;

public static class VehicleErrors
{
    public static readonly Error NotFound = new("Vehicle.NotFound", "Vehicle not found.");

    public static readonly Error DuplicatePlateNumber = new("Vehicle.DuplicatePlateNumber", "A vehicle with this plate number already exists.");

    public static Error Validation(string message) => new("Vehicle.Validation", message);
}

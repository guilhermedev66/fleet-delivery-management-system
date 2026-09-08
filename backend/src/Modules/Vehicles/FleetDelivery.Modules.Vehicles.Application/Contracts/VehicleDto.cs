namespace FleetDelivery.Modules.Vehicles.Application.Contracts;

public sealed record VehicleDto(
    Guid Id,
    string PlateNumber,
    string Type,
    decimal CapacityKg,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record VehicleListDto(IReadOnlyList<VehicleDto> Items);

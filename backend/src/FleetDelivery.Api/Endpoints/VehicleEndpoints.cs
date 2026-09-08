using FleetDelivery.Modules.Vehicles.Application.Contracts;
using FleetDelivery.Modules.Vehicles.Application.Vehicles;
using FleetDelivery.Modules.Vehicles.Domain;
using MediatR;

namespace FleetDelivery.Api.Endpoints;

/// <summary>
/// Maps <c>/api/vehicles/*</c>. Listing/reading is open to any authenticated
/// role (dispatchers browse the fleet, the shipment-assign picker needs
/// Active vehicles); registering a vehicle is restricted to Dispatcher/Admin,
/// mirroring <see cref="ShipmentEndpoints"/>'s create-shipment restriction.
/// </summary>
public static class VehicleEndpoints
{
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/vehicles").WithTags("Vehicles").RequireAuthorization();

        group.MapGet("", ListVehiclesAsync);
        group.MapGet("/{id:guid}", GetVehicleByIdAsync);
        group.MapPost("", RegisterVehicleAsync).RequireAuthorization(policy => policy.RequireRole("Dispatcher", "Admin"));

        return app;
    }

    private static async Task<IResult> ListVehiclesAsync(ISender sender, CancellationToken cancellationToken, string? status = null)
    {
        VehicleStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<VehicleStatus>(status, ignoreCase: true, out var value))
            {
                return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid status filter.", detail: $"'{status}' is not a valid vehicle status.");
            }

            parsedStatus = value;
        }

        var result = await sender.Send(new ListVehiclesQuery(parsedStatus), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> GetVehicleByIdAsync(Guid id, ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetVehicleByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static async Task<IResult> RegisterVehicleAsync(RegisterVehicleRequest request, ISender sender, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<VehicleType>(request.Type, ignoreCase: true, out var type))
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid vehicle type.", detail: $"'{request.Type}' is not a valid vehicle type.");
        }

        var result = await sender.Send(new RegisterVehicleCommand(request.PlateNumber, type, request.CapacityKg), cancellationToken);

        return result.IsSuccess ? Results.Ok(result.Value.ToResponse()) : MapFailure(result.Error);
    }

    private static IResult MapFailure(FleetDelivery.BuildingBlocks.Results.Error error) => error.Code switch
    {
        "Vehicle.NotFound" => Results.NotFound(),

        "Vehicle.DuplicatePlateNumber" => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Duplicate plate number.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/vehicle-duplicate-plate-number"),

        "Vehicle.Validation" => Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Validation error.",
            detail: error.Message,
            type: "https://fleetdelivery.local/errors/vehicle-validation"),

        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Unexpected error."),
    };
}

public sealed record RegisterVehicleRequest(string PlateNumber, string Type, decimal CapacityKg);

public sealed record VehicleResponse(Guid Id, string PlateNumber, string Type, decimal CapacityKg, string Status, DateTimeOffset CreatedAt);

public sealed record VehicleListResponse(IReadOnlyList<VehicleResponse> Items);

internal static class VehicleApiMapping
{
    public static VehicleResponse ToResponse(this VehicleDto dto) => new(
        dto.Id,
        dto.PlateNumber,
        dto.Type,
        dto.CapacityKg,
        dto.Status,
        dto.CreatedAt);

    public static VehicleListResponse ToResponse(this VehicleListDto dto) => new(dto.Items.Select(ToResponse).ToList());
}

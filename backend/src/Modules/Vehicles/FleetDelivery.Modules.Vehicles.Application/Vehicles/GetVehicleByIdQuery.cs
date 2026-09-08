using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Vehicles.Application.Abstractions;
using FleetDelivery.Modules.Vehicles.Application.Contracts;
using MediatR;

namespace FleetDelivery.Modules.Vehicles.Application.Vehicles;

public sealed record GetVehicleByIdQuery(Guid Id) : IRequest<Result<VehicleDto>>;

public sealed class GetVehicleByIdQueryHandler(IVehicleRepository repository) : IRequestHandler<GetVehicleByIdQuery, Result<VehicleDto>>
{
    public async Task<Result<VehicleDto>> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicle = await repository.GetByIdAsync(request.Id, cancellationToken);

        return vehicle is null
            ? Result.Failure<VehicleDto>(VehicleErrors.NotFound)
            : vehicle.ToDto();
    }
}

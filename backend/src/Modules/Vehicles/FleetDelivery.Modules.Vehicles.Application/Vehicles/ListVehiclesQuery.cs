using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Vehicles.Application.Abstractions;
using FleetDelivery.Modules.Vehicles.Application.Contracts;
using FleetDelivery.Modules.Vehicles.Domain;
using MediatR;

namespace FleetDelivery.Modules.Vehicles.Application.Vehicles;

public sealed record ListVehiclesQuery(VehicleStatus? Status) : IRequest<Result<VehicleListDto>>;

public sealed class ListVehiclesQueryHandler(IVehicleRepository repository) : IRequestHandler<ListVehiclesQuery, Result<VehicleListDto>>
{
    public async Task<Result<VehicleListDto>> Handle(ListVehiclesQuery request, CancellationToken cancellationToken)
    {
        var vehicles = await repository.ListAsync(request.Status, cancellationToken);

        return new VehicleListDto(vehicles.Select(v => v.ToDto()).ToList());
    }
}

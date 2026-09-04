using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record GetShipmentTimelineQuery(Guid ShipmentId, Guid CallerUserId, string CallerRole) : IRequest<Result<ShipmentTimelineDto>>;

/// <summary>Same ownership rule as <see cref="GetShipmentByIdQuery"/> — 404, not 403, for a Driver requesting a shipment that isn't theirs.</summary>
public sealed class GetShipmentTimelineQueryHandler(IShipmentRepository repository) : IRequestHandler<GetShipmentTimelineQuery, Result<ShipmentTimelineDto>>
{
    public async Task<Result<ShipmentTimelineDto>> Handle(GetShipmentTimelineQuery request, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(request.ShipmentId, cancellationToken);

        if (shipment is null)
        {
            return Result.Failure<ShipmentTimelineDto>(ShipmentErrors.NotFound);
        }

        if (string.Equals(request.CallerRole, RoleNames.Driver, StringComparison.Ordinal) && shipment.AssignedDriverId != request.CallerUserId)
        {
            return Result.Failure<ShipmentTimelineDto>(ShipmentErrors.NotFound);
        }

        return shipment.ToTimelineDto();
    }
}

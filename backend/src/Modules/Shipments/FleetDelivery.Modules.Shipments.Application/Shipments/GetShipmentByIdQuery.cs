using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

/// <param name="CallerUserId">Caller's own user id, from the JWT.</param>
/// <param name="CallerRole">Caller's role, from the JWT.</param>
public sealed record GetShipmentByIdQuery(Guid ShipmentId, Guid CallerUserId, string CallerRole) : IRequest<Result<ShipmentDto>>;

/// <summary>
/// A Driver who isn't the shipment's assigned driver gets the exact same
/// <see cref="ShipmentErrors.NotFound"/> as a genuinely-nonexistent id — a
/// 403 would confirm the id exists, which is the weaker IDOR posture; 404
/// hides that too.
/// </summary>
public sealed class GetShipmentByIdQueryHandler(IShipmentRepository repository) : IRequestHandler<GetShipmentByIdQuery, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(GetShipmentByIdQuery request, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(request.ShipmentId, cancellationToken);

        if (shipment is null)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.NotFound);
        }

        if (string.Equals(request.CallerRole, RoleNames.Driver, StringComparison.Ordinal) && shipment.AssignedDriverId != request.CallerUserId)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.NotFound);
        }

        return shipment.ToDto();
    }
}

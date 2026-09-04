using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Identity.Application.Users;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record AssignCommand(Guid ShipmentId, Guid DriverId, Guid AssignedByUserId, int ExpectedVersion) : IRequest<Result<ShipmentDto>>;

/// <summary>
/// Validates <c>driverId</c> against Identity's own Application public
/// contract (<see cref="GetCurrentUserQuery"/>, sent via MediatR's
/// <see cref="ISender"/>) rather than a direct query into the <c>identity</c>
/// schema — the in-process cross-module call style described in
/// docs/ARCHITECTURE.md. There is no separate Drivers module in M2: "driver"
/// IS just an Identity user with <c>Role.Driver</c>.
/// </summary>
public sealed class AssignCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork, ISender sender)
    : IRequestHandler<AssignCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(AssignCommand request, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(request.ShipmentId, cancellationToken);

        if (shipment is null)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.NotFound);
        }

        if (shipment.Version != request.ExpectedVersion)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.ConcurrencyConflict);
        }

        var driverResult = await sender.Send(new GetCurrentUserQuery(request.DriverId), cancellationToken);

        if (driverResult.IsFailure || !string.Equals(driverResult.Value.Role, RoleNames.Driver, StringComparison.Ordinal))
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.InvalidDriver);
        }

        try
        {
            shipment.Assign(request.DriverId, request.AssignedByUserId);
        }
        catch (InvalidShipmentTransitionException ex)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.InvalidTransition(ex.Message));
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.ConcurrencyConflict);
        }

        return shipment.ToDto();
    }
}

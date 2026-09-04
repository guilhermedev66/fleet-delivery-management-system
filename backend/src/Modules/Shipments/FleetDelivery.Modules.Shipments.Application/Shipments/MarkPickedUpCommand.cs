using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record MarkPickedUpCommand(Guid ShipmentId, Guid DriverId, int ExpectedVersion) : IRequest<Result<ShipmentDto>>;

/// <summary>
/// Ownership (the caller must be the assigned driver) is checked here at the
/// handler level AND inside <see cref="Shipment.MarkPickedUp"/> itself
/// (belt and suspenders) — this is exactly the kind of IDOR the project is
/// meant to demonstrate protecting against. A mismatch returns the same
/// <see cref="ShipmentErrors.NotFound"/> as a genuinely-missing shipment id.
/// </summary>
public sealed class MarkPickedUpCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<MarkPickedUpCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(MarkPickedUpCommand request, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(request.ShipmentId, cancellationToken);

        if (shipment is null)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.NotFound);
        }

        if (shipment.AssignedDriverId != request.DriverId)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.NotFound);
        }

        if (shipment.Version != request.ExpectedVersion)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.ConcurrencyConflict);
        }

        try
        {
            shipment.MarkPickedUp(request.DriverId);
        }
        catch (InvalidShipmentTransitionException ex)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.InvalidTransition(ex.Message));
        }
        catch (ShipmentDriverMismatchException)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.NotFound);
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

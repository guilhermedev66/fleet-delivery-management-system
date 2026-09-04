using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record MarkOutForDeliveryCommand(Guid ShipmentId, Guid DriverId, int ExpectedVersion) : IRequest<Result<ShipmentDto>>;

public sealed class MarkOutForDeliveryCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<MarkOutForDeliveryCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(MarkOutForDeliveryCommand request, CancellationToken cancellationToken)
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
            shipment.MarkOutForDelivery(request.DriverId);
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

using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record MarkInTransitCommand(Guid ShipmentId, Guid DriverId, int ExpectedVersion) : IRequest<Result<ShipmentDto>>;

public sealed class MarkInTransitCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<MarkInTransitCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(MarkInTransitCommand request, CancellationToken cancellationToken)
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
            shipment.MarkInTransit(request.DriverId);
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

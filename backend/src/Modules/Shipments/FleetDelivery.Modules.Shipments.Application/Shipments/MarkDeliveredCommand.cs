using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record MarkDeliveredCommand(
    Guid ShipmentId,
    Guid DriverId,
    int ExpectedVersion,
    string? RecipientName,
    string? Notes) : IRequest<Result<ShipmentDto>>;

public sealed class MarkDeliveredCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<MarkDeliveredCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(MarkDeliveredCommand request, CancellationToken cancellationToken)
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

        // Falls back to the shipment's own recipient name if the driver didn't
        // supply a (possibly different, e.g. "left with neighbor") one.
        var recipientName = string.IsNullOrWhiteSpace(request.RecipientName) ? shipment.RecipientName : request.RecipientName;

        try
        {
            shipment.MarkDelivered(request.DriverId, recipientName, request.Notes);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.Validation(ex.Message));
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

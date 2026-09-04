using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record CancelCommand(Guid ShipmentId, Guid CancelledByUserId, int ExpectedVersion, string Reason) : IRequest<Result<ShipmentDto>>;

public sealed class CancelCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CancelCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(CancelCommand request, CancellationToken cancellationToken)
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

        try
        {
            shipment.Cancel(request.CancelledByUserId, request.Reason);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.Validation(ex.Message));
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

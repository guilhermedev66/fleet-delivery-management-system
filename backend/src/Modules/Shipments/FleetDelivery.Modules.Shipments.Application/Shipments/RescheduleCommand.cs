using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record RescheduleCommand(Guid ShipmentId, Guid RescheduledByUserId, int ExpectedVersion) : IRequest<Result<ShipmentDto>>;

public sealed class RescheduleCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<RescheduleCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(RescheduleCommand request, CancellationToken cancellationToken)
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
            shipment.Reschedule(request.RescheduledByUserId);
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

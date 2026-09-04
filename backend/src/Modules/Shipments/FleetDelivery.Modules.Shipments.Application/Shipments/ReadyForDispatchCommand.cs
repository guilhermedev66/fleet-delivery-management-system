using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record ReadyForDispatchCommand(Guid ShipmentId, Guid ActorUserId, int ExpectedVersion) : IRequest<Result<ShipmentDto>>;

/// <summary>
/// Backs the single <c>POST /api/shipments/{id}/ready-for-dispatch</c>
/// endpoint in the fixed API contract, which has to serve both dispatch-queue
/// entry points the domain model exposes: <c>Draft -> ReadyForDispatch</c>
/// (<see cref="Shipment.MarkReadyForDispatch"/>) and, for a shipment that
/// already failed and was rescheduled, <c>Rescheduled -> ReadyForDispatch</c>
/// (<see cref="Shipment.BackToReadyForDispatch"/>). Which one applies is
/// purely a function of the shipment's current status, so the handler
/// dispatches to the matching domain method rather than the API exposing two
/// endpoints for what is, from the dispatcher's point of view, one action:
/// "put this back in the dispatch queue".
/// </summary>
public sealed class ReadyForDispatchCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<ReadyForDispatchCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(ReadyForDispatchCommand request, CancellationToken cancellationToken)
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
            if (shipment.Status == ShipmentStatus.Rescheduled)
            {
                shipment.BackToReadyForDispatch(request.ActorUserId);
            }
            else
            {
                shipment.MarkReadyForDispatch(request.ActorUserId);
            }
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

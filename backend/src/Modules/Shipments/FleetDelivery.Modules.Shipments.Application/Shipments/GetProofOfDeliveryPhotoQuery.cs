using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record GetProofOfDeliveryPhotoQuery(
    Guid ShipmentId,
    Guid CallerUserId,
    string CallerRole) : IRequest<Result<ProofOfDeliveryPhotoDto>>;

/// <summary>
/// Dispatcher/Admin users and the assigned Driver may retrieve the photo.
/// Every other caller gets the same 404 as an unknown shipment/photo, so the
/// endpoint never confirms protected resource existence.
/// </summary>
public sealed class GetProofOfDeliveryPhotoQueryHandler(
    IShipmentRepository shipmentRepository,
    IProofOfDeliveryPhotoRepository photoRepository)
    : IRequestHandler<GetProofOfDeliveryPhotoQuery, Result<ProofOfDeliveryPhotoDto>>
{
    public async Task<Result<ProofOfDeliveryPhotoDto>> Handle(GetProofOfDeliveryPhotoQuery request, CancellationToken cancellationToken)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);

        if (shipment is null)
        {
            return Result.Failure<ProofOfDeliveryPhotoDto>(ShipmentErrors.NotFound);
        }

        var isDispatcherOrAdmin = string.Equals(request.CallerRole, "Dispatcher", StringComparison.Ordinal)
            || string.Equals(request.CallerRole, "Admin", StringComparison.Ordinal);
        var isAssignedDriver = string.Equals(request.CallerRole, RoleNames.Driver, StringComparison.Ordinal)
            && shipment.AssignedDriverId == request.CallerUserId;

        if (!isDispatcherOrAdmin && !isAssignedDriver)
        {
            return Result.Failure<ProofOfDeliveryPhotoDto>(ShipmentErrors.NotFound);
        }

        var photo = await photoRepository.GetByShipmentIdAsync(request.ShipmentId, cancellationToken);

        return photo is null
            ? Result.Failure<ProofOfDeliveryPhotoDto>(ShipmentErrors.NotFound)
            : new ProofOfDeliveryPhotoDto(photo.Content, photo.ContentType);
    }
}

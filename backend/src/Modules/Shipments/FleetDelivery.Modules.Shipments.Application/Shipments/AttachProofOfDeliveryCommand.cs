using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record AttachProofOfDeliveryCommand(
    Guid ShipmentId,
    Guid DriverId,
    byte[] Content,
    string ContentType) : IRequest<Result<ShipmentDto>>;

public sealed class AttachProofOfDeliveryCommandHandler(
    IShipmentRepository shipmentRepository,
    IProofOfDeliveryPhotoRepository photoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<AttachProofOfDeliveryCommand, Result<ShipmentDto>>
{
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public async Task<Result<ShipmentDto>> Handle(AttachProofOfDeliveryCommand request, CancellationToken cancellationToken)
    {
        var shipment = await shipmentRepository.GetByIdAsync(request.ShipmentId, cancellationToken);

        if (shipment is null || shipment.AssignedDriverId != request.DriverId)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.NotFound);
        }

        if (request.Content.LongLength > ProofOfDeliveryPhoto.MaxContentBytes)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.ProofOfDeliveryTooLarge);
        }

        var detectedContentType = DetectContentType(request.Content);

        if (detectedContentType is null)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.InvalidProofOfDeliveryContent);
        }

        try
        {
            shipment.AttachProofOfDelivery(request.DriverId);
        }
        catch (ShipmentNotDeliveredException ex)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.ShipmentNotDelivered(ex.Message));
        }
        catch (ProofOfDeliveryAlreadyAttachedException ex)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.ProofOfDeliveryAlreadyAttached(ex.Message));
        }
        catch (ShipmentDriverMismatchException)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.NotFound);
        }

        // ContentType is deliberately derived from the bytes above. The
        // client-provided request.ContentType is untrusted metadata and is
        // never persisted.
        photoRepository.Add(ProofOfDeliveryPhoto.Create(shipment.Id, request.Content, detectedContentType));

        try
        {
            // Both repositories share the scoped ShipmentsDbContext, so the
            // flag and photo commit atomically in this single save.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.ConcurrencyConflict);
        }
        catch (ProofOfDeliveryPhotoAlreadyExistsException)
        {
            // Two concurrent uploads both passed the in-memory
            // HasProofOfDelivery check above; this one lost the race at the
            // database level. Same 409 the fast path returns — the caller
            // can't tell which check caught it, nor should they need to.
            return Result.Failure<ShipmentDto>(ShipmentErrors.ProofOfDeliveryAlreadyAttached(
                "A Proof of Delivery photo was already attached to this shipment."));
        }

        return shipment.ToDto();
    }

    private static string? DetectContentType(ReadOnlySpan<byte> content)
    {
        if (content.StartsWith(JpegSignature))
        {
            return "image/jpeg";
        }

        if (content.StartsWith(PngSignature))
        {
            return "image/png";
        }

        return null;
    }
}

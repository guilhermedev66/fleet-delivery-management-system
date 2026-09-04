using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

public sealed record CreateShipmentCommand(
    string RecipientName,
    string RecipientPhone,
    AddressDto Origin,
    AddressDto Destination,
    Guid CreatedByUserId) : IRequest<Result<ShipmentDto>>;

public sealed class CreateShipmentCommandHandler(IShipmentRepository repository, IUnitOfWork unitOfWork)
    : IRequestHandler<CreateShipmentCommand, Result<ShipmentDto>>
{
    public async Task<Result<ShipmentDto>> Handle(CreateShipmentCommand request, CancellationToken cancellationToken)
    {
        Shipment shipment;

        try
        {
            var origin = Address.Create(request.Origin.Street, request.Origin.City, request.Origin.State, request.Origin.PostalCode, request.Origin.Country);
            var destination = Address.Create(request.Destination.Street, request.Destination.City, request.Destination.State, request.Destination.PostalCode, request.Destination.Country);

            shipment = Shipment.Create(request.RecipientName, request.RecipientPhone, origin, destination, request.CreatedByUserId);
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<ShipmentDto>(ShipmentErrors.Validation(ex.Message));
        }

        repository.Add(shipment);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return shipment.ToDto();
    }
}

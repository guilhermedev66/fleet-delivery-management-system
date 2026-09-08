using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

internal static class ShipmentMapper
{
    public static ShipmentDto ToDto(this Shipment shipment) => new(
        shipment.Id,
        shipment.TrackingNumber,
        shipment.Status.ToString(),
        shipment.RecipientName,
        shipment.RecipientPhone,
        shipment.OriginAddress.ToDto(),
        shipment.DestinationAddress.ToDto(),
        shipment.AssignedDriverId,
        shipment.AssignedVehicleId,
        shipment.CreatedByUserId,
        shipment.CreatedAt,
        shipment.Version);

    public static AddressDto ToDto(this Address address) => new(
        address.Street,
        address.City,
        address.State,
        address.PostalCode,
        address.Country);

    public static ShipmentTimelineDto ToTimelineDto(this Shipment shipment) => new(
        shipment.TrackingEvents
            .OrderBy(e => e.OccurredAt)
            .Select(e => new TrackingEventDto(e.Type, e.OccurredAt, e.ActorUserId, e.Notes))
            .ToList());
}

namespace FleetDelivery.Modules.Shipments.Application.Contracts;

public sealed record AddressDto(string Street, string City, string State, string PostalCode, string Country);

public sealed record ShipmentDto(
    Guid Id,
    string TrackingNumber,
    string Status,
    string RecipientName,
    string RecipientPhone,
    AddressDto Origin,
    AddressDto Destination,
    Guid? AssignedDriverId,
    Guid? AssignedVehicleId,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    int Version);

public sealed record TrackingEventDto(string Type, DateTimeOffset OccurredAt, Guid? ActorUserId, string? Notes);

public sealed record ShipmentTimelineDto(IReadOnlyList<TrackingEventDto> Events);

public sealed record ShipmentListPageDto(IReadOnlyList<ShipmentDto> Items, int Page, int PageSize, int TotalCount);

public sealed record AvailableDriverDto(Guid Id, string FullName, string Email, bool IsAvailable);

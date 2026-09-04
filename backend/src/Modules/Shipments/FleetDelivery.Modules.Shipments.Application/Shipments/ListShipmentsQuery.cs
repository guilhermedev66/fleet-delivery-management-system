using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using FleetDelivery.Modules.Shipments.Domain;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

/// <param name="Status">Optional client-supplied status filter.</param>
/// <param name="AssignedDriverId">
/// Optional client-supplied driver filter (e.g. a dispatcher looking up one
/// driver's shipments) — IGNORED and overridden below when the caller
/// themselves is a Driver, per docs/ARCHITECTURE.md: never trust a
/// client-supplied filter for a Driver's own scoping.
/// </param>
public sealed record ListShipmentsQuery(
    int Page,
    int PageSize,
    ShipmentStatus? Status,
    Guid? AssignedDriverId,
    Guid CallerUserId,
    string CallerRole) : IRequest<Result<ShipmentListPageDto>>;

public sealed class ListShipmentsQueryHandler(IShipmentRepository repository) : IRequestHandler<ListShipmentsQuery, Result<ShipmentListPageDto>>
{
    public async Task<Result<ShipmentListPageDto>> Handle(ListShipmentsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 20 : request.PageSize;

        // Server-side, unconditional override — a Driver only ever sees their
        // own shipments, regardless of what (if anything) they passed.
        var assignedDriverId = string.Equals(request.CallerRole, RoleNames.Driver, StringComparison.Ordinal)
            ? request.CallerUserId
            : request.AssignedDriverId;

        var (items, totalCount) = await repository.ListAsync(page, pageSize, request.Status, assignedDriverId, cancellationToken);

        return new ShipmentListPageDto(items.Select(ShipmentMapper.ToDto).ToList(), page, pageSize, totalCount);
    }
}

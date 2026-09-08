using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Identity.Application.Users;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Application.Contracts;
using MediatR;

namespace FleetDelivery.Modules.Shipments.Application.Shipments;

/// <summary>
/// Backs the dispatcher's driver picker on the Assign action: every Driver
/// (via Identity's <see cref="ListDriversQuery"/>) annotated with whether
/// they're currently on a delivery (via <see cref="IShipmentRepository.GetBusyDriverIdsAsync"/>) —
/// not filtered out, so the UI can show *why* a driver can't be picked
/// instead of just making them disappear.
/// </summary>
public sealed record ListAvailableDriversQuery : IRequest<Result<IReadOnlyList<AvailableDriverDto>>>;

public sealed class ListAvailableDriversQueryHandler(IShipmentRepository repository, ISender sender)
    : IRequestHandler<ListAvailableDriversQuery, Result<IReadOnlyList<AvailableDriverDto>>>
{
    public async Task<Result<IReadOnlyList<AvailableDriverDto>>> Handle(ListAvailableDriversQuery request, CancellationToken cancellationToken)
    {
        var driversResult = await sender.Send(new ListDriversQuery(), cancellationToken);

        if (driversResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AvailableDriverDto>>(driversResult.Error);
        }

        var busyDriverIds = await repository.GetBusyDriverIdsAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AvailableDriverDto>>(
            driversResult.Value
                .Select(d => new AvailableDriverDto(d.Id, d.FullName, d.Email, !busyDriverIds.Contains(d.Id)))
                .ToList());
    }
}

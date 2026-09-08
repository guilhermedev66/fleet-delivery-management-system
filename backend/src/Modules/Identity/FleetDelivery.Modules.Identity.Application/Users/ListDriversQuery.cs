using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Application.Contracts;
using FleetDelivery.Modules.Identity.Domain;
using MediatR;

namespace FleetDelivery.Modules.Identity.Application.Users;

/// <summary>
/// Cross-module-safe: returns every <see cref="Role.Driver"/> user as
/// <see cref="UserDto"/>. Shipments' <c>ListAvailableDriversQuery</c> calls
/// this (via MediatR) rather than reaching into Identity's schema directly,
/// then cross-references the result against its own repository to work out
/// which drivers are currently busy.
/// </summary>
public sealed record ListDriversQuery : IRequest<Result<IReadOnlyList<UserDto>>>;

public sealed class ListDriversQueryHandler(IUserRepository userRepository) : IRequestHandler<ListDriversQuery, Result<IReadOnlyList<UserDto>>>
{
    public async Task<Result<IReadOnlyList<UserDto>>> Handle(ListDriversQuery request, CancellationToken cancellationToken)
    {
        var drivers = await userRepository.ListByRoleAsync(Role.Driver, cancellationToken);

        return Result.Success<IReadOnlyList<UserDto>>(
            drivers.Select(d => new UserDto(d.Id, d.Email.Value, d.FullName, d.Role.ToString())).ToList());
    }
}

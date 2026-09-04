using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Application.Contracts;
using MediatR;

namespace FleetDelivery.Modules.Identity.Application.Users;

public sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<UserDto>>;

public sealed class GetCurrentUserQueryHandler(IUserRepository userRepository) : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken);

        if (user is null)
        {
            return Result.Failure<UserDto>(IdentityErrors.UserNotFound);
        }

        return new UserDto(user.Id, user.Email.Value, user.FullName, user.Role.ToString());
    }
}

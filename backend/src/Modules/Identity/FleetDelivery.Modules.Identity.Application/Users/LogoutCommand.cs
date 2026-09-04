using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Identity.Application.Abstractions;
using MediatR;

namespace FleetDelivery.Modules.Identity.Application.Users;

/// <summary>Logout is idempotent by design — always succeeds, whether or not the token was present/valid.</summary>
public sealed record LogoutCommand(string? RawRefreshToken) : IRequest<Result>;

public sealed class LogoutCommandHandler(
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            return Result.Success();
        }

        var hash = tokenService.HashRefreshToken(request.RawRefreshToken);
        var token = await refreshTokenRepository.GetByTokenHashAsync(hash, cancellationToken);

        if (token is not null && token.IsActive)
        {
            token.Revoke();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}

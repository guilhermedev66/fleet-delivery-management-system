using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Identity.Application.Abstractions;
using MediatR;

namespace FleetDelivery.Modules.Identity.Application.Users;

public sealed record RefreshTokenCommand(string RawRefreshToken) : IRequest<Result<RefreshTokenResult>>;

public sealed record RefreshTokenResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RawRefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResult>>
{
    public async Task<Result<RefreshTokenResult>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            return Result.Failure<RefreshTokenResult>(IdentityErrors.InvalidRefreshToken);
        }

        var incomingHash = tokenService.HashRefreshToken(request.RawRefreshToken);
        var existingToken = await refreshTokenRepository.GetByTokenHashAsync(incomingHash, cancellationToken);

        if (existingToken is null)
        {
            return Result.Failure<RefreshTokenResult>(IdentityErrors.InvalidRefreshToken);
        }

        if (existingToken.IsRevoked)
        {
            // A revoked token being presented again is a signal of theft (the
            // token was rotated already, so this is a replay of an old value).
            // Fail closed and burn the whole chain for this user.
            var activeTokens = await refreshTokenRepository.GetActiveByUserIdAsync(existingToken.UserId, cancellationToken);
            foreach (var token in activeTokens)
            {
                token.Revoke();
            }

            if (activeTokens.Count > 0)
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }

            return Result.Failure<RefreshTokenResult>(IdentityErrors.InvalidRefreshToken);
        }

        if (existingToken.IsExpired)
        {
            return Result.Failure<RefreshTokenResult>(IdentityErrors.InvalidRefreshToken);
        }

        var user = await userRepository.GetByIdAsync(existingToken.UserId, cancellationToken);

        if (user is null || !user.IsActive)
        {
            return Result.Failure<RefreshTokenResult>(IdentityErrors.InvalidRefreshToken);
        }

        // Rotate: issue a new token and revoke the old one, linking them.
        var newRawToken = tokenService.GenerateRefreshToken();
        var newHash = tokenService.HashRefreshToken(newRawToken);
        var newRefreshToken = Domain.RefreshToken.Issue(user.Id, newHash, tokenService.RefreshTokenLifetime);

        existingToken.Revoke(newHash);
        refreshTokenRepository.Add(newRefreshToken);

        var accessToken = tokenService.CreateAccessToken(user);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RefreshTokenResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            newRawToken,
            newRefreshToken.ExpiresAt);
    }
}

using FleetDelivery.BuildingBlocks.Results;
using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Application.Contracts;
using MediatR;

namespace FleetDelivery.Modules.Identity.Application.Users;

public sealed record LoginCommand(string Email, string Password) : IRequest<Result<LoginResult>>;

/// <param name="RawRefreshToken">Raw refresh token — hand to the client exactly once (as an httpOnly cookie), never persisted raw.</param>
public sealed record LoginResult(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RawRefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    UserDto User);

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<LoginCommand, Result<LoginResult>>
{
    public async Task<Result<LoginResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Same generic failure whether the email doesn't exist, the password is
        // wrong, or the account is inactive — never reveal which (no account enumeration).
        if (user is null || !user.IsActive || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            return Result.Failure<LoginResult>(IdentityErrors.InvalidCredentials);
        }

        var accessToken = tokenService.CreateAccessToken(user);

        var rawRefreshToken = tokenService.GenerateRefreshToken();
        var refreshTokenHash = tokenService.HashRefreshToken(rawRefreshToken);
        var refreshToken = Domain.RefreshToken.Issue(user.Id, refreshTokenHash, tokenService.RefreshTokenLifetime);

        refreshTokenRepository.Add(refreshToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var userDto = new UserDto(user.Id, user.Email.Value, user.FullName, user.Role.ToString());

        return new LoginResult(
            accessToken.Value,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt,
            userDto);
    }
}

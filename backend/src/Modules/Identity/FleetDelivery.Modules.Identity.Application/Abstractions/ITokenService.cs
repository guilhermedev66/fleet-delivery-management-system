using FleetDelivery.Modules.Identity.Domain;

namespace FleetDelivery.Modules.Identity.Application.Abstractions;

/// <summary>Result of issuing a JWT access token.</summary>
/// <param name="Value">The signed, encoded JWT.</param>
/// <param name="ExpiresAt">Absolute UTC expiry.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

/// <summary>
/// Abstracts JWT access-token issuance and refresh-token generation/hashing
/// so Application handlers don't depend on JWT libraries directly.
/// Implemented in Infrastructure.
/// </summary>
public interface ITokenService
{
    /// <summary>Issues a short-lived, signed JWT access token carrying the user's id, email and role claims.</summary>
    AccessToken CreateAccessToken(User user);

    /// <summary>Generates a new cryptographically random raw refresh token. Returned to the client exactly once — never persisted raw.</summary>
    string GenerateRefreshToken();

    /// <summary>SHA-256 hashes a raw refresh token for storage/lookup.</summary>
    string HashRefreshToken(string rawToken);

    /// <summary>How long a freshly-issued refresh token remains valid.</summary>
    TimeSpan RefreshTokenLifetime { get; }
}

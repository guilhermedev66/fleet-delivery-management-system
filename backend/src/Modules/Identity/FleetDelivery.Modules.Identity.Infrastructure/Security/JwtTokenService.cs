using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FleetDelivery.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Issues short-lived HMAC-SHA256-signed JWT access tokens and
/// cryptographically random refresh tokens.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    // Spec: access token lifetime is fixed at 15 minutes.
    private static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);

    // Not spec-mandated beyond "cryptographically random, rotate on use" — 7 days
    // is a reasonable default for an httpOnly-cookie-scoped refresh token.
    // Documented in backend/README.md.
    public TimeSpan RefreshTokenLifetime { get; } = TimeSpan.FromDays(7);

    private readonly JwtOptions _options = jwtOptions.Value;

    public AccessToken CreateAccessToken(User user)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.Add(AccessTokenLifetime);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email.Value),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var value = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(value, expiresAt);
    }

    public string GenerateRefreshToken()
    {
        // 256-bit cryptographically random value, base64url-encoded so it's
        // cookie/header-safe without escaping.
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Base64UrlEncoder.Encode(bytes);
    }

    public string HashRefreshToken(string rawToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));

        return Convert.ToHexString(bytes);
    }
}

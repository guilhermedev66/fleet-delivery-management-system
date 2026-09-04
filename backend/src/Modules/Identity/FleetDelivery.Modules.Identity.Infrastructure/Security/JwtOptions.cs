namespace FleetDelivery.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Bound from the <c>Jwt</c> configuration section. The base
/// <c>appsettings.json</c> ships this with empty values on purpose —
/// production must supply real values via environment variables
/// (<c>Jwt__Issuer</c>, <c>Jwt__Audience</c>, <c>Jwt__SigningKey</c>).
/// <c>appsettings.Development.json</c> carries an actual dev-only signing
/// key since it's a tracked, non-secret local-dev convenience.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    /// <summary>HMAC-SHA256 signing key. Must be at least 256 bits (32 bytes) once UTF-8 encoded.</summary>
    public string SigningKey { get; set; } = string.Empty;
}

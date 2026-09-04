using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace FleetDelivery.Modules.Identity.Infrastructure.Security;

/// <summary>
/// Wraps <see cref="Microsoft.AspNetCore.Identity.PasswordHasher{TUser}"/>
/// (PBKDF2-HMAC-SHA256, ASP.NET Core Identity's v3 format) without pulling
/// in the full ASP.NET Core Identity/UserManager machinery — just the
/// hasher class.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _inner = new();

    public string Hash(string password) =>
        // PasswordHasher<TUser>.HashPassword doesn't actually read any data off
        // the `user` argument (only the password) — passing null is safe and
        // avoids requiring a real User instance before one exists.
        _inner.HashPassword(null!, password);

    public bool Verify(string passwordHash, string providedPassword)
    {
        var result = _inner.VerifyHashedPassword(null!, passwordHash, providedPassword);

        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}

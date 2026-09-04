namespace FleetDelivery.Modules.Identity.Application.Abstractions;

/// <summary>
/// Abstracts password hashing so Application handlers don't depend on
/// <c>Microsoft.AspNetCore.Identity</c> directly. Implemented in
/// Infrastructure via <c>PasswordHasher&lt;User&gt;</c>.
/// </summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string providedPassword);
}

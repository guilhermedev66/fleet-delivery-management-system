using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Domain;
using FleetDelivery.Modules.Identity.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace FleetDelivery.Modules.Identity.Infrastructure.Seeding;

/// <summary>
/// Seeds a single dev-only Admin user when none exists yet. Callers MUST
/// only invoke this when <c>IHostEnvironment.IsDevelopment()</c> is true —
/// this class does not check the environment itself, to keep that decision
/// visible at the call site in Program.cs rather than hidden in here.
///
/// Dev admin credentials (never valid outside Development):
///   email:    admin@fleetdelivery.local
///   password: Dev!Passw0rd123
/// Also documented in backend/README.md.
/// </summary>
public static class IdentityDevSeeder
{
    public const string DevAdminEmail = "admin@fleetdelivery.local";

    // Dev-only convenience credential — never used outside ASPNETCORE_ENVIRONMENT=Development.
    public const string DevAdminPassword = "Dev!Passw0rd123";

    public static async Task SeedAsync(
        IdentityDbContext dbContext,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (await userRepository.AnyAsync(cancellationToken))
        {
            return;
        }

        var passwordHash = passwordHasher.Hash(DevAdminPassword);
        var admin = User.Register(DevAdminEmail, passwordHash, "Dev Admin", Role.Admin);

        userRepository.Add(admin);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Seeded dev-only Admin user {Email} — this only happens in the Development environment.",
            DevAdminEmail);
    }
}

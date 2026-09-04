using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Domain;
using FleetDelivery.Modules.Identity.Infrastructure.Persistence;
using FleetDelivery.Modules.Shipments.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FleetDelivery.IntegrationTests.Infrastructure;

/// <summary>
/// Same shape as <see cref="IdentityApiFactory"/> (Testcontainers Postgres +
/// the real <c>FleetDelivery.Api</c> host in Development, so its own
/// auto-migrate path sets up both the "identity" and "shipments" schemas),
/// plus a helper to seed non-Admin users (Dispatcher, Driver) directly via
/// <see cref="IUserRepository"/> — there's no self-registration endpoint, and
/// <c>IdentityDevSeeder</c> only ever seeds one Admin.
/// </summary>
public sealed class ShipmentsApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DefaultPassword = "Dev!Passw0rd123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("fleet_delivery_test")
        .WithUsername("fleet_test")
        .WithPassword("fleet_test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = _postgres.GetConnectionString(),
                ["Jwt:Issuer"] = "FleetDelivery.IntegrationTests",
                ["Jwt:Audience"] = "FleetDelivery.IntegrationTests.Client",
                ["Jwt:SigningKey"] = "integration-test-signing-key-not-for-production-9876543210",
                ["Cors:AllowedOrigin"] = "http://localhost:5173",
                ["RateLimiting:Login:PermitLimit"] = "1000",
                ["RateLimiting:Login:WindowSeconds"] = "60",
            });
        });
    }

    public IdentityDbContext CreateIdentityDbContext() => Services.CreateScope().ServiceProvider.GetRequiredService<IdentityDbContext>();

    public ShipmentsDbContext CreateShipmentsDbContext() => Services.CreateScope().ServiceProvider.GetRequiredService<ShipmentsDbContext>();

    /// <summary>Seeds a user with the given role directly via <see cref="IUserRepository"/> and returns their credentials for logging in through the real <c>/api/auth/login</c> endpoint.</summary>
    public async Task<(Guid Id, string Email, string Password)> CreateUserAsync(Role role, string? fullName = null)
    {
        using var scope = Services.CreateScope();
        var provider = scope.ServiceProvider;

        var userRepository = provider.GetRequiredService<IUserRepository>();
        var passwordHasher = provider.GetRequiredService<IPasswordHasher>();
        var unitOfWork = provider.GetRequiredService<IUnitOfWork>();

        var email = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}@fleetdelivery.local";
        var passwordHash = passwordHasher.Hash(DefaultPassword);
        var user = User.Register(email, passwordHash, fullName ?? $"Test {role}", role);

        userRepository.Add(user);
        await unitOfWork.SaveChangesAsync();

        return (user.Id, email, DefaultPassword);
    }
}

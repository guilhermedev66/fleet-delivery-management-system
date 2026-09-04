using FleetDelivery.Modules.Identity.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace FleetDelivery.IntegrationTests.Infrastructure;

/// <summary>
/// Spins up a real, disposable Postgres via Testcontainers and points the
/// full <c>FleetDelivery.Api</c> host at it. Runs the host in the
/// "Development" environment so Program.cs's own dev-only migration +
/// admin-seed path does the schema setup — the same code path that runs
/// in real local dev, rather than a parallel test-only setup.
///
/// Requires a working Docker daemon. If InitializeAsync can't reach one,
/// these tests will fail to start their container — see backend/README.md
/// for the Docker/Testcontainers notes (they run fine on GitHub Actions'
/// ubuntu-latest runners either way).
/// </summary>
public sealed class IdentityApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
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

                // TestServer collapses every request onto the same synthetic
                // connection, so the per-IP login rate limiter would otherwise
                // trip after 5 login calls across the *entire* test class
                // (which shares this one host via IClassFixture). Loosened
                // here only — production/dev keep the real 5/minute default.
                ["RateLimiting:Login:PermitLimit"] = "1000",
                ["RateLimiting:Login:WindowSeconds"] = "60",
            });
        });
    }

    /// <summary>Resolves a scoped <see cref="IdentityDbContext"/> for direct DB assertions/setup in tests.</summary>
    public IdentityDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    }
}

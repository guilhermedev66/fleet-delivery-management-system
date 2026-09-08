using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Domain;
using FleetDelivery.Modules.Identity.Infrastructure.Persistence;
using FleetDelivery.Modules.Shipments.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace FleetDelivery.IntegrationTests.Infrastructure;

/// <summary>
/// Same shape as <see cref="ShipmentsApiFactory"/>, plus a real, disposable
/// RabbitMQ broker (Testcontainers) so M4's publisher tests can exercise a
/// genuine publish, not a mock. The hosted <c>OutboxPublisherHostedService</c>
/// is still disabled (<c>Outbox:PublisherEnabled = false</c>) even here —
/// tests resolve <c>OutboxBatchProcessor</c> directly and call
/// <c>ProcessBatchAsync</c> on their own schedule, for determinism (no
/// waiting out a poll interval, no flakiness from a background timer racing
/// test assertions).
/// </summary>
public sealed class OutboxPublisherApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DefaultPassword = "Dev!Passw0rd123";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("fleet_delivery_outbox_test")
        .WithUsername("fleet_test")
        .WithPassword("fleet_test_password")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management-alpine")
        .WithUsername("fleet_test")
        .WithPassword("fleet_test_password")
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await Task.WhenAll(_postgres.StopAsync(), _rabbitMq.StopAsync());
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
                ["Outbox:PublisherEnabled"] = "false",
                ["RabbitMq:Host"] = _rabbitMq.Hostname,
                ["RabbitMq:Port"] = _rabbitMq.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:Username"] = "fleet_test",
                ["RabbitMq:Password"] = "fleet_test_password",
            });
        });
    }

    public ShipmentsDbContext CreateShipmentsDbContext() => Services.CreateScope().ServiceProvider.GetRequiredService<ShipmentsDbContext>();

    public string RabbitMqConnectionString => _rabbitMq.GetConnectionString();

    /// <summary>Same helper as <see cref="ShipmentsApiFactory.CreateUserAsync"/> — duplicated rather than shared because the two factories intentionally don't share a base class (different container sets).</summary>
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

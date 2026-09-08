using FleetDelivery.BuildingBlocks.Messaging;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Infrastructure.Messaging;
using FleetDelivery.Modules.Shipments.Infrastructure.Persistence;
using FleetDelivery.Modules.Shipments.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetDelivery.Modules.Shipments.Infrastructure;

/// <summary>Composition-root wiring for the Shipments module. Called once from <c>FleetDelivery.Api</c>'s <c>Program.cs</c>, mirroring <c>AddIdentityModule</c>.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddShipmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ShipmentsDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", ShipmentsDbContext.Schema)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ShipmentsDbContext>());
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IProofOfDeliveryPhotoRepository, ProofOfDeliveryPhotoRepository>();

        services.AddOutboxPublisher(configuration);

        return services;
    }

    /// <summary>
    /// M4: drains <c>shipments.outbox_messages</c> to RabbitMQ. Split out of
    /// <see cref="AddShipmentsModule"/> only for readability — still called
    /// from there, not from <c>Program.cs</c> directly.
    /// </summary>
    private static IServiceCollection AddOutboxPublisher(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.Configure<OutboxPublisherOptions>(configuration.GetSection(OutboxPublisherOptions.SectionName));

        services.AddSingleton<RabbitMqConnectionProvider>();
        services.AddSingleton<IIntegrationEventPublisher, RabbitMqIntegrationEventPublisher>();
        services.AddScoped<OutboxBatchProcessor>();

        // Always registered — OutboxPublisherHostedService checks
        // OutboxPublisherOptions.PublisherEnabled itself, inside
        // ExecuteAsync, via the DI-resolved IOptions<T> (see its own doc
        // comment for why a registration-time IConfiguration read here
        // would silently miss WebApplicationFactory's test overrides).
        services.AddHostedService<OutboxPublisherHostedService>();

        return services;
    }
}

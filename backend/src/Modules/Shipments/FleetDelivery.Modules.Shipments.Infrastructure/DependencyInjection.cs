using FleetDelivery.Modules.Shipments.Application.Abstractions;
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

        return services;
    }
}

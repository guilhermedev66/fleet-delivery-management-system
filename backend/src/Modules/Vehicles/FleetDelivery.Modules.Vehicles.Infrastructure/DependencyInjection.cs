using FleetDelivery.Modules.Vehicles.Application.Abstractions;
using FleetDelivery.Modules.Vehicles.Infrastructure.Persistence;
using FleetDelivery.Modules.Vehicles.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetDelivery.Modules.Vehicles.Infrastructure;

/// <summary>Composition-root wiring for the Vehicles module. Called once from <c>FleetDelivery.Api</c>'s <c>Program.cs</c>, mirroring <c>AddShipmentsModule</c>.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddVehiclesModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<VehiclesDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("Postgres"),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", VehiclesDbContext.Schema)));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<VehiclesDbContext>());
        services.AddScoped<IVehicleRepository, VehicleRepository>();

        return services;
    }
}

using System.Reflection;
using FleetDelivery.Modules.Vehicles.Application.Abstractions;
using FleetDelivery.Modules.Vehicles.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FleetDelivery.Modules.Vehicles.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Vehicles module. Owns the "vehicles" PostgreSQL
/// schema exclusively. No Outbox wiring — unlike Shipments, nothing in the
/// product needs a <c>VehicleRegistered</c> integration event yet (see the
/// class-level comment on <see cref="Vehicle"/>); add it if a real consumer
/// shows up instead of speculatively wiring an unused Outbox table now.
/// </summary>
public sealed class VehiclesDbContext(DbContextOptions<VehiclesDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "vehicles";

    /// <summary>Postgres error code for a unique-constraint violation.</summary>
    private const string UniqueViolationSqlState = "23505";

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: UniqueViolationSqlState })
        {
            throw new DuplicatePlateNumberException(ex);
        }
    }

    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);
}

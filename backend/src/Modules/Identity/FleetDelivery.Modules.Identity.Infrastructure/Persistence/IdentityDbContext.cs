using System.Reflection;
using FleetDelivery.Modules.Identity.Application.Abstractions;
using FleetDelivery.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetDelivery.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Identity module. Owns the "identity" PostgreSQL
/// schema exclusively — per the modular-monolith rule, no other module's
/// DbContext maps into this schema, and this context never maps into
/// another module's schema.
/// </summary>
public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "identity";

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    // Explicit implementation: DbContext.SaveChangesAsync returns Task<int>,
    // which is not a structural match for IUnitOfWork's Task-returning member.
    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);
}

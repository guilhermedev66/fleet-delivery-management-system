using System.Reflection;
using System.Text.Json;
using FleetDelivery.BuildingBlocks.Domain;
using FleetDelivery.BuildingBlocks.Messaging;
using FleetDelivery.Modules.Shipments.Application.Abstractions;
using FleetDelivery.Modules.Shipments.Domain;
using Microsoft.EntityFrameworkCore;

namespace FleetDelivery.Modules.Shipments.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the Shipments module. Owns the "shipments" PostgreSQL
/// schema exclusively.
///
/// Outbox wiring: <see cref="SaveChangesAsync(System.Threading.CancellationToken)"/>
/// is overridden to, before the real save, walk every tracked
/// <see cref="AggregateRoot{Guid}"/> with pending domain events, write one
/// <see cref="OutboxMessage"/> row per event to <see cref="OutboxMessages"/>
/// (same <see cref="DbContext"/>, same upcoming <c>SaveChanges</c> call), and
/// clear the aggregate's pending events. Because this all happens inside one
/// <c>SaveChangesAsync</c>/transaction, the state change and its outbox row
/// commit together or not at all — see docs/ARCHITECTURE.md §Outbox. No
/// RabbitMQ publisher runs against these rows yet (that's M4); they sit
/// unpublished (<c>ProcessedOn</c> null) until then.
///
/// <see cref="OutboxMessages"/> is its own table in the "shipments" schema
/// rather than a table shared across module schemas — deliberate duplication
/// for now (Identity doesn't have Outbox wiring yet either), per YAGNI. Worth
/// extracting this whole SaveChangesAsync-interceptor pattern into
/// BuildingBlocks once a second module needs identical wiring.
/// </summary>
public sealed class ShipmentsDbContext(DbContextOptions<ShipmentsDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "shipments";

    public DbSet<Shipment> Shipments => Set<Shipment>();

    public DbSet<ProofOfDeliveryPhoto> ProofOfDeliveryPhotos => Set<ProofOfDeliveryPhoto>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AppendOutboxMessagesForPendingDomainEvents();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translated to an Application-layer exception so command
            // handlers never need to reference EF Core themselves.
            throw new ConcurrencyConflictException(ex);
        }
    }

    // Explicit implementation: DbContext.SaveChangesAsync returns Task<int>,
    // which is not a structural match for IUnitOfWork's Task-returning member.
    Task IUnitOfWork.SaveChangesAsync(CancellationToken cancellationToken) => SaveChangesAsync(cancellationToken);

    // camelCase (Web defaults) to match every other JSON contract this API
    // emits, including the M4 RabbitMQ envelope this content ends up nested
    // inside (see RabbitMqIntegrationEventPublisher) — a consumer shouldn't
    // have to special-case this payload's casing versus everything else.
    private static readonly JsonSerializerOptions OutboxContentSerializerOptions = new(JsonSerializerDefaults.Web);

    private void AppendOutboxMessagesForPendingDomainEvents()
    {
        var aggregatesWithPendingEvents = ChangeTracker
            .Entries<AggregateRoot<Guid>>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregatesWithPendingEvents)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    Type = domainEvent.GetType().Name,
                    Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), OutboxContentSerializerOptions),
                    OccurredOn = DateTimeOffset.UtcNow,
                });
            }

            aggregate.ClearDomainEvents();
        }
    }
}

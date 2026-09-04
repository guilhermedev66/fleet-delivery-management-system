namespace FleetDelivery.BuildingBlocks.Domain;

/// <summary>
/// Base class for aggregate roots. Accumulates domain events raised during
/// state transitions; the events are dispatched by infrastructure (e.g. an
/// EF Core <c>SaveChanges</c> interceptor) after the transaction commits,
/// then cleared.
/// </summary>
/// <typeparam name="TId">The type of the aggregate's identifier.</typeparam>
public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id)
        : base(id)
    {
    }

    protected AggregateRoot()
    {
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();
}

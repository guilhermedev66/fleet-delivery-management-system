using MediatR;

namespace FleetDelivery.BuildingBlocks.Domain;

/// <summary>
/// Marker interface for domain events: in-process notifications raised by an
/// <see cref="AggregateRoot{TId}"/> and dispatched via MediatR after
/// <c>SaveChanges</c> succeeds (same transaction, same module).
///
/// Not to be confused with an integration event (see
/// <see cref="Messaging.IntegrationEvent"/>), which crosses module or
/// process boundaries over RabbitMQ.
/// </summary>
public interface IDomainEvent : INotification
{
}

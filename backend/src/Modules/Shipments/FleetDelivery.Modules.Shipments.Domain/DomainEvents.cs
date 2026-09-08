using FleetDelivery.BuildingBlocks.Domain;

namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>
/// In-process domain events raised by <see cref="Shipment"/>'s transition
/// methods. Dispatched via MediatR after <c>SaveChanges</c> succeeds (same
/// transaction/module). The subset called out in docs/ARCHITECTURE.md's
/// "Event-driven architecture" section is also written to the Outbox (see
/// <c>ShipmentsDbContext.SaveChangesAsync</c>) as the corresponding
/// integration event, for M4's RabbitMQ publisher to pick up later.
/// </summary>
public sealed record ShipmentCreated(Guid ShipmentId, string TrackingNumber, Guid CreatedByUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ShipmentReadyForDispatch(Guid ShipmentId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DriverAssigned(Guid ShipmentId, Guid DriverId, Guid VehicleId, Guid AssignedByUserId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ShipmentPickedUp(Guid ShipmentId, Guid DriverId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ShipmentInTransit(Guid ShipmentId, Guid DriverId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ShipmentOutForDelivery(Guid ShipmentId, Guid DriverId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DeliveryCompleted(Guid ShipmentId, Guid DriverId, string RecipientName, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DeliveryFailed(Guid ShipmentId, Guid DriverId, string Reason, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DeliveryRescheduled(Guid ShipmentId, Guid RescheduledByUserId, DateTimeOffset OccurredOn) : IDomainEvent;

/// <summary>
/// Not in docs/ARCHITECTURE.md's original integration-event list — added as
/// the natural symmetric counterpart to the other terminal-state events
/// (<see cref="DeliveryCompleted"/>, <see cref="ShipmentCancelled"/>) so
/// "returned to origin" is observable the same way they are.
/// </summary>
public sealed record ShipmentReturned(Guid ShipmentId, Guid ReturnedByUserId, string Reason, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record ShipmentCancelled(Guid ShipmentId, Guid CancelledByUserId, string Reason, DateTimeOffset OccurredOn) : IDomainEvent;

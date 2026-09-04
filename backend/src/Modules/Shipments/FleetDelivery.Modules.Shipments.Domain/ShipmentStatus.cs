namespace FleetDelivery.Modules.Shipments.Domain;

/// <summary>
/// Lifecycle status of a <see cref="Shipment"/>. Transitions between these
/// are only ever made via <see cref="Shipment"/>'s explicit transition
/// methods, each validated against the transition table in
/// <see cref="Shipment"/> — never assigned directly.
/// </summary>
public enum ShipmentStatus
{
    Draft,
    ReadyForDispatch,
    Assigned,
    PickedUp,
    InTransit,
    OutForDelivery,
    Delivered,
    DeliveryFailed,
    Rescheduled,
    Returned,
    Cancelled,
}

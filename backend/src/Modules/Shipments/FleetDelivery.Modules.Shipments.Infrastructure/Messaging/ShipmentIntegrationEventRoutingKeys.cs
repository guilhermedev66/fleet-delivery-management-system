namespace FleetDelivery.Modules.Shipments.Infrastructure.Messaging;

/// <summary>
/// Maps an outbox row's <c>Type</c> (the domain event's CLR type name, see
/// <c>ShipmentsDbContext.AppendOutboxMessagesForPendingDomainEvents</c>) to
/// the RabbitMQ routing key it publishes under, per docs/ARCHITECTURE.md's
/// topic naming (<c>shipment.delivered</c>, <c>driver.assigned</c>, ...).
/// Every domain event Shipments currently raises already corresponds to a
/// listed integration event — nothing here filters anything out.
/// </summary>
public static class ShipmentIntegrationEventRoutingKeys
{
    public const string UnknownRoutingKey = "shipment.unknown";

    private static readonly IReadOnlyDictionary<string, string> RoutingKeysByEventType = new Dictionary<string, string>
    {
        ["ShipmentCreated"] = "shipment.created",
        ["ShipmentReadyForDispatch"] = "shipment.ready-for-dispatch",
        ["DriverAssigned"] = "driver.assigned",
        ["ShipmentPickedUp"] = "shipment.picked-up",
        ["ShipmentInTransit"] = "shipment.in-transit",
        ["ShipmentOutForDelivery"] = "shipment.out-for-delivery",
        ["DeliveryCompleted"] = "shipment.delivered",
        ["DeliveryFailed"] = "shipment.delivery-failed",
        ["DeliveryRescheduled"] = "shipment.rescheduled",
        ["ShipmentReturned"] = "shipment.returned",
        ["ShipmentCancelled"] = "shipment.cancelled",
    };

    /// <summary>Falls back to <see cref="UnknownRoutingKey"/> for a type this mapping hasn't been updated for yet, rather than throwing and blocking the whole batch — logged as a warning by the caller so the gap gets noticed and fixed.</summary>
    public static string For(string eventType) =>
        RoutingKeysByEventType.GetValueOrDefault(eventType, UnknownRoutingKey);
}

import type { DispatchFeedEvent } from './useDispatchEvents'

// Every shipment integration event's `data` carries at least `shipmentId`;
// a few (ShipmentCreated) also carry `trackingNumber`, which reads better
// when present. Deliberately generic rather than one bespoke sentence per
// event type — the type list (see backend's ShipmentIntegrationEventRoutingKeys)
// already grows with the domain, and this stays correct without editing in
// lockstep.
const EVENT_LABELS: Record<string, string> = {
  ShipmentCreated: 'Shipment created',
  ShipmentReadyForDispatch: 'Ready for dispatch',
  DriverAssigned: 'Driver & vehicle assigned',
  ShipmentPickedUp: 'Picked up',
  ShipmentInTransit: 'In transit',
  ShipmentOutForDelivery: 'Out for delivery',
  DeliveryCompleted: 'Delivered',
  DeliveryFailed: 'Delivery failed',
  DeliveryRescheduled: 'Rescheduled',
  ShipmentReturned: 'Returned to origin',
  ShipmentCancelled: 'Cancelled',
}

export function describeEvent(event: DispatchFeedEvent): { label: string; subject: string } {
  const label = EVENT_LABELS[event.type] ?? event.type
  const trackingNumber =
    typeof event.data.trackingNumber === 'string' ? event.data.trackingNumber : undefined
  const shipmentId = typeof event.data.shipmentId === 'string' ? event.data.shipmentId : undefined

  return { label, subject: trackingNumber ?? shipmentId ?? '—' }
}

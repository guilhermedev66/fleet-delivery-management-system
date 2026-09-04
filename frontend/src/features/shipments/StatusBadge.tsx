import { cx } from '../../lib/utils'
import type { ShipmentStatus } from '../../lib/api/shipments'

type BadgeVariant = 'success' | 'danger' | 'warning' | 'neutral'

// Terminal-success -> green, terminal-failure/cancelled -> muted red,
// needs-attention -> amber, everything still in flight -> neutral blue.
const STATUS_VARIANT: Record<ShipmentStatus, BadgeVariant> = {
  Draft: 'neutral',
  ReadyForDispatch: 'neutral',
  Assigned: 'neutral',
  PickedUp: 'neutral',
  InTransit: 'neutral',
  OutForDelivery: 'neutral',
  Delivered: 'success',
  DeliveryFailed: 'danger',
  Rescheduled: 'warning',
  Returned: 'danger',
  Cancelled: 'danger',
}

const STATUS_LABEL: Record<ShipmentStatus, string> = {
  Draft: 'Draft',
  ReadyForDispatch: 'Ready for Dispatch',
  Assigned: 'Assigned',
  PickedUp: 'Picked Up',
  InTransit: 'In Transit',
  OutForDelivery: 'Out for Delivery',
  Delivered: 'Delivered',
  DeliveryFailed: 'Delivery Failed',
  Rescheduled: 'Rescheduled',
  Returned: 'Returned',
  Cancelled: 'Cancelled',
}

const VARIANT_CLASSES: Record<BadgeVariant, string> = {
  success: 'bg-[var(--badge-success-bg)] text-[var(--badge-success-text)]',
  danger: 'bg-[var(--badge-danger-bg)] text-[var(--badge-danger-text)]',
  warning: 'bg-[var(--badge-warning-bg)] text-[var(--badge-warning-text)]',
  neutral: 'bg-[var(--badge-neutral-bg)] text-[var(--badge-neutral-text)]',
}

export function ShipmentStatusBadge({ status }: { status: ShipmentStatus }) {
  return (
    <span
      className={cx(
        'inline-flex w-fit items-center rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap',
        VARIANT_CLASSES[STATUS_VARIANT[status]],
      )}
    >
      {STATUS_LABEL[status]}
    </span>
  )
}

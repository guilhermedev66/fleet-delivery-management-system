import { cx } from '../../lib/utils'
import type { VehicleStatus } from '../../lib/api/vehicles'

type BadgeVariant = 'success' | 'danger' | 'warning' | 'neutral'

// In service -> green, retired -> muted red, in maintenance -> amber.
const STATUS_VARIANT: Record<VehicleStatus, BadgeVariant> = {
  Active: 'success',
  Maintenance: 'warning',
  Retired: 'danger',
}

const VARIANT_CLASSES: Record<BadgeVariant, string> = {
  success: 'bg-[var(--badge-success-bg)] text-[var(--badge-success-text)]',
  danger: 'bg-[var(--badge-danger-bg)] text-[var(--badge-danger-text)]',
  warning: 'bg-[var(--badge-warning-bg)] text-[var(--badge-warning-text)]',
  neutral: 'bg-[var(--badge-neutral-bg)] text-[var(--badge-neutral-text)]',
}

export function VehicleStatusBadge({ status }: { status: VehicleStatus }) {
  return (
    <span
      className={cx(
        'inline-flex w-fit items-center rounded-full px-2.5 py-0.5 text-xs font-medium whitespace-nowrap',
        VARIANT_CLASSES[STATUS_VARIANT[status]],
      )}
    >
      {status}
    </span>
  )
}

import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../auth/authStore'
import type { ShipmentStatus } from '../../lib/api/shipments'
import { useShipments } from './hooks'
import { ShipmentStatusBadge } from './StatusBadge'

const STATUS_OPTIONS: ShipmentStatus[] = [
  'Draft',
  'ReadyForDispatch',
  'Assigned',
  'PickedUp',
  'InTransit',
  'OutForDelivery',
  'Delivered',
  'DeliveryFailed',
  'Rescheduled',
  'Returned',
  'Cancelled',
]

const PAGE_SIZE = 20

function formatDate(iso: string): string {
  return new Date(iso).toLocaleString()
}

function SkeletonRows() {
  return (
    <div className="flex flex-col gap-2">
      {Array.from({ length: 5 }).map((_, index) => (
        <div key={index} className="h-14 animate-pulse rounded-md bg-[var(--color-bg-subtle)]" />
      ))}
    </div>
  )
}

export function ShipmentsListPage() {
  const [status, setStatus] = useState<ShipmentStatus | ''>('')
  const [page, setPage] = useState(1)
  const user = useAuthStore((state) => state.user)
  const canCreate = user?.role === 'Dispatcher' || user?.role === 'Admin'

  const { data, isLoading, isError } = useShipments({
    status: status || undefined,
    page,
    pageSize: PAGE_SIZE,
  })

  const totalPages = data ? Math.max(1, Math.ceil(data.totalCount / data.pageSize)) : 1

  function handleStatusChange(value: string) {
    setStatus(value as ShipmentStatus | '')
    setPage(1)
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Shipments</h1>
        {canCreate && (
          <Link
            to="/shipments/new"
            className="rounded-md bg-[var(--color-accent)] px-4 py-2 text-sm font-medium text-[var(--color-accent-text)]"
          >
            New Shipment
          </Link>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <label htmlFor="status-filter" className="text-sm font-medium">
          Status
        </label>
        <select
          id="status-filter"
          value={status}
          onChange={(event) => handleStatusChange(event.target.value)}
          className="rounded-md border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm"
        >
          <option value="">All statuses</option>
          {STATUS_OPTIONS.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
      </div>

      {isLoading && <SkeletonRows />}

      {isError && !isLoading && (
        <p role="alert" className="text-sm text-[var(--color-danger)]">
          Couldn't load shipments. Please try again.
        </p>
      )}

      {!isLoading && !isError && data && data.items.length === 0 && (
        <div className="rounded-lg border border-dashed border-[var(--color-border)] p-8 text-center">
          <p className="text-sm text-[var(--color-text-muted)]">No shipments found.</p>
        </div>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <>
          {/* Desktop table */}
          <div className="hidden overflow-x-auto rounded-lg border border-[var(--color-border)] md:block">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-[var(--color-border)] bg-[var(--color-bg-subtle)]">
                <tr>
                  <th className="px-4 py-2 font-medium">Tracking #</th>
                  <th className="px-4 py-2 font-medium">Recipient</th>
                  <th className="px-4 py-2 font-medium">Status</th>
                  <th className="px-4 py-2 font-medium">Created</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((shipment) => (
                  <tr
                    key={shipment.id}
                    className="border-b border-[var(--color-border)] last:border-0 hover:bg-[var(--color-bg-subtle)]"
                  >
                    <td className="px-4 py-3">
                      <Link
                        to={`/shipments/${shipment.id}`}
                        className="font-medium text-[var(--color-accent)]"
                      >
                        {shipment.trackingNumber}
                      </Link>
                    </td>
                    <td className="px-4 py-3">{shipment.recipientName}</td>
                    <td className="px-4 py-3">
                      <ShipmentStatusBadge status={shipment.status} />
                    </td>
                    <td className="px-4 py-3 text-[var(--color-text-muted)]">
                      {formatDate(shipment.createdAt)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Mobile cards */}
          <div className="flex flex-col gap-3 md:hidden">
            {data.items.map((shipment) => (
              <Link
                key={shipment.id}
                to={`/shipments/${shipment.id}`}
                className="flex flex-col gap-2 rounded-lg border border-[var(--color-border)] p-4"
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="font-medium text-[var(--color-accent)]">
                    {shipment.trackingNumber}
                  </span>
                  <ShipmentStatusBadge status={shipment.status} />
                </div>
                <p className="text-sm">{shipment.recipientName}</p>
                <p className="text-xs text-[var(--color-text-muted)]">
                  {formatDate(shipment.createdAt)}
                </p>
              </Link>
            ))}
          </div>

          <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-[var(--color-text-muted)]">
              Page {data.page} of {totalPages} · {data.totalCount} total
            </p>
            <div className="flex gap-2">
              <button
                type="button"
                onClick={() => setPage((current) => Math.max(1, current - 1))}
                disabled={page <= 1}
                className="rounded-md border border-[var(--color-border)] px-3 py-1.5 text-sm disabled:opacity-50"
              >
                Previous
              </button>
              <button
                type="button"
                onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
                disabled={page >= totalPages}
                className="rounded-md border border-[var(--color-border)] px-3 py-1.5 text-sm disabled:opacity-50"
              >
                Next
              </button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

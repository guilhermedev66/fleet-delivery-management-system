import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../auth/authStore'
import type { VehicleStatus } from '../../lib/api/vehicles'
import { useVehicles } from './hooks'
import { VehicleStatusBadge } from './StatusBadge'

const STATUS_OPTIONS: VehicleStatus[] = ['Active', 'Maintenance', 'Retired']

function SkeletonRows() {
  return (
    <div className="flex flex-col gap-2">
      {Array.from({ length: 5 }).map((_, index) => (
        <div key={index} className="h-14 animate-pulse rounded-md bg-[var(--color-bg-subtle)]" />
      ))}
    </div>
  )
}

export function VehiclesListPage() {
  const [status, setStatus] = useState<VehicleStatus | ''>('')
  const user = useAuthStore((state) => state.user)
  const canRegister = user?.role === 'Dispatcher' || user?.role === 'Admin'

  const { data, isLoading, isError } = useVehicles({ status: status || undefined })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Vehicles</h1>
        {canRegister && (
          <Link
            to="/vehicles/new"
            className="rounded-md bg-[var(--color-accent)] px-4 py-2 text-sm font-medium text-[var(--color-accent-text)]"
          >
            Register Vehicle
          </Link>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <label htmlFor="vehicle-status-filter" className="text-sm font-medium">
          Status
        </label>
        <select
          id="vehicle-status-filter"
          value={status}
          onChange={(event) => setStatus(event.target.value as VehicleStatus | '')}
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
          Couldn't load vehicles. Please try again.
        </p>
      )}

      {!isLoading && !isError && data && data.items.length === 0 && (
        <div className="rounded-lg border border-dashed border-[var(--color-border)] p-8 text-center">
          <p className="text-sm text-[var(--color-text-muted)]">No vehicles found.</p>
        </div>
      )}

      {!isLoading && !isError && data && data.items.length > 0 && (
        <>
          {/* Desktop table */}
          <div className="hidden overflow-x-auto rounded-lg border border-[var(--color-border)] md:block">
            <table className="w-full text-left text-sm">
              <thead className="border-b border-[var(--color-border)] bg-[var(--color-bg-subtle)]">
                <tr>
                  <th className="px-4 py-2 font-medium">Plate #</th>
                  <th className="px-4 py-2 font-medium">Type</th>
                  <th className="px-4 py-2 font-medium">Capacity (kg)</th>
                  <th className="px-4 py-2 font-medium">Status</th>
                </tr>
              </thead>
              <tbody>
                {data.items.map((vehicle) => (
                  <tr
                    key={vehicle.id}
                    className="border-b border-[var(--color-border)] last:border-0 hover:bg-[var(--color-bg-subtle)]"
                  >
                    <td className="px-4 py-3 font-medium">{vehicle.plateNumber}</td>
                    <td className="px-4 py-3">{vehicle.type}</td>
                    <td className="px-4 py-3">{vehicle.capacityKg}</td>
                    <td className="px-4 py-3">
                      <VehicleStatusBadge status={vehicle.status} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Mobile cards */}
          <div className="flex flex-col gap-3 md:hidden">
            {data.items.map((vehicle) => (
              <div
                key={vehicle.id}
                className="flex flex-col gap-2 rounded-lg border border-[var(--color-border)] p-4"
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="font-medium">{vehicle.plateNumber}</span>
                  <VehicleStatusBadge status={vehicle.status} />
                </div>
                <p className="text-sm">{vehicle.type}</p>
                <p className="text-xs text-[var(--color-text-muted)]">
                  {vehicle.capacityKg} kg capacity
                </p>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  )
}

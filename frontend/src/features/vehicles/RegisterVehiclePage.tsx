import { type FormEvent, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../../lib/api/client'
import type { VehicleType } from '../../lib/api/vehicles'
import { useRegisterVehicle } from './hooks'

const VEHICLE_TYPES: VehicleType[] = ['Van', 'Truck', 'Motorcycle', 'Car']

const INPUT_CLASS =
  'rounded-md border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-accent)] focus:border-[var(--color-accent)]'

export function RegisterVehiclePage() {
  const navigate = useNavigate()
  const registerVehicle = useRegisterVehicle()

  const [plateNumber, setPlateNumber] = useState('')
  const [type, setType] = useState<VehicleType>('Van')
  const [capacityKg, setCapacityKg] = useState('')
  const [touched, setTouched] = useState(false)

  const plateNumberError = plateNumber.trim() ? undefined : 'Required'
  const capacityValue = Number(capacityKg)
  const capacityError =
    capacityKg.trim() && capacityValue > 0 ? undefined : 'Enter a capacity greater than 0'

  const hasErrors = Boolean(plateNumberError) || Boolean(capacityError)

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setTouched(true)
    if (hasErrors) return
    registerVehicle.mutate(
      { plateNumber: plateNumber.trim(), type, capacityKg: capacityValue },
      { onSuccess: () => navigate('/vehicles') },
    )
  }

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6">
      <h1 className="text-xl font-semibold">Register Vehicle</h1>

      <form className="flex flex-col gap-4" onSubmit={handleSubmit} noValidate>
        <div className="flex flex-col gap-1">
          <label htmlFor="plateNumber" className="text-sm font-medium">
            Plate number
          </label>
          <input
            id="plateNumber"
            value={plateNumber}
            onChange={(event) => setPlateNumber(event.target.value)}
            aria-invalid={touched && Boolean(plateNumberError)}
            aria-describedby={touched && plateNumberError ? 'plateNumber-error' : undefined}
            className={INPUT_CLASS}
          />
          {touched && plateNumberError && (
            <p id="plateNumber-error" className="text-xs text-[var(--color-danger)]">
              {plateNumberError}
            </p>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="type" className="text-sm font-medium">
            Type
          </label>
          <select
            id="type"
            value={type}
            onChange={(event) => setType(event.target.value as VehicleType)}
            className={INPUT_CLASS}
          >
            {VEHICLE_TYPES.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor="capacityKg" className="text-sm font-medium">
            Capacity (kg)
          </label>
          <input
            id="capacityKg"
            type="number"
            min="0"
            value={capacityKg}
            onChange={(event) => setCapacityKg(event.target.value)}
            aria-invalid={touched && Boolean(capacityError)}
            aria-describedby={touched && capacityError ? 'capacityKg-error' : undefined}
            className={INPUT_CLASS}
          />
          {touched && capacityError && (
            <p id="capacityKg-error" className="text-xs text-[var(--color-danger)]">
              {capacityError}
            </p>
          )}
        </div>

        {registerVehicle.isError && (
          <p role="alert" className="text-sm text-[var(--color-danger)]">
            {registerVehicle.error instanceof ApiError
              ? "Couldn't register the vehicle. Please check the details and try again."
              : 'Something went wrong. Please try again.'}
          </p>
        )}

        <div className="flex gap-3">
          <button
            type="submit"
            disabled={registerVehicle.isPending}
            className="rounded-md bg-[var(--color-accent)] px-4 py-2 text-sm font-medium text-[var(--color-accent-text)] transition-opacity disabled:opacity-50"
          >
            {registerVehicle.isPending ? 'Registering…' : 'Register Vehicle'}
          </button>
          <Link
            to="/vehicles"
            className="rounded-md border border-[var(--color-border)] px-4 py-2 text-sm font-medium hover:bg-[var(--color-bg-subtle)]"
          >
            Cancel
          </Link>
        </div>
      </form>
    </div>
  )
}

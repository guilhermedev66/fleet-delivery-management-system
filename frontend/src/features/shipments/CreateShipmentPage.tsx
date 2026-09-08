import { type FormEvent, useState } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { ApiError } from '../../lib/api/client'
import { useCreateShipment } from './hooks'

interface AddressFormValues {
  street: string
  city: string
  state: string
  postalCode: string
  country: string
}

const EMPTY_ADDRESS: AddressFormValues = {
  street: '',
  city: '',
  state: '',
  postalCode: '',
  country: '',
}

const ADDRESS_FIELDS: Array<{ key: keyof AddressFormValues; label: string }> = [
  { key: 'street', label: 'Street' },
  { key: 'city', label: 'City' },
  { key: 'state', label: 'State' },
  { key: 'postalCode', label: 'Postal Code' },
  { key: 'country', label: 'Country' },
]

const INPUT_CLASS =
  'rounded-md border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-2 text-sm text-[var(--color-text)] outline-none focus-visible:ring-2 focus-visible:ring-[var(--color-accent)] focus:border-[var(--color-accent)]'

function validateAddress(
  address: AddressFormValues,
): Partial<Record<keyof AddressFormValues, string>> {
  const errors: Partial<Record<keyof AddressFormValues, string>> = {}
  for (const field of ADDRESS_FIELDS) {
    if (!address[field.key].trim()) errors[field.key] = 'Required'
  }
  return errors
}

function AddressFieldset({
  legend,
  idPrefix,
  value,
  onChange,
  errors,
}: {
  legend: string
  idPrefix: string
  value: AddressFormValues
  onChange: (value: AddressFormValues) => void
  errors: Partial<Record<keyof AddressFormValues, string>>
}) {
  function setField(field: keyof AddressFormValues, fieldValue: string) {
    onChange({ ...value, [field]: fieldValue })
  }

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="text-sm font-semibold">{legend}</legend>
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        {ADDRESS_FIELDS.map(({ key, label }) => {
          const id = `${idPrefix}-${key}`
          return (
            <div key={key} className="flex flex-col gap-1">
              <label htmlFor={id} className="text-sm font-medium">
                {label}
              </label>
              <input
                id={id}
                value={value[key]}
                onChange={(event) => setField(key, event.target.value)}
                aria-invalid={Boolean(errors[key])}
                aria-describedby={errors[key] ? `${id}-error` : undefined}
                className={INPUT_CLASS}
              />
              {errors[key] && (
                <p id={`${id}-error`} className="text-xs text-[var(--color-danger)]">
                  {errors[key]}
                </p>
              )}
            </div>
          )
        })}
      </div>
    </fieldset>
  )
}

export function CreateShipmentPage() {
  const navigate = useNavigate()
  const createShipment = useCreateShipment()

  const [recipientName, setRecipientName] = useState('')
  const [recipientPhone, setRecipientPhone] = useState('')
  const [origin, setOrigin] = useState<AddressFormValues>(EMPTY_ADDRESS)
  const [destination, setDestination] = useState<AddressFormValues>(EMPTY_ADDRESS)
  const [touched, setTouched] = useState(false)

  const originErrors = validateAddress(origin)
  const destinationErrors = validateAddress(destination)
  const recipientNameError = recipientName.trim() ? undefined : 'Required'
  const recipientPhoneError = recipientPhone.trim() ? undefined : 'Required'

  const hasErrors =
    Boolean(recipientNameError) ||
    Boolean(recipientPhoneError) ||
    Object.keys(originErrors).length > 0 ||
    Object.keys(destinationErrors).length > 0

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setTouched(true)
    if (hasErrors) return
    createShipment.mutate(
      { recipientName, recipientPhone, origin, destination },
      { onSuccess: (shipment) => navigate(`/shipments/${shipment.id}`) },
    )
  }

  return (
    <div className="mx-auto flex max-w-2xl flex-col gap-6">
      <h1 className="text-xl font-semibold">New Shipment</h1>

      <form className="flex flex-col gap-6" onSubmit={handleSubmit} noValidate>
        <div className="flex flex-col gap-4 sm:flex-row">
          <div className="flex flex-1 flex-col gap-1">
            <label htmlFor="recipientName" className="text-sm font-medium">
              Recipient name
            </label>
            <input
              id="recipientName"
              value={recipientName}
              onChange={(event) => setRecipientName(event.target.value)}
              aria-invalid={touched && Boolean(recipientNameError)}
              aria-describedby={touched && recipientNameError ? 'recipientName-error' : undefined}
              className={INPUT_CLASS}
            />
            {touched && recipientNameError && (
              <p id="recipientName-error" className="text-xs text-[var(--color-danger)]">
                {recipientNameError}
              </p>
            )}
          </div>
          <div className="flex flex-1 flex-col gap-1">
            <label htmlFor="recipientPhone" className="text-sm font-medium">
              Recipient phone
            </label>
            <input
              id="recipientPhone"
              value={recipientPhone}
              onChange={(event) => setRecipientPhone(event.target.value)}
              aria-invalid={touched && Boolean(recipientPhoneError)}
              aria-describedby={touched && recipientPhoneError ? 'recipientPhone-error' : undefined}
              className={INPUT_CLASS}
            />
            {touched && recipientPhoneError && (
              <p id="recipientPhone-error" className="text-xs text-[var(--color-danger)]">
                {recipientPhoneError}
              </p>
            )}
          </div>
        </div>

        <AddressFieldset
          legend="Origin"
          idPrefix="origin"
          value={origin}
          onChange={setOrigin}
          errors={touched ? originErrors : {}}
        />
        <AddressFieldset
          legend="Destination"
          idPrefix="destination"
          value={destination}
          onChange={setDestination}
          errors={touched ? destinationErrors : {}}
        />

        {createShipment.isError && (
          <p role="alert" className="text-sm text-[var(--color-danger)]">
            {createShipment.error instanceof ApiError
              ? "Couldn't create the shipment. Please check the details and try again."
              : 'Something went wrong. Please try again.'}
          </p>
        )}

        <div className="flex gap-3">
          <button
            type="submit"
            disabled={createShipment.isPending}
            className="rounded-md bg-[var(--color-accent)] px-4 py-2 text-sm font-medium text-[var(--color-accent-text)] transition-opacity disabled:opacity-50"
          >
            {createShipment.isPending ? 'Creating…' : 'Create Shipment'}
          </button>
          <Link
            to="/shipments"
            className="rounded-md border border-[var(--color-border)] px-4 py-2 text-sm font-medium hover:bg-[var(--color-bg-subtle)]"
          >
            Cancel
          </Link>
        </div>
      </form>
    </div>
  )
}

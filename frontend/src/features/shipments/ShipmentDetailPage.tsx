import { type FormEvent, type ReactNode, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import type { UseMutationResult, UseQueryResult } from '@tanstack/react-query'
import { ApiError } from '../../lib/api/client'
import type { Address, ShipmentResponse, ShipmentTimelineResponse } from '../../lib/api/shipments'
import { useAuthStore } from '../auth/authStore'
import {
  useAssignShipment,
  useCancelShipment,
  useDeliverShipment,
  useFailShipment,
  useInTransitShipment,
  useOutForDeliveryShipment,
  usePickupShipment,
  useReadyForDispatch,
  useRescheduleShipment,
  useReturnShipment,
  useShipment,
  useShipmentTimeline,
} from './hooks'
import { ShipmentStatusBadge } from './StatusBadge'

const ACTION_BUTTON_CLASS =
  'rounded-md bg-[var(--color-accent)] px-4 py-2 text-sm font-medium text-[var(--color-accent-text)] transition-opacity disabled:opacity-50'
const ACTION_CARD_CLASS =
  'flex min-w-64 flex-col gap-2 rounded-md border border-[var(--color-border)] p-3'
const FIELD_CLASS =
  'rounded-md border border-[var(--color-border)] bg-[var(--color-bg)] px-3 py-1.5 text-sm text-[var(--color-text)] outline-none focus:border-[var(--color-accent)]'

function ActionError({ error }: { error: unknown }) {
  if (!error) return null
  const message =
    error instanceof ApiError && error.status === 409
      ? 'This shipment was updated by someone else — refreshing.'
      : 'Something went wrong. Please try again.'
  return (
    <p role="alert" className="text-sm text-[var(--color-danger)]">
      {message}
    </p>
  )
}

function DetailCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="rounded-lg border border-[var(--color-border)] p-4">
      <p className="mb-2 text-xs font-medium tracking-wide text-[var(--color-text-muted)] uppercase">
        {title}
      </p>
      {children}
    </div>
  )
}

function AddressLines({ address }: { address: Address }) {
  return (
    <div className="text-sm">
      <p>{address.street}</p>
      <p>
        {address.city}, {address.state} {address.postalCode}
      </p>
      <p>{address.country}</p>
    </div>
  )
}

function DetailSkeleton() {
  return (
    <div className="flex flex-col gap-4">
      <div className="h-8 w-48 animate-pulse rounded-md bg-[var(--color-bg-subtle)]" />
      <div className="h-32 animate-pulse rounded-md bg-[var(--color-bg-subtle)]" />
    </div>
  )
}

function ShipmentTimelineList({ query }: { query: UseQueryResult<ShipmentTimelineResponse> }) {
  if (query.isLoading) {
    return (
      <div className="flex flex-col gap-2">
        {Array.from({ length: 3 }).map((_, index) => (
          <div key={index} className="h-10 animate-pulse rounded-md bg-[var(--color-bg-subtle)]" />
        ))}
      </div>
    )
  }

  if (query.isError || !query.data) {
    return <p className="text-sm text-[var(--color-text-muted)]">Couldn't load the timeline.</p>
  }

  if (query.data.events.length === 0) {
    return <p className="text-sm text-[var(--color-text-muted)]">No events yet.</p>
  }

  return (
    <ol className="flex flex-col gap-3 border-l border-[var(--color-border)] pl-4">
      {query.data.events.map((event, index) => (
        <li key={`${event.type}-${event.occurredAt}-${index}`} className="text-sm">
          <p className="font-medium">{event.type}</p>
          <p className="text-xs text-[var(--color-text-muted)]">
            {new Date(event.occurredAt).toLocaleString()}
          </p>
          {event.notes && <p className="text-xs text-[var(--color-text-muted)]">{event.notes}</p>}
        </li>
      ))}
    </ol>
  )
}

// -- One-click actions (Ready for Dispatch, Picked Up, In Transit, Out for
// Delivery, Reschedule) all send only `expectedVersion`. -------------------

function SimpleAction({
  label,
  pendingLabel,
  shipment,
  mutation,
}: {
  label: string
  pendingLabel: string
  shipment: ShipmentResponse
  mutation: UseMutationResult<ShipmentResponse, Error, string>
}) {
  return (
    <div className={ACTION_CARD_CLASS}>
      <button
        type="button"
        onClick={() => mutation.mutate(shipment.version)}
        disabled={mutation.isPending}
        className={ACTION_BUTTON_CLASS}
      >
        {mutation.isPending ? pendingLabel : label}
      </button>
      <ActionError error={mutation.error} />
    </div>
  )
}

function ReadyForDispatchAction({ shipment }: { shipment: ShipmentResponse }) {
  const mutation = useReadyForDispatch(shipment.id)
  return (
    <SimpleAction
      label="Ready for Dispatch"
      pendingLabel="Updating…"
      shipment={shipment}
      mutation={mutation}
    />
  )
}

function PickupAction({ shipment }: { shipment: ShipmentResponse }) {
  const mutation = usePickupShipment(shipment.id)
  return (
    <SimpleAction
      label="Picked Up"
      pendingLabel="Updating…"
      shipment={shipment}
      mutation={mutation}
    />
  )
}

function InTransitAction({ shipment }: { shipment: ShipmentResponse }) {
  const mutation = useInTransitShipment(shipment.id)
  return (
    <SimpleAction
      label="In Transit"
      pendingLabel="Updating…"
      shipment={shipment}
      mutation={mutation}
    />
  )
}

function OutForDeliveryAction({ shipment }: { shipment: ShipmentResponse }) {
  const mutation = useOutForDeliveryShipment(shipment.id)
  return (
    <SimpleAction
      label="Out for Delivery"
      pendingLabel="Updating…"
      shipment={shipment}
      mutation={mutation}
    />
  )
}

function RescheduleAction({ shipment }: { shipment: ShipmentResponse }) {
  const mutation = useRescheduleShipment(shipment.id)
  return (
    <SimpleAction
      label="Reschedule"
      pendingLabel="Updating…"
      shipment={shipment}
      mutation={mutation}
    />
  )
}

// -- Assign: no real driver directory yet (M3) — a plain text input for the
// driver's user id, documented as a rough edge. ---------------------------

function AssignAction({ shipment }: { shipment: ShipmentResponse }) {
  const [driverId, setDriverId] = useState('')
  const mutation = useAssignShipment(shipment.id)

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!driverId.trim()) return
    mutation.mutate({ driverId: driverId.trim(), expectedVersion: shipment.version })
  }

  return (
    <form onSubmit={handleSubmit} className={ACTION_CARD_CLASS}>
      <label htmlFor="assign-driver-id" className="text-sm font-medium">
        Assign to driver (user id)
      </label>
      <p className="text-xs text-[var(--color-text-muted)]">
        No driver directory yet (M3) — paste the driver's user id directly.
      </p>
      <div className="flex flex-wrap gap-2">
        <input
          id="assign-driver-id"
          value={driverId}
          onChange={(event) => setDriverId(event.target.value)}
          placeholder="Driver user id"
          className={`min-w-0 flex-1 ${FIELD_CLASS}`}
        />
        <button
          type="submit"
          disabled={mutation.isPending || !driverId.trim()}
          className={ACTION_BUTTON_CLASS}
        >
          {mutation.isPending ? 'Assigning…' : 'Assign'}
        </button>
      </div>
      <ActionError error={mutation.error} />
    </form>
  )
}

// -- Reason-required actions (Cancel, Return, Report Failed Delivery). -----

function ReasonForm({
  label,
  pendingLabel,
  isPending,
  error,
  onSubmit,
}: {
  label: string
  pendingLabel: string
  isPending: boolean
  error: unknown
  onSubmit: (reason: string) => void
}) {
  const [reason, setReason] = useState('')
  const fieldId = `reason-${label.toLowerCase().replace(/\s+/g, '-')}`

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!reason.trim()) return
    onSubmit(reason.trim())
  }

  return (
    <form onSubmit={handleSubmit} className={ACTION_CARD_CLASS}>
      <label htmlFor={fieldId} className="text-sm font-medium">
        {label} reason
      </label>
      <textarea
        id={fieldId}
        value={reason}
        onChange={(event) => setReason(event.target.value)}
        rows={2}
        className={FIELD_CLASS}
      />
      <button
        type="submit"
        disabled={isPending || !reason.trim()}
        className={`w-fit ${ACTION_BUTTON_CLASS}`}
      >
        {isPending ? pendingLabel : label}
      </button>
      <ActionError error={error} />
    </form>
  )
}

function CancelAction({ shipment }: { shipment: ShipmentResponse }) {
  const mutation = useCancelShipment(shipment.id)
  return (
    <ReasonForm
      label="Cancel"
      pendingLabel="Cancelling…"
      isPending={mutation.isPending}
      error={mutation.error}
      onSubmit={(reason) => mutation.mutate({ expectedVersion: shipment.version, reason })}
    />
  )
}

function ReturnAction({ shipment }: { shipment: ShipmentResponse }) {
  const mutation = useReturnShipment(shipment.id)
  return (
    <ReasonForm
      label="Return"
      pendingLabel="Returning…"
      isPending={mutation.isPending}
      error={mutation.error}
      onSubmit={(reason) => mutation.mutate({ expectedVersion: shipment.version, reason })}
    />
  )
}

function FailAction({ shipment }: { shipment: ShipmentResponse }) {
  const mutation = useFailShipment(shipment.id)
  return (
    <ReasonForm
      label="Report Failed Delivery"
      pendingLabel="Reporting…"
      isPending={mutation.isPending}
      error={mutation.error}
      onSubmit={(reason) => mutation.mutate({ expectedVersion: shipment.version, reason })}
    />
  )
}

// -- Deliver: optional recipient name / notes. ------------------------------

function DeliverAction({ shipment }: { shipment: ShipmentResponse }) {
  const [recipientName, setRecipientName] = useState('')
  const [notes, setNotes] = useState('')
  const mutation = useDeliverShipment(shipment.id)

  function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    mutation.mutate({
      expectedVersion: shipment.version,
      recipientName: recipientName.trim() || undefined,
      notes: notes.trim() || undefined,
    })
  }

  return (
    <form onSubmit={handleSubmit} className={ACTION_CARD_CLASS}>
      <div className="flex flex-col gap-1">
        <label htmlFor="deliver-recipient" className="text-sm font-medium">
          Recipient name (optional)
        </label>
        <input
          id="deliver-recipient"
          value={recipientName}
          onChange={(event) => setRecipientName(event.target.value)}
          className={FIELD_CLASS}
        />
      </div>
      <div className="flex flex-col gap-1">
        <label htmlFor="deliver-notes" className="text-sm font-medium">
          Notes (optional)
        </label>
        <textarea
          id="deliver-notes"
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
          rows={2}
          className={FIELD_CLASS}
        />
      </div>
      <button
        type="submit"
        disabled={mutation.isPending}
        className={`w-fit ${ACTION_BUTTON_CLASS}`}
      >
        {mutation.isPending ? 'Delivering…' : 'Deliver'}
      </button>
      <ActionError error={mutation.error} />
    </form>
  )
}

export function ShipmentDetailPage() {
  const { id } = useParams<{ id: string }>()
  const user = useAuthStore((state) => state.user)
  const shipmentQuery = useShipment(id)
  const timelineQuery = useShipmentTimeline(id)

  if (!id) {
    return <Navigate to="/shipments" replace />
  }

  if (shipmentQuery.isLoading) {
    return <DetailSkeleton />
  }

  if (shipmentQuery.isError || !shipmentQuery.data) {
    return (
      <div className="flex flex-col gap-3">
        <p role="alert" className="text-sm text-[var(--color-danger)]">
          Couldn't load this shipment. It may not exist, or you may not have access to it.
        </p>
        <Link to="/shipments" className="w-fit text-sm text-[var(--color-accent)]">
          Back to Shipments
        </Link>
      </div>
    )
  }

  const shipment = shipmentQuery.data
  const isDispatcher = user?.role === 'Dispatcher' || user?.role === 'Admin'
  const isOwningDriver = user?.role === 'Driver' && shipment.assignedDriverId === user.id
  const showActions =
    isDispatcher &&
    (shipment.status === 'Draft' ||
      shipment.status === 'ReadyForDispatch' ||
      shipment.status === 'Assigned' ||
      shipment.status === 'DeliveryFailed' ||
      shipment.status === 'Rescheduled')
  const showDriverActions =
    isOwningDriver &&
    (shipment.status === 'Assigned' ||
      shipment.status === 'PickedUp' ||
      shipment.status === 'InTransit' ||
      shipment.status === 'OutForDelivery')

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="text-xl font-semibold">{shipment.trackingNumber}</h1>
          <p className="text-sm text-[var(--color-text-muted)]">Shipment details</p>
        </div>
        <ShipmentStatusBadge status={shipment.status} />
      </div>

      <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
        <DetailCard title="Recipient">
          <p className="text-sm">{shipment.recipientName}</p>
          <p className="text-sm text-[var(--color-text-muted)]">{shipment.recipientPhone}</p>
        </DetailCard>
        <DetailCard title="Assigned driver">
          <p className="text-sm">{shipment.assignedDriverId ?? 'Not yet assigned'}</p>
        </DetailCard>
        <DetailCard title="Origin">
          <AddressLines address={shipment.origin} />
        </DetailCard>
        <DetailCard title="Destination">
          <AddressLines address={shipment.destination} />
        </DetailCard>
      </div>

      {(showActions || showDriverActions) && (
        <div className="flex flex-col gap-3">
          <h2 className="text-sm font-semibold">Actions</h2>
          <div className="flex flex-wrap gap-3">
            {isDispatcher && shipment.status === 'Draft' && (
              <ReadyForDispatchAction shipment={shipment} />
            )}
            {isDispatcher && shipment.status === 'ReadyForDispatch' && (
              <AssignAction shipment={shipment} />
            )}
            {isDispatcher && shipment.status === 'DeliveryFailed' && (
              <RescheduleAction shipment={shipment} />
            )}
            {isDispatcher &&
              (shipment.status === 'DeliveryFailed' || shipment.status === 'Rescheduled') && (
                <ReturnAction shipment={shipment} />
              )}
            {isDispatcher &&
              (shipment.status === 'Draft' ||
                shipment.status === 'ReadyForDispatch' ||
                shipment.status === 'Assigned') && <CancelAction shipment={shipment} />}

            {isOwningDriver && shipment.status === 'Assigned' && (
              <PickupAction shipment={shipment} />
            )}
            {isOwningDriver && shipment.status === 'PickedUp' && (
              <InTransitAction shipment={shipment} />
            )}
            {isOwningDriver && shipment.status === 'InTransit' && (
              <OutForDeliveryAction shipment={shipment} />
            )}
            {isOwningDriver && shipment.status === 'OutForDelivery' && (
              <>
                <DeliverAction shipment={shipment} />
                <FailAction shipment={shipment} />
              </>
            )}
          </div>
        </div>
      )}

      <div className="flex flex-col gap-3">
        <h2 className="text-sm font-semibold">Timeline</h2>
        <ShipmentTimelineList query={timelineQuery} />
      </div>
    </div>
  )
}

import { Link } from 'react-router-dom'
import { describeEvent } from './eventDescriptions'
import { useDispatchEvents, type ConnectionStatus } from './useDispatchEvents'

const STATUS_LABEL: Record<ConnectionStatus, string> = {
  connecting: 'Connecting…',
  connected: 'Live',
  reconnecting: 'Reconnecting…',
  disconnected: 'Disconnected',
}

const STATUS_DOT_CLASS: Record<ConnectionStatus, string> = {
  connecting: 'bg-[var(--badge-warning-text)]',
  connected: 'bg-[var(--badge-success-text)]',
  reconnecting: 'bg-[var(--badge-warning-text)]',
  disconnected: 'bg-[var(--badge-danger-text)]',
}

function formatTime(iso: string): string {
  return new Date(iso).toLocaleTimeString()
}

export function DispatchBoardPage() {
  const { events, status } = useDispatchEvents()
  const latestEvent = events[0]

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="text-xl font-semibold">Dispatch Board</h1>
        <div
          role="status"
          aria-live="polite"
          className="flex items-center gap-2 text-sm text-[var(--color-text-muted)]"
        >
          <span aria-hidden className={`h-2 w-2 rounded-full ${STATUS_DOT_CLASS[status]}`} />
          {STATUS_LABEL[status]}
        </div>
        {/* Visually hidden: announces each new event without re-reading the whole list. */}
        <span className="sr-only" role="status" aria-live="polite">
          {latestEvent ? describeEvent(latestEvent).label : ''}
        </span>
      </div>

      <p className="text-sm text-[var(--color-text-muted)]">
        Live feed of shipment activity as it happens — assignments, pickups, deliveries, and
        everything in between. This is a real-time supplement to the{' '}
        <Link to="/shipments" className="text-[var(--color-accent)]">
          Shipments list
        </Link>
        , not a replacement for it: reload the list for the authoritative current state.
      </p>

      {events.length === 0 && (
        <div className="rounded-lg border border-dashed border-[var(--color-border)] p-8 text-center">
          <p className="text-sm text-[var(--color-text-muted)]">
            {status === 'connected'
              ? 'No activity yet. New shipment events will appear here as they happen.'
              : 'Waiting to connect…'}
          </p>
        </div>
      )}

      {events.length > 0 && (
        <ul className="flex flex-col gap-2">
          {events.map((event) => {
            const { label, subject } = describeEvent(event)
            const shipmentId =
              typeof event.data.shipmentId === 'string' ? event.data.shipmentId : undefined

            const row = (
              <div className="flex items-center justify-between gap-3 rounded-lg border border-[var(--color-border)] px-4 py-3 hover:bg-[var(--color-bg-subtle)]">
                <div className="flex min-w-0 flex-col gap-0.5">
                  <span className="text-sm font-medium">{label}</span>
                  <span className="truncate text-xs text-[var(--color-text-muted)]">{subject}</span>
                </div>
                <span className="shrink-0 text-xs text-[var(--color-text-muted)]">
                  {formatTime(event.occurredAt)}
                </span>
              </div>
            )

            return (
              <li key={event.receivedId}>
                {shipmentId ? <Link to={`/shipments/${shipmentId}`}>{row}</Link> : row}
              </li>
            )
          })}
        </ul>
      )}
    </div>
  )
}

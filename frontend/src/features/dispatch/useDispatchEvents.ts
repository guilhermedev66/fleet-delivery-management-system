import { useEffect, useRef, useState } from 'react'
import {
  createDispatchHubConnection,
  type DispatchBoardEvent,
} from '../../lib/realtime/dispatchHub'

const MAX_EVENTS = 50

export interface DispatchFeedEvent extends DispatchBoardEvent {
  /** Client-generated — the server doesn't send one, and a list key needs something stable per received message. */
  receivedId: string
}

export type ConnectionStatus = 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

/**
 * Owns the dispatch hub's connection lifecycle: connects on mount,
 * disconnects on unmount (no leaked connections across navigations), and
 * keeps the most recent events newest-first, capped at MAX_EVENTS so a busy
 * board doesn't grow the list forever.
 */
export function useDispatchEvents() {
  const [events, setEvents] = useState<DispatchFeedEvent[]>([])
  const [status, setStatus] = useState<ConnectionStatus>('connecting')
  const nextIdRef = useRef(0)

  useEffect(() => {
    const connection = createDispatchHubConnection()

    connection.on('shipmentEvent', (event: DispatchBoardEvent) => {
      nextIdRef.current += 1
      const receivedId = String(nextIdRef.current)

      setEvents((current) => [{ ...event, receivedId }, ...current].slice(0, MAX_EVENTS))
    })

    connection.onreconnecting(() => setStatus('reconnecting'))
    connection.onreconnected(() => setStatus('connected'))
    connection.onclose(() => setStatus('disconnected'))

    connection
      .start()
      .then(() => setStatus('connected'))
      .catch(() => setStatus('disconnected'))

    return () => {
      // Fire-and-forget: React's cleanup can't await, and a connection
      // that's already closing/closed handles a redundant stop() safely.
      void connection.stop()
    }
  }, [])

  return { events, status }
}

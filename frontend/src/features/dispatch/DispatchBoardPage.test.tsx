import { act, render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { DispatchBoardPage } from './DispatchBoardPage'

const handlers: Record<string, (...args: unknown[]) => void> = {}

const fakeConnection = {
  on: vi.fn((event: string, handler: (...args: unknown[]) => void) => {
    handlers[event] = handler
  }),
  onreconnecting: vi.fn(),
  onreconnected: vi.fn(),
  onclose: vi.fn(),
  start: vi.fn().mockResolvedValue(undefined),
  stop: vi.fn().mockResolvedValue(undefined),
}

vi.mock('../../lib/realtime/dispatchHub', () => ({
  createDispatchHubConnection: () => fakeConnection,
}))

function renderPage() {
  return render(
    <MemoryRouter>
      <DispatchBoardPage />
    </MemoryRouter>,
  )
}

describe('DispatchBoardPage', () => {
  it('shows the empty state, then renders a real-time event as it arrives', async () => {
    renderPage()

    // Connection starts async — let the mocked start() promise resolve.
    await act(async () => {
      await Promise.resolve()
    })

    expect(fakeConnection.start).toHaveBeenCalledTimes(1)
    expect(screen.getByText(/no activity yet/i)).toBeInTheDocument()

    act(() => {
      handlers.shipmentEvent({
        type: 'ShipmentCreated',
        occurredAt: new Date().toISOString(),
        data: { shipmentId: 'shipment-1', trackingNumber: 'TRK-001' },
      })
    })

    expect(screen.getByText('Shipment created')).toBeInTheDocument()
    expect(screen.getByText('TRK-001')).toBeInTheDocument()
    expect(screen.queryByText(/no activity yet/i)).not.toBeInTheDocument()
  })
})

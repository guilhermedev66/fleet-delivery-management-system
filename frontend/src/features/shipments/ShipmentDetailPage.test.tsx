import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import * as shipmentsApi from '../../lib/api/shipments'
import * as vehiclesApi from '../../lib/api/vehicles'
import { useAuthStore } from '../auth/authStore'
import { ShipmentDetailPage } from './ShipmentDetailPage'

const SHIPMENT: shipmentsApi.ShipmentResponse = {
  id: 'shipment-1',
  trackingNumber: 'TRK-001',
  status: 'ReadyForDispatch',
  recipientName: 'Jane Doe',
  recipientPhone: '555-1234',
  origin: {
    street: '1 Main St',
    city: 'Springfield',
    state: 'IL',
    postalCode: '62701',
    country: 'US',
  },
  destination: {
    street: '2 Main St',
    city: 'Springfield',
    state: 'IL',
    postalCode: '62701',
    country: 'US',
  },
  assignedDriverId: null,
  assignedVehicleId: null,
  createdByUserId: 'dispatcher-1',
  createdAt: new Date().toISOString(),
  version: 'v1',
}

function renderDetailPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  })

  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={['/shipments/shipment-1']}>
        <Routes>
          <Route path="/shipments/:id" element={<ShipmentDetailPage />} />
        </Routes>
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

describe('ShipmentDetailPage assign action', () => {
  it('disables an unavailable driver in the select and explains why', async () => {
    useAuthStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      accessTokenExpiresAt: null,
      user: {
        id: 'dispatcher-1',
        email: 'dispatcher@example.com',
        fullName: 'Dana',
        role: 'Dispatcher',
      },
    })

    vi.spyOn(shipmentsApi, 'getShipment').mockResolvedValue(SHIPMENT)
    vi.spyOn(shipmentsApi, 'getShipmentTimeline').mockResolvedValue({ events: [] })
    vi.spyOn(shipmentsApi, 'listDrivers').mockResolvedValue([
      { id: 'driver-1', fullName: 'Alex Driver', email: 'alex@example.com', isAvailable: true },
      { id: 'driver-2', fullName: 'Sam Busy', email: 'sam@example.com', isAvailable: false },
    ])
    vi.spyOn(vehiclesApi, 'listVehicles').mockResolvedValue({
      items: [
        {
          id: 'vehicle-1',
          plateNumber: 'ABC-123',
          type: 'Van',
          capacityKg: 500,
          status: 'Active',
          createdAt: new Date().toISOString(),
        },
      ],
    })

    renderDetailPage()

    const driverSelect = await screen.findByLabelText('Driver')
    const busyOption = await within(driverSelect).findByText(/Sam Busy/)
    expect(busyOption).toHaveTextContent('currently on a delivery')
    expect((busyOption as HTMLOptionElement).disabled).toBe(true)

    const availableOption = within(driverSelect).getByText(/Alex Driver/)
    expect((availableOption as HTMLOptionElement).disabled).toBe(false)
  })
})

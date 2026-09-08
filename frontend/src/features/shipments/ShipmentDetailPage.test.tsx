import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../lib/api/client'
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
  hasProofOfDelivery: false,
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

describe('ShipmentDetailPage rescheduled shipment', () => {
  it('lets a dispatcher put a rescheduled shipment back up for dispatch', async () => {
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

    vi.spyOn(shipmentsApi, 'getShipment').mockResolvedValue({
      ...SHIPMENT,
      status: 'Rescheduled',
    })
    vi.spyOn(shipmentsApi, 'getShipmentTimeline').mockResolvedValue({ events: [] })

    renderDetailPage()

    expect(await screen.findByRole('button', { name: /ready for dispatch/i })).toBeInTheDocument()
  })
})

describe('ShipmentDetailPage proof of delivery', () => {
  function signInAsOwningDriver() {
    useAuthStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      accessTokenExpiresAt: null,
      user: {
        id: 'driver-1',
        email: 'driver@example.com',
        fullName: 'Alex Driver',
        role: 'Driver',
      },
    })
  }

  it('lets the assigned driver upload a photo once delivered', async () => {
    signInAsOwningDriver()
    vi.spyOn(shipmentsApi, 'getShipment').mockResolvedValue({
      ...SHIPMENT,
      status: 'Delivered',
      assignedDriverId: 'driver-1',
    })
    vi.spyOn(shipmentsApi, 'getShipmentTimeline').mockResolvedValue({ events: [] })
    const attachSpy = vi
      .spyOn(shipmentsApi, 'attachProofOfDelivery')
      .mockResolvedValue({ ...SHIPMENT, status: 'Delivered', hasProofOfDelivery: true })

    const user = userEvent.setup()
    renderDetailPage()

    const fileInput = await screen.findByLabelText(/proof of delivery photo/i)
    const file = new File(['fake-bytes'], 'proof.jpg', { type: 'image/jpeg' })
    await user.upload(fileInput, file)

    const uploadButton = screen.getByRole('button', { name: /upload photo/i })
    expect(uploadButton).not.toBeDisabled()
    await user.click(uploadButton)

    expect(attachSpy).toHaveBeenCalledWith('shipment-1', file)
  })

  it('shows a specific message when the uploaded file is rejected', async () => {
    signInAsOwningDriver()
    vi.spyOn(shipmentsApi, 'getShipment').mockResolvedValue({
      ...SHIPMENT,
      status: 'Delivered',
      assignedDriverId: 'driver-1',
    })
    vi.spyOn(shipmentsApi, 'getShipmentTimeline').mockResolvedValue({ events: [] })
    vi.spyOn(shipmentsApi, 'attachProofOfDelivery').mockRejectedValue(
      new ApiError(
        400,
        'Request failed with status 400',
        'https://fleetdelivery.local/errors/shipment-invalid-proof-of-delivery-content',
      ),
    )

    const user = userEvent.setup()
    renderDetailPage()

    const fileInput = await screen.findByLabelText(/proof of delivery photo/i)
    await user.upload(
      fileInput,
      new File(['not-really-a-jpeg'], 'proof.jpg', { type: 'image/jpeg' }),
    )
    await user.click(screen.getByRole('button', { name: /upload photo/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      /doesn't look like a valid jpeg or png/i,
    )
  })

  it("renders the photo once it's attached", async () => {
    signInAsOwningDriver()
    vi.spyOn(shipmentsApi, 'getShipment').mockResolvedValue({
      ...SHIPMENT,
      status: 'Delivered',
      assignedDriverId: 'driver-1',
      hasProofOfDelivery: true,
    })
    vi.spyOn(shipmentsApi, 'getShipmentTimeline').mockResolvedValue({ events: [] })
    vi.spyOn(shipmentsApi, 'getProofOfDeliveryPhoto').mockResolvedValue(
      new Blob(['fake-bytes'], { type: 'image/jpeg' }),
    )
    const originalCreateObjectURL = URL.createObjectURL
    const originalRevokeObjectURL = URL.revokeObjectURL
    URL.createObjectURL = vi.fn(() => 'blob:mock-url')
    URL.revokeObjectURL = vi.fn()

    try {
      renderDetailPage()

      const photo = await screen.findByRole('img', { name: /proof of delivery/i })
      expect(photo).toHaveAttribute('src', 'blob:mock-url')
    } finally {
      URL.createObjectURL = originalCreateObjectURL
      URL.revokeObjectURL = originalRevokeObjectURL
    }
  })
})

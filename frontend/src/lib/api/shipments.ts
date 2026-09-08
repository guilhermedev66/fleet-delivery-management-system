import { apiClient } from './client'

export interface Address {
  street: string
  city: string
  state: string
  postalCode: string
  country: string
}

export type ShipmentStatus =
  | 'Draft'
  | 'ReadyForDispatch'
  | 'Assigned'
  | 'PickedUp'
  | 'InTransit'
  | 'OutForDelivery'
  | 'Delivered'
  | 'DeliveryFailed'
  | 'Rescheduled'
  | 'Returned'
  | 'Cancelled'

export interface ShipmentResponse {
  id: string
  trackingNumber: string
  status: ShipmentStatus
  recipientName: string
  recipientPhone: string
  origin: Address
  destination: Address
  assignedDriverId: string | null
  assignedVehicleId: string | null
  createdByUserId: string
  createdAt: string
  version: string
}

export interface ShipmentListResponse {
  items: ShipmentResponse[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ShipmentTimelineEvent {
  type: string
  occurredAt: string
  actorUserId: string | null
  notes: string | null
}

export interface ShipmentTimelineResponse {
  events: ShipmentTimelineEvent[]
}

export interface ShipmentFilters {
  status?: ShipmentStatus
  page?: number
  pageSize?: number
}

export interface CreateShipmentRequest {
  recipientName: string
  recipientPhone: string
  origin: Address
  destination: Address
}

export interface DeliverShipmentRequest {
  expectedVersion: string
  recipientName?: string
  notes?: string
}

export interface AvailableDriver {
  id: string
  fullName: string
  email: string
  isAvailable: boolean
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export function listShipments(filters: ShipmentFilters = {}): Promise<ShipmentListResponse> {
  const query = buildQuery({
    status: filters.status,
    page: filters.page,
    pageSize: filters.pageSize,
  })
  return apiClient.get<ShipmentListResponse>(`/api/shipments${query}`)
}

export function getShipment(id: string): Promise<ShipmentResponse> {
  return apiClient.get<ShipmentResponse>(`/api/shipments/${id}`)
}

export function getShipmentTimeline(id: string): Promise<ShipmentTimelineResponse> {
  return apiClient.get<ShipmentTimelineResponse>(`/api/shipments/${id}/timeline`)
}

export function createShipment(body: CreateShipmentRequest): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>('/api/shipments', body)
}

export function readyForDispatch(id: string, expectedVersion: string): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/ready-for-dispatch`, {
    expectedVersion,
  })
}

export function listDrivers(): Promise<AvailableDriver[]> {
  return apiClient.get<AvailableDriver[]>('/api/shipments/drivers')
}

export function assignShipment(
  id: string,
  driverId: string,
  vehicleId: string,
  expectedVersion: string,
): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/assign`, {
    driverId,
    vehicleId,
    expectedVersion,
  })
}

export function pickupShipment(id: string, expectedVersion: string): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/pickup`, { expectedVersion })
}

export function inTransitShipment(id: string, expectedVersion: string): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/in-transit`, { expectedVersion })
}

export function outForDeliveryShipment(
  id: string,
  expectedVersion: string,
): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/out-for-delivery`, {
    expectedVersion,
  })
}

export function deliverShipment(
  id: string,
  body: DeliverShipmentRequest,
): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/deliver`, body)
}

export function failShipment(
  id: string,
  expectedVersion: string,
  reason: string,
): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/fail`, { expectedVersion, reason })
}

export function rescheduleShipment(id: string, expectedVersion: string): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/reschedule`, { expectedVersion })
}

export function returnShipment(
  id: string,
  expectedVersion: string,
  reason: string,
): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/return`, {
    expectedVersion,
    reason,
  })
}

export function cancelShipment(
  id: string,
  expectedVersion: string,
  reason: string,
): Promise<ShipmentResponse> {
  return apiClient.post<ShipmentResponse>(`/api/shipments/${id}/cancel`, {
    expectedVersion,
    reason,
  })
}

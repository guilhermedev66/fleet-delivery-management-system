import { apiClient } from './client'

export type VehicleType = 'Van' | 'Truck' | 'Motorcycle' | 'Car'
export type VehicleStatus = 'Active' | 'Maintenance' | 'Retired'

export interface VehicleResponse {
  id: string
  plateNumber: string
  type: VehicleType
  capacityKg: number
  status: VehicleStatus
  createdAt: string
}

export interface VehicleListResponse {
  items: VehicleResponse[]
}

export interface VehicleFilters {
  status?: VehicleStatus
}

export interface RegisterVehicleRequest {
  plateNumber: string
  type: VehicleType
  capacityKg: number
}

function buildQuery(params: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined) search.set(key, String(value))
  }
  const query = search.toString()
  return query ? `?${query}` : ''
}

export function listVehicles(filters: VehicleFilters = {}): Promise<VehicleListResponse> {
  const query = buildQuery({ status: filters.status })
  return apiClient.get<VehicleListResponse>(`/api/vehicles${query}`)
}

export function getVehicle(id: string): Promise<VehicleResponse> {
  return apiClient.get<VehicleResponse>(`/api/vehicles/${id}`)
}

export function registerVehicle(body: RegisterVehicleRequest): Promise<VehicleResponse> {
  return apiClient.post<VehicleResponse>('/api/vehicles', body)
}

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  getVehicle,
  listVehicles,
  registerVehicle,
  type VehicleFilters,
} from '../../lib/api/vehicles'

export const vehicleKeys = {
  all: ['vehicles'] as const,
  lists: () => [...vehicleKeys.all, 'list'] as const,
  list: (filters: VehicleFilters) => [...vehicleKeys.lists(), filters] as const,
  details: () => [...vehicleKeys.all, 'detail'] as const,
  detail: (id: string) => [...vehicleKeys.details(), id] as const,
}

export function useVehicles(filters: VehicleFilters = {}) {
  return useQuery({
    queryKey: vehicleKeys.list(filters),
    queryFn: () => listVehicles(filters),
  })
}

export function useVehicle(id: string | undefined) {
  return useQuery({
    queryKey: vehicleKeys.detail(id ?? ''),
    queryFn: () => getVehicle(id as string),
    enabled: Boolean(id),
  })
}

export function useRegisterVehicle() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: registerVehicle,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: vehicleKeys.lists() })
    },
  })
}

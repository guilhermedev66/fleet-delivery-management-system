import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  assignShipment,
  cancelShipment,
  createShipment,
  deliverShipment,
  failShipment,
  getShipment,
  getShipmentTimeline,
  inTransitShipment,
  listDrivers,
  listShipments,
  outForDeliveryShipment,
  pickupShipment,
  readyForDispatch,
  rescheduleShipment,
  returnShipment,
  type ShipmentFilters,
} from '../../lib/api/shipments'

export const shipmentKeys = {
  all: ['shipments'] as const,
  lists: () => [...shipmentKeys.all, 'list'] as const,
  list: (filters: ShipmentFilters) => [...shipmentKeys.lists(), filters] as const,
  details: () => [...shipmentKeys.all, 'detail'] as const,
  detail: (id: string) => [...shipmentKeys.details(), id] as const,
  timeline: (id: string) => [...shipmentKeys.detail(id), 'timeline'] as const,
}

export function useShipments(filters: ShipmentFilters) {
  return useQuery({
    queryKey: shipmentKeys.list(filters),
    queryFn: () => listShipments(filters),
    placeholderData: keepPreviousData,
  })
}

export function useShipment(id: string | undefined) {
  return useQuery({
    queryKey: shipmentKeys.detail(id ?? ''),
    queryFn: () => getShipment(id as string),
    enabled: Boolean(id),
  })
}

export function useShipmentTimeline(id: string | undefined) {
  return useQuery({
    queryKey: shipmentKeys.timeline(id ?? ''),
    queryFn: () => getShipmentTimeline(id as string),
    enabled: Boolean(id),
  })
}

export function useCreateShipment() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: createShipment,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: shipmentKeys.lists() })
    },
  })
}

/**
 * Every state-transition mutation below refetches the shipment (detail +
 * timeline + lists) in `onSettled`, on both success and failure. A 409 means
 * either a stale `expectedVersion` or an invalid transition someone else
 * caused in the meantime — in both cases the fix is the same: pull the real
 * current state rather than leave a dead error state on screen. Refetching
 * on other errors too (e.g. a network blip) is harmless.
 */
function useInvalidateShipment(id: string) {
  const queryClient = useQueryClient()
  return () => {
    queryClient.invalidateQueries({ queryKey: shipmentKeys.detail(id) })
    queryClient.invalidateQueries({ queryKey: shipmentKeys.timeline(id) })
    queryClient.invalidateQueries({ queryKey: shipmentKeys.lists() })
  }
}

export function useReadyForDispatch(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: (expectedVersion: string) => readyForDispatch(id, expectedVersion),
    onSettled: invalidate,
  })
}

export function useDrivers() {
  return useQuery({
    queryKey: ['shipments', 'drivers'] as const,
    queryFn: listDrivers,
  })
}

export function useAssignShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: ({
      driverId,
      vehicleId,
      expectedVersion,
    }: {
      driverId: string
      vehicleId: string
      expectedVersion: string
    }) => assignShipment(id, driverId, vehicleId, expectedVersion),
    onSettled: invalidate,
  })
}

export function usePickupShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: (expectedVersion: string) => pickupShipment(id, expectedVersion),
    onSettled: invalidate,
  })
}

export function useInTransitShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: (expectedVersion: string) => inTransitShipment(id, expectedVersion),
    onSettled: invalidate,
  })
}

export function useOutForDeliveryShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: (expectedVersion: string) => outForDeliveryShipment(id, expectedVersion),
    onSettled: invalidate,
  })
}

export function useDeliverShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: (body: { expectedVersion: string; recipientName?: string; notes?: string }) =>
      deliverShipment(id, body),
    onSettled: invalidate,
  })
}

export function useFailShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: ({ expectedVersion, reason }: { expectedVersion: string; reason: string }) =>
      failShipment(id, expectedVersion, reason),
    onSettled: invalidate,
  })
}

export function useRescheduleShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: (expectedVersion: string) => rescheduleShipment(id, expectedVersion),
    onSettled: invalidate,
  })
}

export function useReturnShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: ({ expectedVersion, reason }: { expectedVersion: string; reason: string }) =>
      returnShipment(id, expectedVersion, reason),
    onSettled: invalidate,
  })
}

export function useCancelShipment(id: string) {
  const invalidate = useInvalidateShipment(id)
  return useMutation({
    mutationFn: ({ expectedVersion, reason }: { expectedVersion: string; reason: string }) =>
      cancelShipment(id, expectedVersion, reason),
    onSettled: invalidate,
  })
}

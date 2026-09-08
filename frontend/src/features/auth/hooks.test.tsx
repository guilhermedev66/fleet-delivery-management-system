import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { renderHook, waitFor } from '@testing-library/react'
import type { ReactNode } from 'react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import * as authApi from '../../lib/api/auth'
import { useAuthStore } from './authStore'
import { useLogout } from './hooks'

describe('useLogout', () => {
  it('clears the React Query cache so a different user never sees stale data', async () => {
    vi.spyOn(authApi, 'logout').mockResolvedValue(undefined)
    useAuthStore.setState({
      status: 'authenticated',
      accessToken: 'token',
      accessTokenExpiresAt: null,
      user: { id: 'user-1', email: 'a@example.com', fullName: 'A', role: 'Dispatcher' },
    })

    const queryClient = new QueryClient({
      defaultOptions: { mutations: { retry: false } },
    })
    queryClient.setQueryData(['shipments'], [{ id: 'shipment-1' }])

    function wrapper({ children }: { children: ReactNode }) {
      return (
        <QueryClientProvider client={queryClient}>
          <MemoryRouter>{children}</MemoryRouter>
        </QueryClientProvider>
      )
    }

    const { result } = renderHook(() => useLogout(), { wrapper })
    result.current.mutate()

    await waitFor(() => expect(result.current.isSuccess || result.current.isError).toBe(true))

    expect(queryClient.getQueryData(['shipments'])).toBeUndefined()
    expect(useAuthStore.getState().status).toBe('unauthenticated')
  })
})

import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { getMe, login as loginRequest, logout as logoutRequest, refresh } from '../../lib/api/auth'
import { useAuthStore, type AuthStatus } from './authStore'

/**
 * Attempts a silent, cookie-based session restore on mount: POST /refresh
 * (uses the httpOnly refresh cookie) then GET /me for the user profile.
 * Any failure (no cookie, expired session, network error) just means
 * "not logged in" — resolves to `unauthenticated`, never throws.
 */
export function useInitAuth(): AuthStatus {
  const status = useAuthStore((state) => state.status)
  const setToken = useAuthStore((state) => state.setToken)
  const setSession = useAuthStore((state) => state.setSession)
  const clear = useAuthStore((state) => state.clear)

  useEffect(() => {
    let cancelled = false

    async function bootstrap() {
      try {
        const { accessToken, accessTokenExpiresAt } = await refresh()
        if (cancelled) return
        // Set the token first so the /me call below can use it.
        setToken(accessToken, accessTokenExpiresAt)
        const user = await getMe()
        if (cancelled) return
        setSession({ accessToken, accessTokenExpiresAt, user })
      } catch {
        if (!cancelled) clear()
      }
    }

    bootstrap()

    return () => {
      cancelled = true
    }
  }, [setToken, setSession, clear])

  return status
}

export function useLogin() {
  const setSession = useAuthStore((state) => state.setSession)

  return useMutation({
    mutationFn: ({ email, password }: { email: string; password: string }) =>
      loginRequest(email, password),
    onSuccess: (data) => {
      setSession(data)
    },
  })
}

export function useLogout() {
  const clear = useAuthStore((state) => state.clear)
  const navigate = useNavigate()
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: logoutRequest,
    onSettled: () => {
      // Clear client-side state and redirect even if the API call itself
      // failed (e.g. network hiccup) — the user's intent is to be logged out.
      // Also drop every cached query so a different user signing in on the
      // same tab never flashes the previous user's data before refetching.
      clear()
      queryClient.clear()
      navigate('/login', { replace: true })
    },
  })
}

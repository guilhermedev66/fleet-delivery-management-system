import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '../features/auth/authStore'
import { useInitAuth } from '../features/auth/hooks'

/**
 * Gate for every authenticated route. Kicks off the silent-refresh session
 * check on first mount, shows a lightweight loading state while that's in
 * flight (no flash of the login page), then either redirects to /login or
 * renders the nested authenticated routes.
 */
export function ProtectedRoute() {
  useInitAuth()
  const status = useAuthStore((state) => state.status)

  if (status === 'idle') {
    return (
      <div className="flex min-h-svh items-center justify-center">
        <p className="text-sm text-[var(--color-text-muted)]">Loading…</p>
      </div>
    )
  }

  if (status === 'unauthenticated') {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}

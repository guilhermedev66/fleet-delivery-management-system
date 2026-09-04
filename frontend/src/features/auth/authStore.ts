import { create } from 'zustand'
import type { AuthUser } from '../../lib/api/auth'

export type AuthStatus = 'idle' | 'authenticated' | 'unauthenticated'

interface AuthState {
  /**
   * In-memory only, by design — never persisted to localStorage/sessionStorage.
   * The httpOnly refresh cookie is what survives a reload; on load we
   * exchange it for a fresh access token via a silent refresh (see
   * features/auth/hooks.ts `useInitAuth`).
   */
  accessToken: string | null
  accessTokenExpiresAt: string | null
  user: AuthUser | null
  status: AuthStatus
  setToken: (accessToken: string, accessTokenExpiresAt: string) => void
  setSession: (session: {
    accessToken: string
    accessTokenExpiresAt: string
    user: AuthUser
  }) => void
  clear: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  accessToken: null,
  accessTokenExpiresAt: null,
  user: null,
  status: 'idle',

  setToken: (accessToken, accessTokenExpiresAt) => set({ accessToken, accessTokenExpiresAt }),

  setSession: ({ accessToken, accessTokenExpiresAt, user }) =>
    set({ accessToken, accessTokenExpiresAt, user, status: 'authenticated' }),

  clear: () =>
    set({ accessToken: null, accessTokenExpiresAt: null, user: null, status: 'unauthenticated' }),
}))

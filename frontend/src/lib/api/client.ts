import { useAuthStore } from '../../features/auth/authStore'

// Falls back to the docker-compose API port so `npm run dev` works out of
// the box without requiring a manual `cp .env.example .env` first.
const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

/**
 * Thrown for any non-2xx response. Callers that need to distinguish
 * "expected" failures (e.g. bad login credentials) from unexpected ones
 * should check `status`, not try to parse `message` for specifics — the
 * backend's ProblemDetails-ish error bodies aren't guaranteed to carry a
 * stable shape yet.
 */
export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

async function request<T>(path: string, method: HttpMethod, body?: unknown): Promise<T> {
  const accessToken = useAuthStore.getState().accessToken

  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`

  let response: Response
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      credentials: 'include',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    })
  } catch {
    throw new ApiError(0, 'Unable to reach the server. Check your connection and try again.')
  }

  if (!response.ok) {
    // A 401 anywhere means the session is gone (expired/revoked refresh
    // token, or an access token that no longer validates). M1 scope-cut:
    // force a client-side logout instead of a full refresh-and-retry
    // interceptor chain — see frontend AGENT report for details.
    if (response.status === 401) {
      useAuthStore.getState().clear()
    }
    throw new ApiError(response.status, `Request to ${path} failed with status ${response.status}`)
  }

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path, 'GET'),
  post: <T>(path: string, body?: unknown) => request<T>(path, 'POST', body),
}

import { useAuthStore } from '../../features/auth/authStore'

// Falls back to the docker-compose API port so `npm run dev` works out of
// the box without requiring a manual `cp .env.example .env` first.
const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

/**
 * Thrown for any non-2xx response. Callers that need to distinguish
 * "expected" failures (e.g. bad login credentials) from unexpected ones
 * should check `status`, not try to parse `message` for specifics — the
 * backend's ProblemDetails-ish error bodies aren't guaranteed to carry a
 * stable shape yet. Where an endpoint's failure responses DO define a fixed
 * `type` URI per error code (see e.g. ShipmentEndpoints.MapFailure),
 * `problemType` carries it through for callers that need to tell apart two
 * different errors sharing the same status code.
 */
export class ApiError extends Error {
  readonly status: number
  readonly problemType?: string

  constructor(status: number, message: string, problemType?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.problemType = problemType
  }
}

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'

async function rawRequest(
  path: string,
  method: HttpMethod,
  init: { body?: BodyInit; headers?: Record<string, string> } = {},
): Promise<Response> {
  const accessToken = useAuthStore.getState().accessToken

  const headers: Record<string, string> = { ...init.headers }
  if (accessToken) headers.Authorization = `Bearer ${accessToken}`

  let response: Response
  try {
    response = await fetch(`${API_URL}${path}`, {
      method,
      headers,
      credentials: 'include',
      body: init.body,
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
    let problemType: string | undefined
    try {
      const problem = await response.clone().json()
      if (typeof problem?.type === 'string') problemType = problem.type
    } catch {
      // No JSON body (e.g. a bare 404) — nothing to extract.
    }
    throw new ApiError(
      response.status,
      `Request to ${path} failed with status ${response.status}`,
      problemType,
    )
  }

  return response
}

async function request<T>(path: string, method: HttpMethod, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  if (body !== undefined) headers['Content-Type'] = 'application/json'

  const response = await rawRequest(path, method, {
    body: body !== undefined ? JSON.stringify(body) : undefined,
    headers,
  })

  if (response.status === 204) {
    return undefined as T
  }

  return (await response.json()) as T
}

export const apiClient = {
  get: <T>(path: string) => request<T>(path, 'GET'),
  post: <T>(path: string, body?: unknown) => request<T>(path, 'POST', body),
  // Multipart upload — the browser sets the Content-Type boundary itself,
  // so no header is passed (matching the JSON path's Content-Type-only-
  // when-there's-a-body behavior).
  postForm: async <T>(path: string, formData: FormData): Promise<T> => {
    const response = await rawRequest(path, 'POST', { body: formData })
    return (await response.json()) as T
  },
  // For endpoints that return raw bytes (e.g. an image) instead of JSON.
  // Response.blob() carries the response's Content-Type as Blob.type.
  getBlob: async (path: string): Promise<Blob> => {
    const response = await rawRequest(path, 'GET')
    return response.blob()
  },
}

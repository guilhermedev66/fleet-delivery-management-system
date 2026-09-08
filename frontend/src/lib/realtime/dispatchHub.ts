import {
  HubConnectionBuilder,
  HttpTransportType,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { useAuthStore } from '../../features/auth/authStore'

// Same fallback as lib/api/client.ts, so a bare `npm run dev` (no .env)
// still works — the hub lives on the same API host, just a different path.
const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080'

export interface DispatchBoardEvent {
  type: string
  occurredAt: string
  data: Record<string, unknown>
}

/**
 * Browsers can't set an Authorization header on a WebSocket handshake, so
 * the token travels as an `access_token` query param instead — the backend
 * (Program.cs's JwtBearerEvents.OnMessageReceived) only honors that param
 * on `/hubs/*`, never on the REST API, so this doesn't relax auth anywhere
 * else. `accessTokenFactory` re-reads the store on every (re)connect
 * attempt rather than capturing one value, so a token refreshed mid-session
 * is picked up automatically.
 */
export function createDispatchHubConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_URL}/hubs/dispatch`, {
      accessTokenFactory: () => useAuthStore.getState().accessToken ?? '',
      // WebSockets first, falling back automatically if a proxy/network
      // blocks them — the default negotiated behavior, stated explicitly
      // since LongPolling-only is what the backend integration tests use
      // (TestServer can't do real sockets) and that's a testing detail,
      // not something the real browser client should be limited to.
      transport: HttpTransportType.WebSockets | HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}

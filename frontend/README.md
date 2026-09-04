# Fleet & Delivery Management System — Frontend

React + TypeScript + Vite. Tailwind CSS for styling, React Query for server
state, React Router for routing, Zustand for light UI state. See
`../docs/ARCHITECTURE.md` for the full picture (typed API client against the
backend's OpenAPI contract, SignalR for live dispatch/tracking updates).

This is M0 scaffolding — the app currently renders a single placeholder
route. Feature UI (dispatcher dashboard, driver flow, etc.) lands per
milestone under `src/features/`.

## Structure

- `src/app` — routing/providers shell (`App.tsx`, `router.tsx`, `providers.tsx`)
- `src/features` — feature modules, one per bounded area (empty for now)
- `src/components` — shared/UI primitives (empty for now)
- `src/lib/api` — typed API client, generated against the backend's OpenAPI
  contract once it's published (empty for now)
- `src/lib/utils.ts` — small shared helpers

## Scripts

- `npm run dev` — start the Vite dev server
- `npm run build` — typecheck + production build
- `npm run lint` — ESLint (flat config, TypeScript-aware)
- `npm run typecheck` — `tsc -b`, no emit
- `npm run test` — Vitest (`npm run test -- --run` for a single CI run)
- `npm run format` / `npm run format:check` — Prettier

## Environment

Copy `.env.example` to `.env` and adjust as needed. `VITE_API_URL` points at
the backend API (`http://localhost:5080` when running via the root
`docker-compose.yml`); `VITE_SIGNALR_URL` points at the SignalR hub base.

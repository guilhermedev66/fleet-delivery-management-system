# FleetDelivery backend

.NET 10 modular monolith. See [../docs/ARCHITECTURE.md](../docs/ARCHITECTURE.md)
for the full design. This file covers what's needed to actually run the
backend locally: migrations, seeded credentials, and required config.

## Prerequisites

- .NET 10 SDK
- `dotnet-ef` global tool: `dotnet tool install --global dotnet-ef`
- PostgreSQL reachable (via `docker compose up postgres` from the repo root,
  or any Postgres instance — see connection string below)

## Running locally

```bash
cd backend
dotnet run --project src/FleetDelivery.Api
```

In the `Development` environment (the default via `launchSettings.json`),
`Program.cs` **automatically applies pending EF Core migrations and seeds a
dev-only Admin user on startup** — nothing else to run first.

Every other environment must apply migrations explicitly as part of
deployment (see below) — `Database.Migrate()` is only ever called when
`env.IsDevelopment()` is true.

## Dev-seeded Admin user

Seeded once, only in `ASPNETCORE_ENVIRONMENT=Development`, only if the
`identity.users` table is empty (checked via `IUserRepository.AnyAsync`).
Never runs in any other environment.

| | |
|---|---|
| Email | `admin@fleetdelivery.local` |
| Password | `Dev!Passw0rd123` |
| Role | `Admin` |

Also declared as constants in
[`IdentityDevSeeder`](src/Modules/Identity/FleetDelivery.Modules.Identity.Infrastructure/Seeding/IdentityDevSeeder.cs).

## Identity module — EF Core migrations

The Identity module owns the `identity` Postgres schema exclusively via
`IdentityDbContext`. To apply its migrations explicitly (any environment
other than Development, where it happens automatically):

```bash
cd backend
dotnet ef database update \
  --project src/Modules/Identity/FleetDelivery.Modules.Identity.Infrastructure/FleetDelivery.Modules.Identity.Infrastructure.csproj \
  --startup-project src/FleetDelivery.Api/FleetDelivery.Api.csproj \
  --context IdentityDbContext
```

To add a new migration after changing `User`/`RefreshToken`/their
`IEntityTypeConfiguration`s:

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/Identity/FleetDelivery.Modules.Identity.Infrastructure/FleetDelivery.Modules.Identity.Infrastructure.csproj \
  --startup-project src/FleetDelivery.Api/FleetDelivery.Api.csproj \
  --context IdentityDbContext \
  --output-dir Persistence/Migrations
```

(`--connection "<connection string>"` can be added to either command to
target a specific database without touching `appsettings.*.json` — useful
for a one-off check against a local Postgres.)

### Email case-insensitivity: normalized column, not `citext`

`User` uniqueness on email is enforced via a persisted `NormalizedEmail`
column (uppercase-invariant) with a plain unique index, rather than
Postgres's `citext` extension. `citext` would let a unique index on `email`
itself be case-insensitive, but it requires `CREATE EXTENSION citext` per
database — an extra, Postgres-specific deploy step. A normalized column
needs nothing beyond the migration already checked in. See
[`UserConfiguration`](src/Modules/Identity/FleetDelivery.Modules.Identity.Infrastructure/Persistence/Configurations/UserConfiguration.cs).

## Shipments module

Owns the `shipments` Postgres schema exclusively via `ShipmentsDbContext`.
Covers the M2 shipment/delivery lifecycle: `Draft -> ReadyForDispatch ->
Assigned -> PickedUp -> InTransit -> OutForDelivery -> Delivered`, with
`DeliveryFailed -> Rescheduled -> ReadyForDispatch`, `Returned`, and
`Cancelled` as the alternative exits (see the transition table and doc
comments on
[`Shipment`](src/Modules/Shipments/FleetDelivery.Modules.Shipments.Domain/Shipment.cs)
for the exact rules). Every transition appends a `TrackingEvent` — the audit
trail returned by `GET /api/shipments/{id}/timeline` is a side effect of the
state machine, not written separately.

### "A driver" is just an Identity `User` with `Role.Driver`

There is no separate Drivers module yet — that's a later milestone (license,
capacity, and other driver-profile data). For now, `Shipment.AssignedDriverId`
is a plain `Guid` referencing `identity.users.id`, with **no cross-schema
foreign key** (per the modular-monolith rule: cross-module references are
plain IDs, validated at the application layer, never DB-enforced).
`AssignCommand` validates a `driverId` by calling Identity's own Application
public contract (`GetCurrentUserQuery`, sent via MediatR's `ISender`) rather
than querying the `identity` schema directly — the in-process cross-module
call style from docs/ARCHITECTURE.md. `AssignCommand` validates `vehicleId`
the same way, against the Vehicles module's own Application public contract
(`GetVehicleByIdQuery`), rejecting anything that isn't an `Active` vehicle —
see the [Vehicles module](#vehicles-module) section below.
`GET /api/shipments/drivers` backs the dispatcher's driver picker: every
`Role.Driver` user, each flagged `isAvailable: false` if they're currently
assigned to a shipment that's been dispatched but not yet finished
(`Assigned`/`PickedUp`/`InTransit`/`OutForDelivery`) — kept in the list
rather than filtered out, so the UI can show *why* a driver can't be picked.
Driver-only endpoints (`pickup`, `in-transit`,
`out-for-delivery`, `deliver`, `fail`) authorize by comparing the JWT's `sub`
claim against `Shipment.AssignedDriverId`, both inside the domain method
(`Shipment.MarkPickedUp` etc. throw `ShipmentDriverMismatchException`) and
again at the Application-handler level before ever calling into the domain —
real IDOR protection, covered by
[`ShipmentEndpointsTests`](../tests/FleetDelivery.IntegrationTests/Shipments/ShipmentEndpointsTests.cs).
A shipment that exists but isn't assigned to the calling driver returns
**404**, not 403 — indistinguishable from a shipment that doesn't exist at
all, so the response never confirms the id's existence to a caller who
shouldn't see it.

### Endpoints (`/api/shipments`)

| Method & path | Roles | Notes |
|---|---|---|
| `POST /api/shipments` | Dispatcher, Admin | Creates in `Draft`, server-generates `trackingNumber` |
| `GET /api/shipments` | any | Paged, optional `status`/`driverId` filter; a Driver's own filter is always server-forced, ignoring any client-supplied `driverId` |
| `GET /api/shipments/{id}` | any | 404 (not 403) if a Driver requests one not assigned to them |
| `GET /api/shipments/{id}/timeline` | any | Same ownership rule as above |
| `GET /api/shipments/drivers` | Dispatcher, Admin | Every Driver, each with `isAvailable` — see above |
| `POST /api/shipments/{id}/ready-for-dispatch` | Dispatcher, Admin | Serves both `Draft -> ReadyForDispatch` and `Rescheduled -> ReadyForDispatch` — see the doc comment on `ReadyForDispatchCommand` |
| `POST /api/shipments/{id}/assign` | Dispatcher, Admin | Body: `{ driverId, vehicleId, expectedVersion }` |
| `POST /api/shipments/{id}/pickup` | Driver | Body: `{ expectedVersion }` |
| `POST /api/shipments/{id}/in-transit` | Driver | Body: `{ expectedVersion }` |
| `POST /api/shipments/{id}/out-for-delivery` | Driver | Body: `{ expectedVersion }` |
| `POST /api/shipments/{id}/deliver` | Driver | Body: `{ expectedVersion, recipientName?, notes? }` |
| `POST /api/shipments/{id}/fail` | Driver | Body: `{ expectedVersion, reason }` |
| `POST /api/shipments/{id}/reschedule` | Dispatcher, Admin | Body: `{ expectedVersion }` |
| `POST /api/shipments/{id}/return` | Dispatcher, Admin | Body: `{ expectedVersion, reason }` |
| `POST /api/shipments/{id}/cancel` | Dispatcher, Admin | Body: `{ expectedVersion, reason }`; only allowed before `PickedUp` |

Every mutating endpoint takes `expectedVersion` (the `version` field on the
last `ShipmentResponse` you read) and returns a 409 ProblemDetails if it's
stale — distinguishable from an invalid-transition 409 by its `type`
(`.../shipment-concurrency-conflict` vs. `.../shipment-invalid-transition`).
`Shipment.Version` is a hand-rolled `int` counter (not Postgres's `xmin`) —
see the doc comment on `Shipment` for why, and
[`ShipmentEndpointsTests`](../tests/FleetDelivery.IntegrationTests/Shipments/ShipmentEndpointsTests.cs)
for a real (not mocked) stale-version-returns-409 test.

### Outbox: written AND published (M4)

`ShipmentsDbContext.SaveChangesAsync` is overridden to walk every tracked
aggregate's pending domain events and write one `OutboxMessage` row per
event to its own `shipments.outbox_messages` table, in the same
`SaveChanges` call/transaction as the state change — see the doc comment on
`ShipmentsDbContext`. This module's Outbox table is deliberately its own
copy rather than shared with Identity's schema — Identity doesn't have
Outbox wiring yet either, and sharing one table across module schemas is a
bridge to cross once a second module actually needs it (YAGNI).

`OutboxPublisherHostedService` (a `BackgroundService`, registered by
`AddShipmentsModule`) polls that table and drains it to RabbitMQ's
`fleet.events` topic exchange. See
[`src/Modules/Shipments/FleetDelivery.Modules.Shipments.Infrastructure/Messaging/`](src/Modules/Shipments/FleetDelivery.Modules.Shipments.Infrastructure/Messaging/)
for the implementation; the short version:

- **Claiming strategy: `SELECT ... FOR UPDATE SKIP LOCKED`**, inside an
  explicit transaction (`OutboxBatchProcessor.ProcessBatchAsync`). Two
  processors racing the same poll (two threads, or the same code running in
  two app instances) never claim the same row — the loser's `SELECT` simply
  skips whatever the winner already locked. No separate lease/claimed-by
  column: the Postgres row lock *is* the claim, and it's released
  automatically if the process crashes mid-batch (the transaction never
  commits), so a crash between claiming and publishing just leaves the row
  to be claimed again next poll — never stuck, never lost. Covered by
  `OutboxPublisherTests.Two_concurrent_batch_processors_never_publish_the_same_row_twice`
  against real concurrent processors, not a single-threaded assumption.
- **Retry**: a failed publish increments `AttemptCount`, records `Error`,
  and sets `NextAttemptOn` to an exponential backoff capped at
  `Outbox:MaxBackoffSeconds` (default 300s) — a persistently-failing row is
  retried at most that often, **never abandoned** (no message is ever
  silently given up on; there's no dead-letter step on the publisher side,
  since nothing has rejected the message — it just hasn't been sent yet).
- **At-least-once delivery**: if the process crashes after a successful
  RabbitMQ publish but before the row is marked `ProcessedOn` (committed),
  the row is reclaimed and republished on restart — a duplicate delivery,
  not a lost one. This is why `IntegrationEventEnvelope.MessageId` is the
  outbox row's own id: a consumer's Inbox pattern (see
  docs/ARCHITECTURE.md's "Idempotent consumers") keys off exactly this id to
  no-op a redelivery. No consumer exists yet in this milestone — this is the
  contract the first one must honor.
- **Envelope**: `{ messageId, type, occurredAt, correlationId, data }`
  (camelCase, matching every other JSON contract this API emits), published
  `Persistent` to the durable `fleet.events` topic exchange under a routing
  key derived from the event type (`ShipmentIntegrationEventRoutingKeys`,
  e.g. `DeliveryCompleted` -> `shipment.delivered`, per
  docs/ARCHITECTURE.md's topic naming). `correlationId` is currently
  self-referential (same value as `messageId`) — no request-scoped
  correlation id is threaded through commands yet; that's future work
  alongside real distributed tracing, not fabricated here.
- **Connection resilience**: `RabbitMqConnectionProvider` holds one
  long-lived `IConnection` per process with
  `AutomaticRecoveryEnabled`/`TopologyRecoveryEnabled` — reconnects after a
  dropped connection without a hand-rolled retry loop.
- **No consumer queues or DLQ declared by the publisher.** Declaring the
  exchange is the publisher's job; binding queues (and any dead-lettering
  for messages a *consumer* rejects) is whichever module adds the first real
  consumer's job — not manufactured speculatively here.
- **Disabled in most integration tests** (`Outbox:PublisherEnabled = false`,
  set by `ShipmentsApiFactory`) — those tests don't spin up a RabbitMQ
  container and don't exercise messaging.
  [`OutboxPublisherApiFactory`](../backend/tests/FleetDelivery.IntegrationTests/Infrastructure/OutboxPublisherApiFactory.cs)
  is the one that does (Postgres + RabbitMQ via Testcontainers), and its
  tests call `OutboxBatchProcessor.ProcessBatchAsync` directly rather than
  waiting on the hosted service's poll timer, for determinism.

### Shipments module — EF Core migrations

```bash
cd backend
dotnet ef database update \
  --project src/Modules/Shipments/FleetDelivery.Modules.Shipments.Infrastructure/FleetDelivery.Modules.Shipments.Infrastructure.csproj \
  --startup-project src/FleetDelivery.Api/FleetDelivery.Api.csproj \
  --context ShipmentsDbContext
```

To add a new migration after changing `Shipment`/`TrackingEvent`/`DeliveryAttempt`/their `IEntityTypeConfiguration`s:

```bash
dotnet ef migrations add <Name> \
  --project src/Modules/Shipments/FleetDelivery.Modules.Shipments.Infrastructure/FleetDelivery.Modules.Shipments.Infrastructure.csproj \
  --startup-project src/FleetDelivery.Api/FleetDelivery.Api.csproj \
  --context ShipmentsDbContext \
  --output-dir Persistence/Migrations
```

No dev seed for this module — there's no meaningful default shipment to
create automatically the way there's a default Admin user.

## Vehicles module

Owns the `vehicles` Postgres schema exclusively via `VehiclesDbContext`.
Currently create-and-read only: `Vehicle.Register` (plate number, type,
capacity) always starts a vehicle `Active` — there's no
send-to-maintenance/retire workflow yet because nothing in the product needs
one (dispatch only reads `Status` to offer `Active` vehicles for assignment).
See the doc comment on
[`Vehicle`](src/Modules/Vehicles/FleetDelivery.Modules.Vehicles.Domain/Vehicle.cs)
for why a status-transition method was deliberately left out rather than
speculatively adding a public setter.

Plate numbers are normalized (trimmed, upper-cased) and enforced unique at
the DB level (`ix_vehicles_plate_number`) — the real invariant, with
`IVehicleRepository.ExistsByPlateNumberAsync` only a friendly fast-path in
front of it; a race that slips past the fast-path surfaces as a Postgres
unique-violation, translated by `VehiclesDbContext.SaveChangesAsync` into a
409, not a raw 500. No Outbox wiring here (unlike Identity/Shipments) —
nothing downstream needs a `VehicleRegistered` integration event yet; add it
if a real consumer shows up instead of wiring an unused table now.

### Endpoints (`/api/vehicles`)

| Method & path | Roles | Notes |
|---|---|---|
| `GET /api/vehicles` | any | Optional `status` filter (`Active`/`Maintenance`/`Retired`) |
| `GET /api/vehicles/{id}` | any | 404 if unknown |
| `POST /api/vehicles` | Dispatcher, Admin | Body: `{ plateNumber, type, capacityKg }`; 409 on a duplicate plate number |

### Vehicles module — EF Core migrations

```bash
cd backend
dotnet ef database update \
  --project src/Modules/Vehicles/FleetDelivery.Modules.Vehicles.Infrastructure/FleetDelivery.Modules.Vehicles.Infrastructure.csproj \
  --startup-project src/FleetDelivery.Api/FleetDelivery.Api.csproj \
  --context VehiclesDbContext
```

## Real-time: dispatch board (M5)

`DispatchBoardConsumerHostedService` (in `FleetDelivery.Api/RealTime/`) is
the first real consumer of M4's outbox events: it binds a durable queue to
`fleet.events` (routing pattern `#` — a dispatch board's whole point is
seeing everything) and re-broadcasts each message to `DispatchHub`
(`FleetDelivery.Api/Hubs/`) over SignalR at `/hubs/dispatch`.

- **Authorization**: the hub requires authentication; group membership is
  derived from the connection's JWT claims in `OnConnectedAsync` — a
  Dispatcher/Admin joins the shared `dispatchers` group (which the consumer
  broadcasts to), never from anything the client sends. A Driver connecting
  gets their own per-user group only, receiving nothing from the dispatchers
  broadcast — covered by
  `DispatchBoardTests.An_authenticated_Driver_does_not_receive_dispatchers_group_broadcasts`
  against a real connection, not just a design claim.
- **JWT over SignalR**: browsers can't set an `Authorization` header on a
  WebSocket handshake, so the token travels as an `access_token` query
  parameter instead (`JwtBearerEvents.OnMessageReceived` in `Program.cs`,
  scoped to paths under `/hubs` only — it doesn't relax auth anywhere else).
  Standard, documented ASP.NET Core SignalR pattern, not a workaround.
- **Dead-lettering**: this consumer's queue is declared with
  `x-dead-letter-exchange` pointing at a fanout `fleet.events.dlx`. A
  message that fails to process (malformed payload — its only realistic
  failure mode, since broadcasting is in-process and has no external
  dependency to be transiently down) is nack'd without requeue and lands in
  `dispatch-board.fleet.events.dlq` for inspection — never retried in a
  loop, never silently dropped. Simpler than the outbox publisher's
  bounded-backoff retry deliberately: see the doc comment on
  `DispatchBoardConsumerHostedService` for why that asymmetry is
  intentional, not an oversight.
- **What ships out**: `DispatchBoardEvent { type, occurredAt, data }` — a
  thin projection of the RabbitMQ envelope (drops `messageId`/`correlationId`,
  which are internal plumbing a browser has no use for), pushed as the
  `shipmentEvent` SignalR method.
- **Frontend**: `frontend/src/features/dispatch/` — `DispatchBoardPage`
  replaces the old placeholder route, showing a live, capped (50 most
  recent) feed with a connection-status indicator
  (connecting/live/reconnecting/disconnected). It's a supplement to the
  Shipments list, not a replacement — no attempt to reconstruct full
  shipment state from the event stream alone.
- **Disabled in most integration tests** (`RealTime:ConsumerEnabled = false`
  in `ShipmentsApiFactory`, same reasoning as `Outbox:PublisherEnabled`).
  `DispatchBoardTests` uses `OutboxPublisherApiFactory` (real RabbitMQ)
  with the consumer left at its default-enabled setting, and a real
  `Microsoft.AspNetCore.SignalR.Client` connection over the
  `WebApplicationFactory`'s `TestServer` (via `HttpTransportType.LongPolling`
  — `TestServer` doesn't support real WebSockets, the documented testing
  workaround) — proving the full pipeline end to end: create shipment ->
  outbox row -> published -> consumed -> broadcast -> a real connected
  client receives it.

## Required configuration

`appsettings.json` (the base file, tracked in git) ships **empty**
placeholders for everything below — production must supply real values via
environment variables (e.g. `Jwt__SigningKey`, using ASP.NET Core's `:` ->
`__` env var convention). `appsettings.Development.json` carries actual
dev-only values (including a real signing key) since it's a tracked,
non-secret local-dev convenience per this repo's `.gitignore`
(`appsettings.Local.json` is the gitignored escape hatch for anything that
shouldn't be tracked at all).

| Key | Purpose |
|---|---|
| `ConnectionStrings:Postgres` | Npgsql connection string |
| `Jwt:Issuer` / `Jwt:Audience` | JWT `iss`/`aud` validation |
| `Jwt:SigningKey` | HMAC-SHA256 signing key, **must be ≥ 32 bytes UTF-8-encoded** |
| `Cors:AllowedOrigin` | Single allowed frontend origin (default dev value: `http://localhost:5173`) |
| `RateLimiting:Login:PermitLimit` | Requests/window allowed on `POST /api/auth/login` per client IP (default: `5`) |
| `RateLimiting:Login:WindowSeconds` | Window size in seconds (default: `60`) |
| `RabbitMq:Host` / `Port` / `Username` / `Password` / `VirtualHost` | Broker connection (`Port` default `5672`, `VirtualHost` default `/`) |
| `RabbitMq:ExchangeName` | Topic exchange every integration event publishes to (default `fleet.events`) |
| `Outbox:PublisherEnabled` | Whether `OutboxPublisherHostedService` runs at all (default `true`; test hosts without a RabbitMQ container set this `false`) |
| `Outbox:PollIntervalSeconds` | How often the publisher polls for unprocessed rows (default `2`) |
| `Outbox:BatchSize` | Max rows claimed per poll (default `20`) |
| `Outbox:MaxBackoffSeconds` | Ceiling on a failing row's retry backoff (default `300`) |
| `RealTime:ConsumerEnabled` | Whether `DispatchBoardConsumerHostedService` runs (default `true`; test hosts without a RabbitMQ container set this `false`) |
| `RealTime:QueueName` / `RoutingPattern` / `DeadLetterExchangeName` / `DeadLetterQueueName` | Dispatch board consumer's queue/DLQ topology — sane defaults, rarely need overriding |

## Auth: tokens, cookies, rotation

- **Access token**: JWT, HMAC-SHA256, 15-minute lifetime. Claims: `sub`
  (user id), `email`, and a `role` claim written with the full
  `ClaimTypes.Role` URI so `[Authorize(Roles = "...")]` works without any
  inbound claim-type remapping.
- **Refresh token**: 256-bit random value (`RandomNumberGenerator`),
  returned to the client raw exactly once as an `httpOnly`, `Secure`,
  `SameSite=Lax` cookie named `refreshToken`, scoped to path `/api/auth`.
  Only its SHA-256 hash is ever persisted (`RefreshToken.TokenHash`). 7-day
  lifetime (not spec-mandated beyond "cryptographically random, rotates on
  use" — see `JwtTokenService.RefreshTokenLifetime`).
- **`SameSite=Lax`, not `Strict`**: chosen because `Strict` cookies aren't
  reliably attached to same-site-but-cross-port fetches during local dev
  (SPA on `:5173`, API on a different port) in every browser, and would
  behave the same way across any future cross-subdomain deployment split.
  `Lax` still blocks the CSRF pattern that matters here (cross-site
  top-level navigation), while `Secure` + a fixed `Path` + `HttpOnly` do the
  rest of the hardening.
- **Rotation**: every successful `POST /api/auth/refresh` issues a new
  refresh token and revokes the old one (`RevokedAt` +
  `ReplacedByTokenHash`). Reusing an already-revoked token is treated as a
  theft signal: the entire active-token chain for that user is revoked too
  (`RefreshTokenCommandHandler`), not just the replayed token — a stricter
  bar than the spec's minimum ("rotation + single-use").
- **Login response never reveals account existence**: unknown email, wrong
  password, and inactive account all produce the same `401` with the same
  generic message.

## Tests

```bash
dotnet test FleetDelivery.sln
```

- **`FleetDelivery.UnitTests`** — password hashing round-trip,
  `RefreshToken`'s own state-transition methods (issue / rotate / revoke,
  no DB), JWT claim/expiry generation; `Shipment`'s own transition table
  (valid transitions append the right `TrackingEvent`/raise the right domain
  event, invalid ones throw `InvalidShipmentTransitionException`, a
  driver-owned transition with the wrong `driverId` throws
  `ShipmentDriverMismatchException`, `Cancel` rejects once `PickedUp`,
  `Delivered` rejects every further transition); `Vehicle.Register`'s
  validation (normalizes the plate number, rejects an empty one or a
  non-positive capacity). No external dependencies.
- **`OutboxPublisherTests`** (in `FleetDelivery.IntegrationTests`, via
  `OutboxPublisherApiFactory` — Postgres + a real, disposable RabbitMQ
  broker via Testcontainers): creating a shipment's outbox row actually gets
  delivered to a real queue bound to `fleet.events` under
  `shipment.created`, with the documented envelope shape; a publish failure
  leaves the row unprocessed with `AttemptCount`/`Error`/`NextAttemptOn` set
  and — critically — is **not** reclaimed before `NextAttemptOn`; and two
  concurrent `OutboxBatchProcessor`s racing the same rows never publish the
  same message twice (real proof of the `FOR UPDATE SKIP LOCKED` claiming
  strategy, not an assumption).
- **`DispatchBoardTests`** (same factory): a real, authenticated SignalR
  client receives a real broadcast end to end when a shipment is created
  (create -> outbox -> publish -> consume -> broadcast -> received, no step
  mocked); a Driver's connection never receives the dispatchers-group
  firehose — real proof of the hub's server-side group authorization, not
  an assumption.
- **`FleetDelivery.ArchitectureTests`** — NetArchTest rules: Identity.Domain,
  Shipments.Domain, and Vehicles.Domain have no dependency on their own
  module's Infrastructure, ASP.NET Core, or EF Core; same for each module's
  Application layer; Identity, Shipments, and Vehicles never depend on each
  other's Infrastructure either (all may depend on BuildingBlocks) —
  including the one real cross-module Application call each way
  (Shipments -> Identity for driver validation, Shipments -> Vehicles for
  vehicle validation; Vehicles itself makes no cross-module calls).
- **`FleetDelivery.IntegrationTests`** — `Testcontainers.PostgreSql` spins up
  a real, disposable Postgres per test class; `Microsoft.AspNetCore.Mvc.Testing`'s
  `WebApplicationFactory` runs the real `FleetDelivery.Api` host (in the
  `Development` environment, so its own auto-migrate + dev-seed path sets up
  all three schemas and the admin user) against it, and tests hit the real
  `/api/auth/*`, `/api/shipments/*`, and `/api/vehicles/*` endpoints over
  HTTP. The Shipments suite (`ShipmentEndpointsTests`) drives a shipment
  through its full lifecycle — including a real driver+vehicle assignment,
  registering an `Active` vehicle via the real `/api/vehicles` endpoint first
  — with real JWTs for a Dispatcher and a Driver seeded via
  `ShipmentsApiFactory.CreateUserAsync`; asserts a Driver acting on (or even
  viewing) a shipment not assigned to them gets a real 404; asserts a stale
  `expectedVersion` gets a real 409 (an actual two-write race against
  Postgres, not a mock); asserts assigning a nonexistent `vehicleId` gets a
  real 400; asserts `GET /api/shipments/drivers` correctly marks a driver
  unavailable once assigned to an in-progress shipment; and asserts a state
  transition leaves a matching row in `shipments.outbox_messages` in the
  same call. The Vehicles suite (`VehicleEndpointsTests`) covers
  register-then-read-back, RBAC (a Driver can't register), a duplicate plate
  number returning 409, and an unknown id returning 404. **Requires a
  working Docker daemon** — if `docker info` doesn't work in your environment, these
  won't be able to start their container. (In WSL specifically: Testcontainers
  can find a usable daemon via Docker Desktop's WSL2 integration socket even
  without that particular distro checked in Docker Desktop's "Resources → WSL
  Integration" list — look for something under `/mnt/wsl/docker-desktop/`.)

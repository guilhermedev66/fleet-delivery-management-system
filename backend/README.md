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

There is no separate Drivers module yet — that's M3 (license, capacity, and
other driver-profile data). For M2, `Shipment.AssignedDriverId` is a plain
`Guid` referencing `identity.users.id`, with **no cross-schema foreign key**
(per the modular-monolith rule: cross-module references are plain IDs,
validated at the application layer, never DB-enforced). `AssignCommand`
validates a `driverId` by calling Identity's own Application public contract
(`GetCurrentUserQuery`, sent via MediatR's `ISender`) rather than querying
the `identity` schema directly — the in-process cross-module call style from
docs/ARCHITECTURE.md. Driver-only endpoints (`pickup`, `in-transit`,
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
| `POST /api/shipments/{id}/ready-for-dispatch` | Dispatcher, Admin | Serves both `Draft -> ReadyForDispatch` and `Rescheduled -> ReadyForDispatch` — see the doc comment on `ReadyForDispatchCommand` |
| `POST /api/shipments/{id}/assign` | Dispatcher, Admin | Body: `{ driverId, expectedVersion }` |
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

### Outbox: written, not yet published

`ShipmentsDbContext.SaveChangesAsync` is overridden to walk every tracked
aggregate's pending domain events and write one `OutboxMessage` row per
event to its own `shipments.outbox_messages` table, in the same
`SaveChanges` call/transaction as the state change — see the doc comment on
`ShipmentsDbContext`. **No RabbitMQ publisher runs against these rows yet**
(that's M4 scope): they sit with `ProcessedOn = null` indefinitely until a
background publisher is added. This module's Outbox table is deliberately
its own copy rather than shared with Identity's schema — Identity doesn't
have Outbox wiring yet either, and sharing one table across module schemas
is a bridge to cross once a second module actually needs it (YAGNI).

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
  `Delivered` rejects every further transition). No external dependencies.
- **`FleetDelivery.ArchitectureTests`** — NetArchTest rules: Identity.Domain
  and Shipments.Domain have no dependency on their own module's
  Infrastructure, ASP.NET Core, or EF Core; Identity.Application and
  Shipments.Application have no dependency on their own module's
  Infrastructure; Identity and Shipments never depend on each other's
  Infrastructure either (both may depend on BuildingBlocks).
- **`FleetDelivery.IntegrationTests`** — `Testcontainers.PostgreSql` spins up
  a real, disposable Postgres per test class; `Microsoft.AspNetCore.Mvc.Testing`'s
  `WebApplicationFactory` runs the real `FleetDelivery.Api` host (in the
  `Development` environment, so its own auto-migrate + dev-seed path sets up
  both schemas and the admin user) against it, and tests hit the real
  `/api/auth/*` and `/api/shipments/*` endpoints over HTTP. The Shipments
  suite (`ShipmentEndpointsTests`) drives a shipment through its full
  lifecycle with real JWTs for a Dispatcher and a Driver seeded via
  `ShipmentsApiFactory.CreateUserAsync`; asserts a Driver acting on (or even
  viewing) a shipment not assigned to them gets a real 404; asserts a stale
  `expectedVersion` gets a real 409 (an actual two-write race against
  Postgres, not a mock); and asserts a state transition leaves a matching row
  in `shipments.outbox_messages` in the same call. **Requires a working
  Docker daemon** — if `docker info` doesn't work in your environment, these
  won't be able to start their container. (In WSL specifically: Testcontainers
  can find a usable daemon via Docker Desktop's WSL2 integration socket even
  without that particular distro checked in Docker Desktop's "Resources → WSL
  Integration" list — look for something under `/mnt/wsl/docker-desktop/`.)

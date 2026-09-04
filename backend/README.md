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
  no DB), JWT claim/expiry generation. No external dependencies.
- **`FleetDelivery.ArchitectureTests`** — NetArchTest rules: Identity.Domain
  has no dependency on Identity.Infrastructure, ASP.NET Core, or EF Core;
  Identity.Application has no dependency on Identity.Infrastructure.
- **`FleetDelivery.IntegrationTests`** — `Testcontainers.PostgreSql` spins up
  a real, disposable Postgres per test class; `Microsoft.AspNetCore.Mvc.Testing`'s
  `WebApplicationFactory` runs the real `FleetDelivery.Api` host (in the
  `Development` environment, so its own auto-migrate + dev-seed path sets up
  the schema and admin user) against it, and tests hit the real
  `/api/auth/*` endpoints over HTTP. **Requires a working Docker daemon** —
  if `docker info` doesn't work in your environment, these won't be able to
  start their container. (In WSL specifically: Testcontainers can find a
  usable daemon via Docker Desktop's WSL2 integration socket even without
  that particular distro checked in Docker Desktop's "Resources → WSL
  Integration" list — look for something under `/mnt/wsl/docker-desktop/`.)

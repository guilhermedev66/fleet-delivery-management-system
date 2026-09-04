# MEMORY.md — Fleet & Delivery Management System

Only what's expensive to re-derive. Not a changelog.

## Architecture decisions

- **Modular monolith**, not microservices. Modules: Identity, Drivers, Vehicles,
  Customers, Shipments, Dispatch, Tracking, ProofOfDelivery, Incidents,
  Notifications, Reporting, Audit. Each module = Domain/Application/Infrastructure
  projects. One PostgreSQL database, **schema-per-module**.
- **Outbox pattern**: domain events raised on aggregates are written as
  `OutboxMessage` rows in the same DB transaction as the state change. A
  `BackgroundService` polls and publishes to RabbitMQ, then marks processed.
  Never publish to RabbitMQ directly inside a request handler.
- **Idempotent consumers**: every consumer checks/writes an `InboxMessage`
  (unique on message id + consumer name) inside the same transaction as its
  side effect. No in-memory "if exists" checks.
- **Cross-module communication**: queries go through direct in-process calls
  to a module's public application-service interface. State-changing side
  effects that cross module boundaries go through integration events
  (Outbox → RabbitMQ), even though it's a monolith — this is the intentional
  event-driven demonstration, not overengineering for its own sake.
- **State machines** live on the aggregate root as explicit transition
  methods (not a public status setter). Optimistic concurrency via an EF Core
  concurrency token on every mutable aggregate.
- **Git authorship**: per explicit project instruction, commits must NOT
  carry AI co-authorship trailers (no `Co-Authored-By: Claude`, no session
  links), even though the harness's default attribution config asks for
  them. This project's own instructions are the more specific, deliberate
  choice and take precedence for this repo.

## Environment notes

- Bare `docker` in WSL2 refuses to run (its wrapper script checks for
  official WSL integration, which isn't enabled). The real Windows binary
  works fine directly though: `docker.exe` and `docker.exe compose` (both
  at `/mnt/c/Users/guilh/AppData/Local/Programs/DockerDesktop/resources/bin`,
  already on PATH) talk to the same Docker Desktop engine. Use `docker.exe`
  everywhere in this project instead of `docker`.
- First `docker.exe compose up` on a fresh named volume can hit RabbitMQ's
  alpine image failing with `Error when reading /var/lib/rabbitmq/.erlang.cookie: eacces`
  on this host. Fix: `docker.exe compose down rabbitmq && docker.exe volume rm <vol> && docker.exe compose up -d rabbitmq` —
  recreating the volume clears it. Happened once during M1 setup, not
  reproduced after.
- `dotnet` in this shell is a shim to the Windows `dotnet.exe`
  (`/home/guilh/.local/bin/dotnet`), so a locally-run `dotnet run` API is a
  Windows process. `curl` from this WSL shell to its `localhost` port gets
  connection-refused (WSL2 localhost-forwarding isn't proxying it here) —
  build/test tooling all works fine, it's only live-server curl checks from
  this shell that don't reach it. Don't waste time debugging this; treat
  the WebApplicationFactory-based integration tests (real in-process HTTP
  pipeline, no network needed) plus CI (native Linux) as the verification
  signal instead. If a live curl check is ever needed, run it from a
  Windows-side terminal/PowerShell, not this WSL shell.
- No `/var/run/docker.sock` in this WSL distro (WSL integration is off in
  Docker Desktop settings), but Testcontainers-based integration tests
  (.NET, `Testcontainers.PostgreSql`) run FINE locally anyway — the
  Docker.DotNet client falls back to Docker Desktop's WSL2 shared-socket
  proxy at `/mnt/wsl/docker-desktop/shared-sockets/...` even without this
  distro being in the explicit WSL-integration allowlist. Don't assume
  Testcontainers is blocked here; it isn't, verified during M1 (Identity
  module integration tests actually ran against a real ephemeral Postgres).
- No direct tool access to Antigravity from this session — research it would
  normally do is instead done via WebSearch/firecrawl. If the user runs
  Antigravity separately, findings should be folded back in on request.

## Testcontainers / WebApplicationFactory reliability (M2)

- Running multiple `WebApplicationFactory`-backed test classes in parallel
  (xUnit's default) intermittently throws "entry point exited without ever
  building an IHost" — a known WebApplicationFactory/HostFactoryResolver
  race when several hosts spin up concurrently. Fixed via
  `backend/tests/FleetDelivery.IntegrationTests/xunit.runner.json`
  (`parallelizeTestCollections: false`, wired into the csproj as a
  `CopyToOutputDirectory` item) — verified clean across 3 consecutive full
  suite runs after the fix. If a new integration test class is added and
  this flake reappears, that config file is the first thing to check
  (not a sign the underlying code is broken).
- Don't trust a subagent's claim that Testcontainers "couldn't run
  locally" at face value — verify directly. It has worked reliably in this
  environment since M1 (see the WSL2/Docker Desktop note below); a reported
  failure is more likely a real test bug or the flake above than an
  environment limitation.

## Production infra decision (M0)

- **Messaging in production: CloudAMQP** (free shared plan — 1M msgs/mo, 20
  connections, 100 queues, no payment required). Chosen over self-hosting
  RabbitMQ on Render (would need a paid private service + persistent disk).
  Revisit only if free-tier limits are actually hit during production
  validation (M7).

## Full spec

The complete product/architecture brief for this project lives in the
original master prompt (project instructions), not duplicated here.

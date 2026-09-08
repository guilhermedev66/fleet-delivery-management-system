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

## WebApplicationFactory + IHostedService gotcha (found via CI failure, M5)

**Never gate `AddHostedService<T>()` registration on a raw `IConfiguration`
read at `Program.cs`'s top level.** `WebApplicationFactory`'s test config
overrides (`ConfigureWebHost` -> `ConfigureAppConfiguration`) are merged in
when the real host is built — which happens *after* `Program.cs`'s own
top-level statements have already executed (`HostFactoryResolver` replays
the original entry point to capture the builder, then WebApplicationFactory
applies its overrides to that captured builder, then calls the real
`Build()`). So `builder.Configuration.GetValue(...)` read directly in
`Program.cs` sees only the original config sources — never a test factory's
override — while `IOptions<T>` (via `services.Configure<T>(section)`)
resolves lazily at DI-construction time, *after* the real `Build()`, and
correctly sees the merged config. Symptom when this goes wrong: a hosted
service starts in a test host that deliberately has no broker/dependency
for it, throws inside `ExecuteAsync`, and — under .NET's default
`BackgroundServiceExceptionBehavior.StopHost` — crashes the *entire* test
host, failing every test built on that factory with a misleading "Server
hasn't been initialized yet" from `WebApplicationFactory.CreateClient()`.
Passed locally, only broke on CI (a timing race in how fast the failed
connection attempt outraced `WebApplicationFactory`'s own startup — not
reliably reproducible, don't try to chase it locally).

**Fix, applied to both `OutboxPublisherHostedService` and
`DispatchBoardConsumerHostedService`**: always register the hosted service;
check the enable/disable flag as the *first* thing inside `ExecuteAsync`,
via the properly-injected `IOptions<T>`, and return immediately if
disabled. The CORS/rate-limiter config reads in `Program.cs` already
avoided this exact pitfall (see their inline comments) — this is the same
lesson, generalized to hosted-service registration.

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

## Vehicles module (added after M2)

- Vehicles is create-and-read only by design: `Vehicle.Register` always
  starts `Active`, no status-transition method exists yet. Don't add one
  speculatively — wait for a real "send to maintenance"/"retire" UI need,
  then give it the same explicit-transition-table treatment as `Shipment`.
- No Outbox wiring on `VehiclesDbContext` (unlike Identity/Shipments) —
  nothing downstream consumes a `VehicleRegistered` event yet. Add it only
  when a real consumer shows up.
- Plate numbers are normalized (trim + upper-invariant) in `Vehicle.Register`
  and enforced unique via a DB index (`ix_vehicles_plate_number`). A
  duplicate is checked both as a repository pre-check (friendly error, fast
  path) *and* as a Postgres unique-violation caught in
  `VehiclesDbContext.SaveChangesAsync` and translated to
  `DuplicatePlateNumberException` (race-safe backstop) — the DB constraint is
  the actual invariant, the pre-check is just UX.
- Shipment assignment now validates **both** driverId (existing, via
  Identity's `GetCurrentUserQuery`) and vehicleId (via Vehicles'
  `GetVehicleByIdQuery`, must be `Active`) — same in-process cross-module
  call style both times, never a direct cross-schema query.
- `GET /api/shipments/drivers` lives in the *Shipments* module (not
  Identity) since "which drivers, and are they free for a new assignment" is
  a Shipments-domain question — it calls Identity's `ListDriversQuery` for
  the roster, then cross-references `IShipmentRepository.GetBusyDriverIdsAsync`
  (busy = assigned to a shipment in `Assigned`/`PickedUp`/`InTransit`/`OutForDelivery`)
  to compute `isAvailable`. Busy drivers stay in the list (disabled in the
  UI with a reason), never silently filtered out.

## M4 — RabbitMQ Outbox Publisher (backend-complete, handoff below)

- **Claiming strategy: `SELECT ... FOR UPDATE SKIP LOCKED`** inside an
  explicit transaction (`OutboxBatchProcessor.ProcessBatchAsync`), not a
  lease/claimed-by column. Chosen because the Postgres row lock releases
  itself on crash/rollback — no stale-claim cleanup logic needed, unlike a
  lease that can outlive the worker that took it. Proven safe with a real
  test running two concurrent processors against real Postgres
  (`OutboxPublisherTests.Two_concurrent_batch_processors_never_publish_the_same_row_twice`),
  not just reasoned about.
- **Retry**: added `AttemptCount`/`NextAttemptOn` to `OutboxMessage` (shared
  BuildingBlocks type). Exponential backoff capped at
  `Outbox:MaxBackoffSeconds` (default 300s) — bounded *frequency*, not
  bounded *attempts*: a row is retried forever, never abandoned (no
  publisher-side dead-letter step — DLQ is a consumer-rejection concept,
  and there are no consumers yet).
- **At-least-once delivery, by design**: a crash between a successful
  RabbitMQ publish and the DB commit that marks `ProcessedOn` causes a
  redelivery on restart. `IntegrationEventEnvelope.MessageId` is the outbox
  row's own id specifically so a future consumer's Inbox pattern can
  de-duplicate on it. Don't "fix" this into exactly-once — it's the standard
  outbox/broker tradeoff, and the mitigation is the consumer's job.
- **RabbitMQ is Shipments-module-local** (`RabbitMqOptions`,
  `RabbitMqConnectionProvider`, the publisher itself all live in
  `Shipments.Infrastructure/Messaging/`), same YAGNI reasoning as the
  per-module Outbox table — promote to BuildingBlocks only once a second
  module needs to publish. `IIntegrationEventPublisher` (the interface) and
  `IntegrationEventEnvelope`/`OutboxMessage` (the shapes) DO live in
  BuildingBlocks since they're the generic, reusable parts.
- **JSON casing fix**: `ShipmentsDbContext`'s domain-event -> `OutboxMessage.Content`
  serialization was PascalCase (an M2 oversight, harmless until something
  actually read the content). Switched to `JsonSerializerDefaults.Web`
  (camelCase) to match the HTTP API and the new RabbitMQ envelope — a
  one-line, low-risk fix, in-scope for M4 since it directly affects the
  published event contract.
- **Publisher tests disabled by default**: `Outbox:PublisherEnabled=false`
  in `ShipmentsApiFactory` (no RabbitMQ container there). A dedicated
  `OutboxPublisherApiFactory` spins up Postgres + RabbitMQ via
  Testcontainers; its tests call `OutboxBatchProcessor.ProcessBatchAsync`
  directly rather than waiting on the hosted service's poll timer.
- **Not built** (deliberately out of scope for a publisher-only milestone):
  any real consumer, queue bindings, or DLQ topology (all consumer-side
  concerns); request-scoped `correlationId` propagation (currently
  self-referential — see the doc comment on `RabbitMqIntegrationEventPublisher`);
  automated tests for DB-outage or graceful-shutdown-mid-batch (reasoned
  about via the transaction/cancellation-token design, not empirically
  tested — would need killing a Testcontainer mid-test, judged
  disproportionate for this milestone).
- **Evidence at handoff**: 81 backend tests green (39 unit, 18 architecture,
  24 integration — including 3 new `OutboxPublisherTests`), `dotnet build`
  clean (0 warnings), CI green on push.

### Ownership note (2026-09-08)

User's stated preference: substantial backend work should go to a Codex
Backend agent when one is reachable in-session, with Claude as orchestrator.
Checked via `ListAgents` this session — no Codex mechanism (agent type, MCP
server, or CLI) is present here; only local subagent types and unrelated
Remote Control sessions. Don't assume that's permanent — recheck with
`ListAgents` at the start of future sessions rather than assuming
unavailability carries over. When Codex Backend *is* reachable, hand off
new backend milestones to it rather than implementing them directly;
finishing a unit Claude already started mid-flight is the documented
exception, not a general license to keep building backend features solo.

## M5 — Real-time Dispatch Board (RabbitMQ consumer + SignalR)

- **First real consumer of M4's outbox events.** Chose "dispatch board"
  over a new "Tracking" module because `@microsoft/signalr` was already a
  frontend dependency (scaffolded at M0, unused until now), the
  architecture doc already specifies the SignalR security model in detail,
  and Shipments already records everything a dispatch feed needs — no new
  schema/module was justified for a pure broadcast.
- **Consumer + Hub live in `FleetDelivery.Api`**, not inside Shipments —
  unlike the M4 publisher, this isn't shipment-domain business logic, it's
  a cross-cutting integration concern (mirrors where Endpoints/health
  checks already live: composition-root concerns sit in Api). It reuses
  Shipments' `RabbitMqConnectionProvider` (already a DI singleton) for its
  channel rather than opening a second broker connection.
- **No Inbox/idempotency table for this consumer.** The documented Inbox
  pattern exists to guard *duplicate DB writes* on redelivery; this
  consumer only reads and broadcasts (no DB write of its own), so a
  redelivered message just causes one extra harmless UI push, not a
  correctness bug. Add Inbox tracking when a consumer that actually
  mutates data (e.g. a future Notifications module persisting a row)
  shows up — don't add the machinery pre-emptively for a stateless one.
- **DLQ, deliberately simpler than the publisher's retry**: this
  consumer's only realistic failure mode is a malformed payload (broadcast
  itself is in-process, nothing external to be transiently down), so a
  failed message is nack'd straight to a dead-letter queue — no
  bounded-retry-with-backoff machinery, unlike the publisher. Different
  failure profile, deliberately different (simpler) handling.
- **JWT-over-SignalR**: token passed as `?access_token=` query param
  (browsers can't set WS handshake headers), read via
  `JwtBearerEvents.OnMessageReceived`, scoped to `/hubs/*` paths only.
  Standard ASP.NET Core pattern, not a security relaxation elsewhere.
- **Evidence**: 83 backend tests green (39 unit, 18 architecture, 26
  integration — 2 new `DispatchBoardTests` using a real
  `Microsoft.AspNetCore.SignalR.Client` connection over
  `WebApplicationFactory`'s `TestServer` via `HttpTransportType.LongPolling`,
  the documented workaround since `TestServer` can't do real WebSockets).
  Frontend: lint/typecheck/tests(7)/build/format all green.

### Ownership note update

Still no Codex mechanism reachable this session (re-checked via
`ListAgents` before starting M5, per the user's instruction not to assume
unavailability carries over silently) — same conclusion as the M4 handoff,
not re-logging further unless that changes.

## Full spec

The complete product/architecture brief for this project lives in the
original master prompt (project instructions), not duplicated here.

# Architecture — Fleet & Delivery Management System

> **Reading this doc**: it was written as the target design *before*
> implementation started, and most of it now matches reality closely. Where
> the actual code deliberately diverges (a simpler choice, a deferred piece,
> a corrected claim), that's called out inline as **Actual:** rather than
> silently edited away — the reasoning behind a divergence is usually worth
> more than a doc that just agrees with the code. See also MEMORY.md for the
> decisions and trade-offs behind these.

## Style: modular monolith, event-driven internally

One deployable API (plus background workers), organized as independent
modules with enforced boundaries, communicating through in-process calls
(queries) and integration events over RabbitMQ (state-changing side effects
that cross module boundaries). Microservices are not justified at this
scale; the event-driven pieces are demonstrated *within* the monolith.

## Modules

Identity, Drivers, Vehicles, Customers, Shipments, Dispatch, Tracking,
ProofOfDelivery, Incidents, Notifications, Reporting, Audit.

**Actual (as of the RabbitMQ outbox / dispatch board / Proof of Delivery
work): three real modules exist — Identity, Shipments, Vehicles.** The
others aren't separate modules because nothing has needed them to be yet:
"Drivers" is just an Identity user with `Role.Driver` (see the doc comment
on `AssignCommand`); "Dispatch" (the real-time board) and "ProofOfDelivery"
(photo upload) both live inside Shipments, since dispatching and proving a
delivery are Shipments-domain operations, not independent bounded contexts
with their own state; "Tracking" is the same dispatch-board event feed, not
a separate read model. Customers, Incidents, Notifications, Reporting, and
Audit remain unbuilt — nothing in the product has needed them yet. Split
one out for real only when it actually earns independent
Domain/Application/Infrastructure projects and its own schema, not on a
schedule.

Each module is split into:

- `*.Domain` — entities, value objects, domain events, state machines. No
  EF Core, no ASP.NET references.
- `*.Application` — use cases (command/query handlers via MediatR), module's
  public contracts (interfaces + DTOs other modules are allowed to call).
- `*.Infrastructure` — EF Core `DbContext` (one per module, own PostgreSQL
  schema), repositories, outbox/inbox plumbing, RabbitMQ consumers.

`FleetDelivery.Api` is the composition root: wires DI per module, exposes
minimal API endpoints, hosts SignalR hubs.

`FleetDelivery.BuildingBlocks` is the shared kernel: base `Entity`/
`AggregateRoot`, `IDomainEvent`, outbox/inbox message shapes, result/error
types. No module-specific logic lives here.

**Actual: no MediatR pipeline behaviors exist** (no cross-cutting
validation/logging/transaction step wraps every command/query) — each
handler does its own validation and its own `SaveChangesAsync` call inline,
which has been enough at this scale. Add a pipeline behavior when the
duplication actually hurts, not speculatively.

Module boundaries are enforced with architecture tests (NetArchTest): a
module's `Domain`/`Application` may not reference another module's
`Infrastructure`, and cross-module calls must go through the other module's
`Application` public contracts, not its internals.

## Database

Single PostgreSQL instance, **one schema per module** (`identity`,
`shipments`, `dispatch`, `tracking`, ...). Each module owns its own EF Core
migrations against its own schema. No cross-schema foreign keys — cross-
module references are stored as plain IDs, validated at the application
layer, not enforced by the DB.

Invariants are protected in the database where it matters: unique
constraints (e.g. `Vehicle.PlateNumber`, `Shipment.TrackingNumber`),
concurrency tokens (a hand-rolled `Version` column, not Postgres's `xmin` —
see the doc comment on `Shipment` for why) on every mutable aggregate root.

**Actual: status enums are mapped as `varchar` with no Postgres `CHECK`
constraint** — an invalid value could theoretically land in the column via
a channel that bypasses EF Core entirely (raw SQL, a different client). The
application layer is the only thing preventing it today. A `CHECK`
constraint is a cheap, real strengthening worth adding without much
ceremony; it just hasn't been done yet. **"One active assignment per
shipment" is enforced by the state machine (`AssignedDriverId`/`AssignedVehicleId`
are scalar nullable columns — a shipment structurally has zero or one
active assignment, never a set of them) plus the optimistic concurrency
token, not a literal DB unique index** — there's nothing for a unique
index to protect against here that the concurrency token doesn't already
catch. **Idempotency-key uniqueness (the Inbox pattern) is deferred** — see
"Idempotent consumers" below.

## Shipment / Delivery state machine

```
Draft -> ReadyForDispatch -> Assigned -> PickedUp -> InTransit
      -> OutForDelivery -> Delivered

Alternative exits: DeliveryFailed, Rescheduled, Returned, Cancelled
```

Transitions are only allowed via explicit methods on the `Shipment`
aggregate (`ReadyForDispatch()`, `Assign(driverId, vehicleId)`,
`MarkPickedUp()`, ...), each validated against a transition table. Invalid
transitions throw a domain exception mapped to HTTP 409. Every transition
raises a domain event and requires the aggregate's current concurrency token
to match (optimistic concurrency) — a stale write fails with 409, not a
silent overwrite.

Rules (non-exhaustive, enforced in the aggregate, not just the API):

- `Delivered` is terminal — no transition out of it.
- `Cancelled` is only reachable before `PickedUp`.
- A driver can only transition a shipment currently assigned to them.
- Every transition writes a `DeliveryEvent`/tracking row — the audit trail
  is a side effect of the state machine, not a separate manual step.

## Event-driven architecture

**Domain events** are in-process records raised by aggregate methods
(`IDomainEvent`, same transaction). **Actual: nothing dispatches them as
MediatR `INotification`s** — there's no same-module in-process reaction to
a domain event today, so that machinery hasn't been built. What actually
happens with every domain event: `ShipmentsDbContext.SaveChangesAsync`
walks the change tracker for pending domain events and writes each one
straight to the Outbox in the same transaction (see below) — domain event
*is* the outbox payload here, not a separate notification that also
happens to feed the outbox.

**Integration events** (cross-module or cross-process) are the ones that
matter for the event-driven story: `ShipmentCreated`, `ShipmentReadyForDispatch`,
`DriverAssigned`, `ShipmentPickedUp`, `ShipmentInTransit`,
`ShipmentOutForDelivery`, `DeliveryCompleted`, `DeliveryFailed`,
`DeliveryRescheduled`, `ShipmentReturned`, `ShipmentCancelled`,
`IncidentReported` (`IncidentReported` not implemented yet — no Incidents
module).

Not every domain event becomes an integration event — only ones another
module or an external concern (Notifications, Tracking, SignalR broadcast,
Audit) genuinely needs.

### Outbox pattern

```
BEGIN TRANSACTION
  domain state change (EF Core change tracker)
  OutboxMessage row (same DbContext, same SaveChanges)
COMMIT
```

A `BackgroundService` (`OutboxPublisherHostedService`, backed by
`OutboxBatchProcessor`) polls unpublished `OutboxMessage` rows per module
schema, publishes to RabbitMQ, marks `ProcessedOn` on ack. Publish failures
leave the row unprocessed (with `AttemptCount`/`Error`/`NextAttemptOn` set)
for the next poll — never publish-then-write or write-then-publish as two
separate uncommitted steps. Claiming uses `SELECT ... FOR UPDATE SKIP
LOCKED` in an explicit transaction (safe across multiple app instances —
see backend/README.md's Outbox section for why this beats a lease/claim
column).

### RabbitMQ topology

- Topic exchange `fleet.events`, routing keys like `shipment.delivered`,
  `driver.assigned`.
- One durable queue per consumer group, bound with the routing keys it
  cares about.
- Envelope: `{ messageId, type, occurredAt, correlationId, data }`.
- Dead-letter exchange + per-queue DLQ. **Actual, publisher side**: no
  DLQ — a message that fails to *publish* isn't rejected by anything, it
  just hasn't been sent yet, so it retries with capped exponential backoff
  forever rather than dead-lettering (see backend/README.md). **Actual,
  consumer side**: the one real consumer so far (the dispatch board) does
  have a DLX (`fleet.events.dlx`, fanout) + DLQ — a message it can't process
  (malformed payload, its only realistic failure mode) is nack'd without
  requeue straight to the DLQ, deliberately simpler than a bounded-retry
  dance since there's no external dependency for that consumer to be
  transiently down for.

### Idempotent consumers

Every consumer inserts into an `InboxMessage` table (unique on
`(messageId, consumerName)`) in the *same transaction* as its side effect.
Duplicate delivery -> unique constraint violation -> no-op ack. This is a DB
constraint, not an in-memory check, so it survives worker restarts.

**Actual: not implemented yet.** The one consumer built so far (the
dispatch board) only reads a message and re-broadcasts it over SignalR —
no database write of its own, so a redelivered message just causes one
extra harmless UI push, not a duplicate-write bug. The Inbox pattern is
real, necessary machinery for the *first* consumer that actually mutates
data (e.g. a future Notifications module persisting a row) — add it then,
not speculatively now.

### Failure classification

- **Transient** (DB timeout, RabbitMQ hiccup) -> retry with backoff. This is
  what the outbox publisher does today.
- **Permanent** (bad payload, business rule violation) -> straight to DLQ,
  logged, observable — never silently dropped. This is what the dispatch
  board consumer does today; no other consumer classifies failures this way
  yet since none needs to.

## Dispatch & concurrency

Assigning a shipment to a driver+vehicle is a single transactional
operation guarded by: the shipment's concurrency token (see "Database"
above for why this isn't a literal unique constraint), and driver/vehicle
availability checks re-verified inside the transaction (never trust a
stale read). Two concurrent dispatch attempts on the same shipment must
result in exactly one winner and a 409 for the loser — tested for real with
parallel requests, not mocks.

Both driver-busy and vehicle-Active status are re-verified server-side
inside `AssignCommandHandler` — a stale/bypassed client can't assign an
already-busy driver just because the UI's picker would have disabled it.
The parallel-race claim is backed by a real test
(`ShipmentEndpointsTests`, two concurrent `Task.WhenAll` HTTP `POST
/api/shipments/{id}/assign` calls against the same shipment) asserting
exactly one 200 and one 409 — not inferred from the sequential
stale-version test alone.

## Real-time (SignalR)

Hubs are authenticated; group membership is derived from the user's
server-side claims, never from a client-supplied group name. `DispatchHub`
(`/hubs/dispatch`) is the one hub built so far: Dispatcher/Admin connections
join a shared `dispatchers` group and receive every shipment event, pushed
by `DispatchBoardConsumerHostedService` — the first real consumer of the
outbox events described above. **Actual: group membership is role-derived
only — there's no `organizationId` claim or concept anywhere in the system
(see Security model below), so there's no org-scoped group yet.** "Tracking
updates" and "driver notifications" as distinct concerns don't exist
separately from this same dispatch event feed.

## Security model

JWT bearer auth (access + rotating refresh token, refresh stored hashed).
RBAC roles: Admin, Dispatcher, Driver, Operations (Customer later if a
portal is built). Resource-based authorization handlers enforce ownership
(e.g. a Driver can only act on shipments assigned to them) server-side —
`driverId` and role are always derived from the token, never accepted from
the request body.

**Actual: there is no `organizationId` claim, and no multi-tenancy concept
anywhere in the domain model, token service, or hubs.** This is a
single-tenant system today; every "org-scoped" claim in this document
describes an intended future shape, not current behavior — don't build
against it as if it exists.

Proof of Delivery photo uploads: 5 MB size limit, MIME allowlist validated
against the actual byte signature (JPEG/PNG magic bytes), never the
declared `Content-Type` header or filename extension. **Actual storage:
the photo bytes are persisted in PostgreSQL (`bytea`), not on a filesystem**
— deliberately, to sidestep both path-traversal risk (no server-generated
filename needed when there's no file path at all) and the ephemeral-
filesystem problem most PaaS hosts have (a Render deploy's local disk
doesn't survive a restart; Postgres/Neon does). "Stored outside the web
root, never executed" is satisfied trivially this way, not by a directory
convention.

## Observability

Serilog structured JSON logs. `/health/live` and `/health/ready`
(readiness checks Postgres via `IdentityDbContext` and RabbitMQ
connectivity via `RabbitMqHealthCheck`).

**Actual: no OpenTelemetry.** No tracing spans, no `CorrelationId`
propagation through logs — `IntegrationEventEnvelope.CorrelationId` exists
in the RabbitMQ envelope shape but is currently self-referential (equal to
the message's own id), not a real cross-request trace id, because nothing
threads a request-scoped correlation id through commands yet. Real
distributed tracing (OpenTelemetry, real correlation propagation) is future
work, not currently built — don't cite this section as evidence it exists.

## Testing strategy

xUnit + Testcontainers (PostgreSQL, RabbitMQ where a test needs the real
broker). Priorities: auth/RBAC/IDOR, state transitions, dispatch
concurrency, Outbox delivery-under-failure, consumer idempotency
(duplicate message redelivery), retry/DLQ behavior, DB constraints,
Proof of Delivery upload security, SignalR group isolation. Architecture
tests enforce module boundaries. Coverage is not a target in itself.

## Frontend

React + TypeScript + Vite + Tailwind. React Query for server state, React
Router, a hand-written typed API client (`frontend/src/lib/api/`) — not
generated from the API's OpenAPI document; `AddOpenApi()` is wired up
backend-side but nothing consumes it to generate a client. SignalR client
for the live dispatch board.

**Actual: no dedicated mobile-first Driver UI yet.** Every role shares the
same desktop-first shell and pages today (role-gated action buttons within
shared pages, e.g. `ShipmentDetailPage`), not a separate driver-optimized
flow. Build one when a driver actually needs to use this on a phone in the
field, not speculatively.

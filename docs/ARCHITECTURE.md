# Architecture — Fleet & Delivery Management System

## Style: modular monolith, event-driven internally

One deployable API (plus background workers), organized as independent
modules with enforced boundaries, communicating through in-process calls
(queries) and integration events over RabbitMQ (state-changing side effects
that cross module boundaries). Microservices are not justified at this
scale; the event-driven pieces are demonstrated *within* the monolith.

## Modules

Identity, Drivers, Vehicles, Customers, Shipments, Dispatch, Tracking,
ProofOfDelivery, Incidents, Notifications, Reporting, Audit.

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
`AggregateRoot`, `IDomainEvent`, outbox message shape, result/error types,
pipeline behaviors (validation, logging, transaction). No module-specific
logic lives here.

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
constraints (idempotency keys, one active assignment per shipment),
check constraints (status enums), concurrency tokens (`xmin` or a `Version`
column) on every mutable aggregate root.

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

**Domain events** (in-process, same transaction, dispatched via MediatR
`INotification` after `SaveChanges` succeeds) drive same-transaction /
same-module reactions (e.g. updating a read model within the same module).

**Integration events** (cross-module or cross-process) are the ones that
matter for the event-driven story: `ShipmentCreated`, `ShipmentReadyForDispatch`,
`DriverAssigned`, `ShipmentPickedUp`, `ShipmentInTransit`,
`ShipmentOutForDelivery`, `DeliveryCompleted`, `DeliveryFailed`,
`DeliveryRescheduled`, `ShipmentCancelled`, `IncidentReported`.

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

A `BackgroundService` (`OutboxPublisher`) polls unpublished `OutboxMessage`
rows per module schema, publishes to RabbitMQ, marks `ProcessedAt` on ack.
Publish failures leave the row unprocessed for the next poll — never
publish-then-write or write-then-publish as two separate uncommitted steps.

### RabbitMQ topology

- Topic exchange `fleet.events`, routing keys like `shipment.delivered`,
  `driver.assigned`.
- One durable queue per consumer group, bound with the routing keys it
  cares about.
- Envelope: `{ messageId, type, occurredAt, correlationId, data }`.
- Dead-letter exchange `fleet.events.dlx` + per-queue DLQ. Retry with bounded
  attempts and backoff (redeliver via a delay before DLQ, not infinite
  requeue).

### Idempotent consumers

Every consumer inserts into an `InboxMessage` table (unique on
`(messageId, consumerName)`) in the *same transaction* as its side effect.
Duplicate delivery -> unique constraint violation -> no-op ack. This is a DB
constraint, not an in-memory check, so it survives worker restarts.

### Failure classification

- **Transient** (DB timeout, RabbitMQ hiccup) -> retry with backoff.
- **Permanent** (bad payload, business rule violation) -> straight to DLQ,
  logged, observable — never silently dropped.

## Dispatch & concurrency

Assigning a shipment to a driver+vehicle is a single transactional
operation guarded by: the shipment's concurrency token, a unique
"one active assignment per shipment" constraint, and driver/vehicle
availability checks re-verified inside the transaction (never trust a
stale read). Two concurrent dispatch attempts on the same shipment must
result in exactly one winner and a 409 for the loser — tested for real with
parallel requests, not mocks.

## Real-time (SignalR)

Hubs are authenticated; group membership is derived from the user's
server-side claims (org/role), never from a client-supplied group name.
Dispatch board, tracking updates, and driver notifications are pushed from
the same integration-event handlers that update the read side, so realtime
state always matches persisted state.

## Security model

JWT bearer auth (access + rotating refresh token, refresh stored hashed).
RBAC roles: Admin, Dispatcher, Driver, Operations (Customer later if a
portal is built). Resource-based authorization handlers enforce ownership
(e.g. a Driver can only act on shipments assigned to them) server-side —
`driverId`, `organizationId`, and role are always derived from the token,
never accepted from the request body. Uploads (Proof of Delivery photos):
size limit, MIME allowlist validated against actual content (not just the
extension), server-generated filenames, stored outside the web root,
never executed.

## Observability

OpenTelemetry across ASP.NET Core, HttpClient, Npgsql, and manually
instrumented RabbitMQ publish/consume spans, correlated via a
`CorrelationId` carried in the message envelope and log scope. Serilog
structured JSON logs. `/health/live` and `/health/ready` (readiness checks
DB + RabbitMQ connectivity).

## Testing strategy

xUnit + Testcontainers (PostgreSQL, RabbitMQ where a test needs the real
broker). Priorities: auth/RBAC/IDOR, state transitions, dispatch
concurrency, Outbox delivery-under-failure, consumer idempotency
(duplicate message redelivery), retry/DLQ behavior, DB constraints,
Proof of Delivery upload security, SignalR group isolation. Architecture
tests enforce module boundaries. Coverage is not a target in itself.

## Frontend

React + TypeScript + Vite + Tailwind. React Query for server state,
React Router, a typed API client generated or hand-written against the
API's OpenAPI contract. SignalR client for live dispatch/tracking updates.
Dispatcher UI is desktop-first (dashboard, dispatch board, tables). Driver
UI is mobile-first (My Deliveries -> Start Route -> Arrived -> outcome ->
Proof of Delivery).

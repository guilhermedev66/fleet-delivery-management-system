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

- Docker CLI is not yet wired into this WSL2 distro (Docker Desktop WSL
  integration not enabled). `docker-compose.yml` exists and is correct, but
  hasn't been run/verified locally yet — verify before declaring M1 Docker
  work PASS.
- No direct tool access to Antigravity from this session — research it would
  normally do is instead done via WebSearch/firecrawl. If the user runs
  Antigravity separately, findings should be folded back in on request.

## Production infra decision (M0)

- **Messaging in production: CloudAMQP** (free shared plan — 1M msgs/mo, 20
  connections, 100 queues, no payment required). Chosen over self-hosting
  RabbitMQ on Render (would need a paid private service + persistent disk).
  Revisit only if free-tier limits are actually hit during production
  validation (M7).

## Full spec

The complete product/architecture brief for this project lives in the
original master prompt (project instructions), not duplicated here.

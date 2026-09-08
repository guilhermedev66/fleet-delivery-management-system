# Fleet & Delivery Management System

A logistics operations platform for dispatching shipments, tracking
deliveries in real time, and managing drivers, vehicles, and routes —
built to demonstrate a real event-driven backend, not another CRUD app.

> Status: **in development (shipment & delivery lifecycle, fleet vehicle
> registry, RabbitMQ outbox publisher, real-time dispatch board, Proof of
> Delivery uploads)**. Auth/RBAC, CI, Docker, and the app shell are in
> place. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full
> design — including where it documents *intended* future shape (org-scoped
> multi-tenancy, OpenTelemetry tracing, a dedicated mobile driver UI) versus
> what's actually built today.

## Overview

Dispatchers assign shipments to drivers and vehicles; drivers execute
deliveries and capture Proof of Delivery; everyone watches the operation
update live. The system models a real shipment lifecycle with a strict
state machine, protects against concurrent dispatch conflicts, and
publishes shipment events asynchronously through RabbitMQ with a
transactional Outbox, consumed today by a real-time dispatch board.

## Architecture

Modular monolith, event-driven internally. One PostgreSQL database
(schema-per-module), RabbitMQ for integration events, background workers
for outbox publishing and consumption. Full write-up: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Stack

**Backend** — C#, .NET 10, ASP.NET Core, EF Core, PostgreSQL (Npgsql),
RabbitMQ, SignalR, Serilog.

**Frontend** — React, TypeScript, Vite, Tailwind CSS, React Query, SignalR
client.

**Testing** — xUnit, Testcontainers (PostgreSQL + RabbitMQ), NetArchTest,
Vitest, React Testing Library.

**Infra** — Docker Compose (local), GitHub Actions (CI).

## Key features

- Explicit shipment/delivery state machine with optimistic concurrency
- Fleet vehicle registry (register/list/filter), server-validated on
  dispatch assignment alongside driver availability
- Transactional Outbox pattern, with a background publisher that reliably
  drains it to RabbitMQ (`FOR UPDATE SKIP LOCKED` claiming, safe across
  multiple app instances, capped exponential retry backoff — never
  hot-looping, never silently dropping a message)
- Idempotent RabbitMQ consumers (inbox pattern) with retry + DLQ — the first
  real consumer (the dispatch board) is built with its own dead-letter
  topology; the shared *idempotent-write* Inbox pattern applies once a
  consumer actually mutates data (this one only broadcasts, so it doesn't
  need it yet — see backend/README.md)
- Concurrency-safe dispatch (driver + vehicle assignment) — optimistic
  concurrency token, driver/vehicle availability re-verified server-side,
  a stale write 409s instead of silently overwriting
- Real-time dispatch board via SignalR, authorized server-side per
  connection (role-derived group membership, never client-supplied)
- Proof of Delivery photo uploads: 5 MB limit, MIME allowlist validated
  against the actual byte signature (not the filename or declared
  Content-Type), immutable once attached, driver-owned with the same
  IDOR-hardened 404 pattern as the rest of Shipments
- RBAC with server-enforced ownership (no client-trusted IDs)

## Local development

```bash
docker compose up -d          # PostgreSQL + RabbitMQ (+ API once built)

cd backend
dotnet restore
dotnet run --project src/FleetDelivery.Api

cd frontend
npm install
npm run dev
```

## Production

| Layer      | Provider   |
|------------|------------|
| Frontend   | Vercel     |
| API        | Render     |
| Database   | Neon (PostgreSQL) |
| Messaging  | CloudAMQP (free shared RabbitMQ plan) |

URLs will be added here once deployed.

## Documentation

- [Architecture](docs/ARCHITECTURE.md)

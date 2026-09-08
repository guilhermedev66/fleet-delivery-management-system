# Fleet & Delivery Management System

A logistics operations platform for dispatching shipments, tracking
deliveries in real time, and managing drivers, vehicles, and routes —
built to demonstrate a real event-driven backend, not another CRUD app.

> Status: **in development (shipment & delivery lifecycle, fleet vehicle
> registry)**. Auth/RBAC, CI, Docker, and the app shell are in place. See
> [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full design.

## Overview

Dispatchers assign shipments to drivers and vehicles; drivers execute
deliveries from a mobile-first workflow and capture Proof of Delivery;
everyone watches the operation update live. The system models a real
shipment lifecycle with a strict state machine, protects against
concurrent dispatch conflicts, and processes side effects (notifications,
tracking, audit) asynchronously through RabbitMQ with a transactional
Outbox and idempotent consumers.

## Architecture

Modular monolith, event-driven internally. One PostgreSQL database
(schema-per-module), RabbitMQ for integration events, background workers
for outbox publishing and consumption. Full write-up: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## Stack

**Backend** — C#, .NET 10, ASP.NET Core, EF Core, PostgreSQL (Npgsql),
RabbitMQ, SignalR, OpenTelemetry, Serilog.

**Frontend** — React, TypeScript, Vite, Tailwind CSS, React Query, SignalR
client.

**Testing** — xUnit, Testcontainers (PostgreSQL + RabbitMQ), NetArchTest,
Vitest, React Testing Library.

**Infra** — Docker Compose (local), GitHub Actions (CI).

## Key features

- Explicit shipment/delivery state machine with optimistic concurrency
- Fleet vehicle registry (register/list/filter), server-validated on
  dispatch assignment alongside driver availability
- Transactional Outbox pattern for reliable event publishing
- Idempotent RabbitMQ consumers (inbox pattern) with retry + DLQ
- Concurrency-safe dispatch (driver + vehicle assignment)
- Real-time dispatch board and tracking via SignalR
- Secure Proof of Delivery uploads
- RBAC with server-enforced ownership (no client-trusted IDs)
- OpenTelemetry tracing across HTTP, database, and messaging

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

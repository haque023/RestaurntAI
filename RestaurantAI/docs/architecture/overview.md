# Architecture Overview

Source documents: *AI-Powered Restaurant Ecosystem — SRS v1.0* and the platform architecture
specification (sections 3–5, 9). This page records how the codebase maps to them.

## High-level architecture

```
                          GLOBAL USERS
                                │
                                ▼
              ┌─────────────────────────────────┐
              │   OPTIONAL EDGE LAYER (later)   │  CDN / WAF / DDoS / DNS / TLS
              └────────────────┬────────────────┘
                               ▼
              ┌─────────────────────────────────┐
              │   YARP GATEWAY  (Gateway.Api)   │  routing, rate limiting, authN,
              └────────────────┬────────────────┘  API policies, correlation
                               ▼
       ┌─────────────────────────────────────────────────────┐
       │      APPLICATION PLATFORM  (ASP.NET Core, src/Api)   │
       │ Identity · Consumer · Restaurant · Menu · Order      │
       │ Reservation · Loyalty · Admin                       │
       └──────────────────────┬──────────────────────────────┘
             ┌────────────────┼─────────────────┐
             ▼                ▼                 ▼
       PostgreSQL           Redis            RabbitMQ
    (system of record)     (cache)          (messaging)
             │
     Outbox messages ──► Outbox Dispatcher ──► RabbitMQ ──► Notifications / Loyalty /
                                                            Analytics / Memory / Audit

       ┌─────────────────────────────────────────────────────┐
       │                AI PLATFORM  (src/AI)                │
       │   Context Orchestrator ─┬─ Memory  (PostgreSQL)     │
       │                         ├─ RAG     (pgvector)       │
       │                         └─ MCP Gateway → Business APIs
       │   Token Budget Manager · Ranking · Eviction         │
       │                 Microsoft Foundry                   │
       │           Consumer AI · Manager AI · Staff AI       │
       └─────────────────────────────────────────────────────┘
```

## Request path

```
Client → [Edge] → Gateway.Api (YARP) → <Module>.Api → Application service → Domain
                                              │
                                              └→ PostgreSQL (+ outbox row, same transaction)
```

AI path (SRS §13): `User → Agent (Foundry) → MCP tool (Mcp.Gateway) → Gateway.Api → Business API → PostgreSQL`.
Agents never touch the database.

## Code map

| Area | Location | Notes |
|---|---|---|
| Edge-ready gateway | `src/Gateway.Api` | YARP config in `appsettings*.json`; no business logic |
| MCP gateway | `src/Mcp.Gateway` | Tools, authorization, idempotency, audit |
| Business APIs | `src/Api/*.Api` | One deployable per module, minimal APIs, own DB schema |
| Use cases | `src/Application/*.Application` | Services + repository abstractions |
| Domain | `src/Domain/*.Domain` | Aggregates, invariants, domain events |
| Shared building blocks | `src/BuildingBlocks` | `SharedKernel` (domain primitives, tenancy contracts), `ServiceDefaults` (telemetry, auth, errors, health) |
| Persistence | `src/Infrastructure/Persistence` | Tenant-aware DbContext, RLS interceptor + migration helpers, Redis |
| Messaging | `src/Infrastructure/Messaging` | Transactional outbox, dispatcher, RabbitMQ publisher |
| Idempotency | `src/Infrastructure/Idempotency` | Durable idempotency keys committed with the business change |
| Vector / storage | `src/Infrastructure/Vector`, `Storage` | pgvector knowledge store, Blob object storage |
| AI | `src/AI/*` | Context orchestration, memory, RAG, Foundry + agents |

`src/BuildingBlocks` is an addition to the original structure: cross-cutting code shared by every
service needed a home that is neither a domain nor an infrastructure adapter.

## Key decisions

- **.NET 10 (LTS)**, central package management, warnings as errors.
- **Database per module schema** on one PostgreSQL cluster (`identity`, `consumer`, `restaurant`,
  `menu`, `orders`, `reservations`, `loyalty`, `knowledge`, `ai_memory`). Each module owns its
  migrations and outbox table; modules integrate through APIs and events, never shared tables.
- **Transactional outbox** for all domain events (at-least-once; consumers must be idempotent).
- **Idempotency in two layers**: MCP Gateway (Redis, dedupes model retries per turn) and business
  APIs (PostgreSQL, `Idempotency-Key` committed atomically with the change).
- **Tenant isolation in four layers**: token claims → gateway/API authorization → application
  checks + EF query filters → PostgreSQL Row-Level Security.
- **Claude via the Anthropic SDK** behind `Microsoft.Extensions.AI.IChatClient`
  (`Foundry.Integration`); the same SDK supports Claude on Microsoft Foundry if the platform
  moves there (`AnthropicFoundryClient`).
- **AI is not the system of record**: price, availability, order/reservation/loyalty state only come
  from business services; RAG content for the same entity is evicted when live data is present.
- **Edge-ready from day 1**: forwarded headers honoured, no edge-specific code in services.

## Not yet implemented

- RabbitMQ consumers / background workers (Notifications, Loyalty, Analytics, Memory, Audit).
- Payments, Kitchen/KDS, Tables & QR, CRM, Promotions, Subscriptions modules.
- Production identity provider integration (Entra ID / External ID) — dev tokens only today.
- Embeddings for RAG: Claude has no embeddings API, so retrieval is off (`NoKnowledgeRetriever`)
  until an embedding provider is configured; the waiter answers from live MCP tool data.
- Streaming AI replies (turns currently return when complete, typically 5–10 s).
- CI/CD pipeline and infrastructure-as-code.

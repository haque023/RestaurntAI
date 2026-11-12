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
       │      APPLICATION PLATFORM  (ASP.NET Core, src/Services)│
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
       │                AI PLATFORM  (src/Services/Ai.Api)  │
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
| Edge-ready gateway | `src/Gateways/Gateway.Api` | YARP config in `appsettings*.json`; no business logic |
| MCP gateway | `src/Gateways/Mcp.Gateway` | Tools, authorization, idempotency, audit |
| Business APIs | `src/Services/*.Api` | One deployable per module, own DB schema |
| HTTP layer | `Services/*.Api/Controllers` | Routes, permissions, request binding; no logic |
| Business logic | `Services/*.Api/Services`, `IServices` | Services behind interfaces; `*Workflow` classes hold domain rules |
| Domain and contracts | `Services/*.Api/Models` | Aggregates, invariants, domain events, requests and responses |
| Data | `Services/*.Api/Data` | DbContext, repositories, migrations |
| Shared building blocks | `src/Shared` | `SharedKernel` (domain primitives, tenancy contracts), `ServiceDefaults` (telemetry, auth, errors, health, controllers) |
| Persistence | `src/Shared/Infrastructure/Persistence` | Tenant-aware DbContext, RLS interceptor + migration helpers, Redis |
| Messaging | `src/Shared/Infrastructure/Messaging` | Transactional outbox, dispatcher, RabbitMQ publisher |
| Idempotency | `src/Shared/Infrastructure/Idempotency` | Durable idempotency keys committed with the business change |
| Vector / storage | `src/Shared/Infrastructure/Vector`, `Storage` | pgvector knowledge store, Blob object storage |
| AI | `src/Services/Ai.Api` | Context orchestration, memory, RAG, Foundry + agents |

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

- **Local accounts, role-derived permissions**: Identity issues JWTs whose permissions come from
  a server-side role map (`PlatformRolePermissions`); clients never choose permissions. Guests who
  scan a table QR get a short-lived guest identity bound to restaurant, branch and table.
- **QR codes are opaque random tokens** resolved through a public read model, so tables cannot be
  enumerated and a code can be rotated without reprinting the others.
- **Audit log from the event stream**: every domain event (with acting user and tenant) and every
  MCP tool execution flows through RabbitMQ into `platform.audit_events` (Admin.Api, idempotent consumer).
- **Two agents**: the restaurant AI waiter (tenant-scoped tools, staff hand-over via `request_staff`)
  and the platform discovery assistant (`search_restaurants`, `search_dishes` over listed restaurants,
  location-aware); discovery chat history lives in Redis because it belongs to no tenant.

## Not yet implemented

- Payments (provider integration, refunds, reconciliation) and subscription billing/invoices;
  plans are assigned by administrators today.
- POS/KDS integrations, notifications (email/SMS/push), CRM segments, promotions and campaigns.
- Menu variants and add-ons, menu images (Blob storage), multilingual menu content.
- External identity provider (Entra ID / External ID), refresh tokens and password reset;
  disabling a user blocks new sign-ins but existing tokens stay valid until they expire.
- Embeddings for RAG: Claude has no embeddings API, so retrieval is off (`NoKnowledgeRetriever`);
  the waiter uses live MCP tool data plus the owner's AI knowledge text.
- Streaming AI replies (turns return when complete, typically 5–10 s).
- Restaurant mobile app and offline mode; CI/CD pipeline and infrastructure-as-code.

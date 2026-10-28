# RestaurantAI

Multi-tenant, AI-enabled restaurant platform: ASP.NET Core business services behind a YARP gateway,
PostgreSQL as the system of record, Redis, RabbitMQ with a transactional outbox, and an AI platform
(Microsoft Foundry) that reaches business capabilities only through an MCP gateway.

```
Clients → [Edge, optional] → Gateway.Api (YARP) → Business APIs → PostgreSQL / Redis / RabbitMQ
AI agents (Foundry) → Mcp.Gateway (tools) → Gateway.Api → Business APIs
```

## Quick start

```bash
docker compose up -d          # PostgreSQL+pgvector, Redis, RabbitMQ, Azurite
dotnet build RestaurantAI.sln
dotnet test RestaurantAI.sln
dotnet run --project src/Gateway.Api
```

Requires the .NET 10 SDK (`global.json`). See [docs/operations](docs/operations/README.md) for
running services, getting a development token and ports.

## Layout

```
src/
  Gateway.Api/        YARP API gateway (routing, auth, rate limiting, correlation)
  Mcp.Gateway/        MCP server: AI tools, authorization, idempotency, audit
  Api/                One ASP.NET Core service per module (minimal APIs, own DB schema)
  Application/        Use cases per module
  Domain/             Aggregates and domain events per module
  Infrastructure/     Persistence (PostgreSQL+RLS, Redis), Messaging (RabbitMQ, Outbox),
                      Idempotency, Vector (pgvector), Storage (Blob)
  AI/                 Context Orchestrator, Memory, RAG, Foundry integration + agents
  BuildingBlocks/     SharedKernel + ServiceDefaults shared by all services
tests/                Unit tests per area + Testcontainers integration tests
deploy/               Dockerfiles, full-stack compose, PostgreSQL init, nginx (optional edge)
docs/                 architecture, api, mcp, security, operations
```

## Documentation

- [MVP 1 scope and demo accounts](docs/mvp1.md)
- [Architecture](docs/architecture/overview.md)
- [API](docs/api/README.md)
- [MCP Gateway](docs/mcp/README.md)
- [Security & tenant isolation](docs/security/README.md)
- [Operations](docs/operations/README.md)

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
dotnet run --project src/Gateways/Gateway.Api
```

Requires the .NET 10 SDK (`global.json`). See [docs/operations](docs/operations/README.md) for
running services, getting a development token and ports.

## Layout

```
src/
  Services/           One ASP.NET Core service per module, each with its own DB schema
    Order.Api/
      Controllers/    HTTP only: routes, permissions, request binding
      IServices/      Interfaces the controllers (and tests) depend on
      Services/       Business logic
      Models/         Entities, requests and responses
      Data/           DbContext, repositories, EF migrations
      Clients/        Calls to other services (where a service needs them)
    Identity.Api/  Restaurant.Api/  Menu.Api/  Reservation.Api/
    Consumer.Api/  Loyalty.Api/  Admin.Api/
    Ai.Api/           Same folders, plus Context/, Foundry/, Memory/ and Rag/ for the AI engine
  Gateways/
    Gateway.Api/      YARP API gateway (routing, auth, rate limiting, correlation)
    Mcp.Gateway/      MCP server: AI tools, authorization, idempotency, audit
  Shared/
    SharedKernel/     Domain primitives and tenancy contracts
    ServiceDefaults/  Telemetry, auth, errors, health, controller setup
    Infrastructure/   Persistence (PostgreSQL+RLS, Redis), Messaging (RabbitMQ, Outbox),
                      Idempotency, Vector (pgvector), Storage (Blob)
tests/                Unit tests per area + Testcontainers integration tests
deploy/               Dockerfiles, full-stack compose, PostgreSQL init, nginx (optional edge)
docs/                 architecture, api, mcp, security, operations
```

A request flows `Controller → IService → Service → Data`. In Order, Menu and Reservation the
`*Workflow` class in `Services/` holds the domain rules behind the service.

## Documentation

- [MVP 1 scope and demo accounts](docs/mvp1.md)
- [Architecture](docs/architecture/overview.md)
- [API](docs/api/README.md)
- [MCP Gateway](docs/mcp/README.md)
- [Security & tenant isolation](docs/security/README.md)
- [Operations](docs/operations/README.md)

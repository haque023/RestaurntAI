# Operations

## Local development

Prerequisites: .NET SDK 10.0.1xx or later, Docker.

```bash
docker compose up -d                       # PostgreSQL (pgvector), Redis, RabbitMQ, Azurite
dotnet build RestaurantAI.sln
dotnet run --project src/Api/Menu.Api      # repeat for the services you need
dotnet run --project src/Api/Order.Api
dotnet run --project src/Api/Identity.Api
dotnet run --project src/Gateway.Api       # http://localhost:5080
dotnet run --project src/Mcp.Gateway       # http://localhost:5100/mcp
```

In Development each service applies its own migrations on startup.

### Getting a token

```bash
curl -s localhost:5080/api/auth/dev-token -H 'content-type: application/json' -d '{
  "tenantId": "11111111-1111-1111-1111-111111111111",
  "roles": ["restaurant_manager"],
  "permissions": ["menu.read","menu.write","orders.create","orders.read","orders.cancel","orders.manage"]
}'
```

## AI waiter (Claude)

The AI service (`src/AI/Ai.Api`, port 5300, gateway route `/api/ai/*`) calls Claude through the
official Anthropic C# SDK. Store the API key with user-secrets (kept outside the repository):

```bash
dotnet user-secrets set "AI:Claude:ApiKey" "<your key>" --project src/AI/Ai.Api
```

or export `ANTHROPIC_API_KEY`. Settings live under `AI:Claude` (model `claude-opus-5-5`, effort
`medium`, server-side refusal fallback on, MCP endpoint). Never put the key in `appsettings*.json`.

## Ports

| Service | Port |
|---|---|
| Gateway.Api | 5080 |
| Mcp.Gateway | 5100 |
| Identity / Consumer / Restaurant / Menu / Order / Reservation / Loyalty / Admin | 5201–5208 |
| Ai.Api (AI waiter) | 5300 |
| PostgreSQL / Redis / RabbitMQ / RabbitMQ UI / Azurite | 5432 / 6379 / 5672 / 15672 / 10000 |

## Migrations

Each module owns its migrations under `src/Api/<Module>.Api/Persistence/Migrations`.

```bash
dotnet tool install --global dotnet-ef
dotnet ef migrations add <Name> --project src/Api/Order.Api --context OrderDbContext --output-dir Persistence/Migrations
```

Production: apply migrations from the pipeline (e.g. `dotnet ef migrations bundle`), not on startup.
For the containerized stack, `Database__MigrateOnStartup=true` enables startup migration.

## Full stack in containers

```bash
JWT_SIGNING_KEY=$(openssl rand -hex 32) docker compose -f deploy/docker-compose.yml up --build
```

Gateway on `:8080`, MCP Gateway on `:8090`.

## Observability

All services emit OpenTelemetry logs, metrics and traces. Set `OTEL_EXPORTER_OTLP_ENDPOINT` to
export to a collector (Azure Monitor, Grafana, Jaeger…). Correlation ids flow through the gateway,
services, MCP tools and outbox messages (`X-Correlation-ID`).

## Tests

```bash
dotnet test RestaurantAI.sln
```

`tests/Integration.Tests` needs Docker (Testcontainers PostgreSQL); the tests are skipped when
Docker is unavailable.

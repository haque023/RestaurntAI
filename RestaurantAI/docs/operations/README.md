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

### Signing in

Every app signs in with email and password (Identity service, PBKDF2 hashes, lockout after 5
failed attempts). In Development a platform administrator is created on first start from
`Identity:BootstrapAdmin` (`appsettings.Development.json`): `admin@restaurant-ai.local` /
`Admin#2026dev`. Outside Development set `Identity__BootstrapAdmin__Email/Password` from a secret
store, or leave them empty.

- Customers register in the guest app; guests who scan a table QR get a temporary guest identity
  (6 h) and can order and use the AI waiter without an account.
- Restaurants sign up in the restaurant console (`/signup`) and wait for verification, or an
  administrator onboards them (Admin → Restaurants → Onboard).
- Owners and managers add staff (role + optional branch) under Staff.

Test scripts can still mint tokens with `POST /api/auth/dev-token` (Development only).

## AI waiter (Claude)

The AI service (`src/AI/Ai.Api`, port 5300, gateway route `/api/ai/*`) calls Claude through the
official Anthropic C# SDK. Store the API key with user-secrets (kept outside the repository):

```bash
dotnet user-secrets set "AI:Claude:ApiKey" "<your key>" --project src/AI/Ai.Api
```

or export `ANTHROPIC_API_KEY`. Settings live under `AI:Claude` (model `claude-opus-5-5`, effort
`medium`, server-side refusal fallback on, MCP endpoint). Never put the key in `appsettings*.json`.

Cost controls: each restaurant's plan caps AI waiter turns per month (Starter 300, Growth 3,000,
Pro 30,000); the discovery assistant is capped per user per day (`AI:Usage:DiscoveryTurnsPerUserPerDay`,
default 60); the gateway limits AI calls to 20/minute per user. Administrators can switch AI off per
restaurant. Every turn is metered in `ai_memory.ai_usage` (tokens, tool calls, latency, outcome).

## Ports

| Service | Port |
|---|---|
| Gateway.Api | 5080 |
| Mcp.Gateway | 5100 |
| Identity / Consumer / Restaurant / Menu / Order / Reservation / Loyalty / Admin | 5201–5208 |
| Ai.Api (AI waiter, discovery assistant) | 5300 |
| Guest app / Restaurant console / Platform admin (Vite) | 3000 / 3001 / 3002 |
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
JWT_SIGNING_KEY=$(openssl rand -hex 32) ANTHROPIC_API_KEY=<key> \
  ADMIN_EMAIL=<email> ADMIN_PASSWORD=<password> docker compose -f deploy/docker-compose.yml up --build
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

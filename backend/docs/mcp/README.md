# MCP Gateway

`src/Gateways/Mcp.Gateway` exposes business capabilities to AI agents over the Model Context Protocol
(streamable HTTP, stateless) at `/mcp`. It is the only path from agents to business systems.

## Tools

| Tool | Permission | Mutates | Branch required | Business API |
|---|---|---|---|---|
| `get_menu` | `menu.read` | no | yes | `GET /api/menu/branches/{id}/items` |
| `search_menu` | `menu.read` | no | yes | `GET /api/menu/branches/{id}/items/search` |
| `get_menu_item_availability` | `menu.read` | no | no | `GET /api/menu/items/{id}/availability` |
| `create_order` | `orders.create` | yes | yes | `POST /api/orders` |
| `get_order_status` | `orders.read` | no | no | `GET /api/orders/{id}` |
| `cancel_order` | `orders.cancel` | yes | no | `POST /api/orders/{id}/cancel` |
| `create_reservation` | `reservations.create` | yes | yes | `POST /api/reservations` |
| `get_reservation` | `reservations.read` | no | no | `GET /api/reservations/{id}` |
| `cancel_reservation` | `reservations.cancel` | yes | no | `POST /api/reservations/{id}/cancel` |

Branch can be omitted when the session token carries `branch_id` (e.g. a QR table session).

## Per-call pipeline

1. **Authenticate** — the agent forwards the *end user's* bearer token (`/mcp` requires authorization).
2. **Resolve context** — `TenantContextResolver` builds User/Restaurant/Branch context from claims only.
3. **Authorize** — `ToolPermissionService` checks permission, branch requirement and branch binding.
4. **Idempotency** (mutating tools) — `IdempotencyKeyGenerator` derives a key from
   tenant + user + tool + `X-Conversation-Id` + `X-Turn-Id` + arguments; `IdempotencyService`
   replays cached results (Redis) and forwards the key as `Idempotency-Key`.
5. **Execute** — `BusinessApiClient` calls the YARP gateway; the business API re-authorizes.
6. **Audit** — `McpAuditService` logs tool, user, tenant, branch, outcome, duration, correlation id.

## Adding a tool

1. Add a name to `McpToolNames` and a `ToolDefinition` in `ToolPermissionService`.
2. Create a `[McpServerToolType]` class under `Tools/<Area>/` following an existing tool.
3. Register it in `Program.cs` with `.WithTools<T>()`.
4. Allow it for the relevant agents in `src/Services/Ai.Api/Foundry/Agents/AgentProfile.cs`.

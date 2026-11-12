# Security

## Tenant isolation (defense in depth)

| Layer | Mechanism | Code |
|---|---|---|
| 1. Identity | Tokens carry `sub`, `tenant_id`, `branch_id`, `role`, `permission` | `SharedKernel/Tenancy/PlatformClaims.cs` |
| 2. API | Gateway validates tokens + route policies; each service validates again; `RequirePermission` filter | `Gateway.Api/Configuration`, `ServiceDefaults/Authorization` |
| 3. Application | Branch/ownership checks in services; EF global query filter on `ITenantOwned` | `Services/*.Api/Services`, `TenantAwareDbContext` |
| 4. Database | PostgreSQL RLS policies (`app.tenant_id` session setting) with `FORCE ROW LEVEL SECURITY` | `TenantSessionInterceptor`, `RowLevelSecurityMigrationExtensions`, module migrations |

RLS is enforced only for non-superuser roles. Applications connect as `restaurant_app`
(`deploy/postgres/01-init.sql`); never run services as `postgres`.

Tables with RLS: `orders.orders`, `orders.idempotency_entries`, `menu.menu_items`,
`reservations.reservations`, `reservations.idempotency_entries`, `loyalty.loyalty_accounts`,
`restaurant.branches`. Background jobs that must cross tenants (e.g. idempotency cleanup) need an
explicit, audited bypass (`app.bypass_rls`), set only for platform-administrator contexts today.

## Public read models (discovery)

Discovery is anonymous and cross-tenant by design, so it never reads tenant tables. Two read
models hold only public data, have **no RLS**, and are written in the same transaction as the
source change:

| Table | Written by | Contents |
|---|---|---|
| `restaurant.discovery_listings` | Restaurant.Api on every restaurant/branch change | Listed restaurants only: name, description, cuisines, diets, branch address, coordinates, hours |
| `menu.public_menu_items` | `MenuDbContext.SaveChangesAsync` | Dish name, category, description, tags, price, availability |

Known gap: `public_menu_items` also contains dishes of restaurants that are not yet listed.
The web app hides them by joining with listings; filter server-side (e.g. from
`RestaurantActivated` events) before relying on the dish endpoint elsewhere.

Favorites (`consumer.favorite_restaurants`) are keyed by the authenticated user id only.

## Authentication

- Services accept JWTs validated against `Authentication:Jwt` (`Authority` for an OIDC provider,
  or `SigningKey` for local development).
- `POST /api/auth/dev-token` exists **only in Development**. Production must use the central identity
  provider; never configure `SigningKey` in production.
- The gateway strips platform-reserved headers (`X-Tenant-Id`, `X-Branch-Id`, `X-User-Id`).

## AI security

- Agents act with the end user's token; AI cannot exceed the user's permissions.
- Tool allow-lists per agent (`AgentCatalog`) plus per-call authorization in the MCP Gateway.
- Every tool execution and denial is audited.
- Tenant-scoped retrieval (`IVectorStore.SearchAsync` requires a tenant id).
- Live data overrides retrieved knowledge in context assembly.

## Secrets

Development defaults in `appsettings*.json` are for local use only. Use Azure Key Vault / managed
identity in deployed environments (`Foundry:ApiKey` empty → `DefaultAzureCredential`).

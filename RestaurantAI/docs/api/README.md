# API

All routes are exposed through the YARP gateway (`http://localhost:5080` locally). Every service
also serves OpenAPI at `/openapi/v1.json` in Development, plus `/health/live` and `/health/ready`.

| Gateway prefix | Service | Local port | Gateway policy |
|---|---|---|---|
| `/api/auth/*` | Identity.Api | 5201 | anonymous (service enforces per endpoint) |
| `/api/consumer/*` | Consumer.Api | 5202 | authenticated |
| `/api/restaurants/*` | Restaurant.Api | 5203 | authenticated |
| `/api/menu/*` | Menu.Api | 5204 | authenticated |
| `/api/orders/*` | Order.Api | 5205 | authenticated |
| `/api/reservations/*` | Reservation.Api | 5206 | authenticated |
| `/api/loyalty/*` | Loyalty.Api | 5207 | authenticated |
| `/api/admin/*` | Admin.Api | 5208 | platform-admin |
| `/api/discovery/restaurants/*` | Restaurant.Api | 5203 | anonymous (public) |
| `/api/discovery/menu-items/*` | Menu.Api | 5204 | anonymous (public) |

All routes use the `per-tenant` token-bucket rate limiter.

## Endpoints

| Method | Path | Permission |
|---|---|---|
| POST | `/api/auth/dev-token` | Development only |
| GET | `/api/auth/me` | authenticated |
| GET/PUT | `/api/consumer/profile` | `consumer.profile` |
| POST | `/api/restaurants` | role `platform_admin` |
| GET | `/api/restaurants/current` | `restaurant.manage` |
| PUT | `/api/restaurants/current/profile` (description, cuisines, dietary options) | `restaurant.manage` |
| PUT | `/api/restaurants/current/listing` (`{ "listed": true }`) | `restaurant.manage` |
| POST | `/api/restaurants/current/branches` | `restaurant.manage` |
| PUT | `/api/restaurants/current/branches/{branchId}` (address, lat/lng, opening hours) | `restaurant.manage` |
| GET | `/api/discovery/restaurants?q=&cuisines=&diets=&lat=&lng=&radiusKm=&openNow=&branchIds=&tenantIds=&limit=` | public |
| GET | `/api/discovery/restaurants/cuisines` | public |
| GET | `/api/discovery/menu-items?q=&diets=&maxPrice=&limit=` | public |
| GET | `/api/consumer/favorites` | `consumer.profile` |
| PUT/DELETE | `/api/consumer/favorites/{restaurantId}` | `consumer.profile` |
| GET | `/api/menu/branches/{branchId}/items` | `menu.read` |
| GET | `/api/menu/branches/{branchId}/items/search?q=&maxPrice=&dietary=` | `menu.read` |
| GET | `/api/menu/branches/{branchId}/items/batch?ids=` | `menu.read` |
| GET | `/api/menu/items/{itemId}/availability` | `menu.read` |
| POST | `/api/menu/branches/{branchId}/items` | `menu.write` |
| PUT | `/api/menu/items/{itemId}/availability` | `menu.write` |
| POST | `/api/orders` (requires `Idempotency-Key`) | `orders.create` |
| GET | `/api/orders/{orderId}` | `orders.read` |
| POST | `/api/orders/{orderId}/cancel` | `orders.cancel` |
| POST | `/api/reservations` (requires `Idempotency-Key`) | `reservations.create` |
| GET | `/api/reservations/{reservationId}` | `reservations.read` |
| POST | `/api/reservations/{reservationId}/cancel` | `reservations.cancel` |
| GET | `/api/loyalty/balance` | `loyalty.read` |
| GET | `/api/admin/overview` | role `platform_admin` |

## Conventions

- **Errors**: RFC 9457 problem details with `title` = error code and a `correlationId` extension.
  Business-rule violations return 422, conflicts 409, missing permissions 403.
- **Correlation**: `X-Correlation-ID` is assigned by the gateway, forwarded upstream and echoed back.
- **Idempotency**: `Idempotency-Key` (≤128 chars). A retry with the same key and body replays the
  original response with `Idempotent-Replayed: true`; same key with a different body → 422.
- **Tenant context** comes only from token claims (`tenant_id`, `branch_id`), never from headers.

## Discovery

- Only **listed** restaurants (status `Active`, set from the console) appear. Results are one row per branch.
- `lat`/`lng` + `radiusKm` (default 10, max 100) filter by distance and sort nearest first.
- `openNow=true` evaluates each branch's weekly hours in the restaurant's own IANA time zone;
  periods whose closing time is earlier than the opening time run past midnight.
- Dish search returns available dishes with their `branchId`; join with
  `/api/discovery/restaurants?branchIds=…` to keep only listed branches (the web app does this).

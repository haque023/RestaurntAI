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
| `/api/admin/*` (overview, audit) | Admin.Api | 5208 | platform-admin |
| `/api/admin/restaurants/*` | Restaurant.Api | 5203 | platform-admin |
| `/api/admin/identity/*` | Identity.Api | 5201 | platform-admin |
| `/api/admin/ai/*` | Ai.Api | 5300 | platform-admin |
| `/api/service-requests/*` | Order.Api | 5205 | authenticated |
| `/api/ai/*` | Ai.Api | 5300 | authenticated |
| `/api/discovery/restaurants/*` | Restaurant.Api | 5203 | anonymous (public) |
| `/api/discovery/menu-items/*` | Menu.Api | 5204 | anonymous (public) |

All routes use the `per-tenant` token-bucket rate limiter, except `/api/ai/*`, which uses
`ai-per-user` (20 requests/minute per user, `RateLimiting:AiPermitsPerUserPerMinute`).

## Endpoints

| Method | Path | Permission |
|---|---|---|
| POST | `/api/auth/register` (customer account) | anonymous |
| POST | `/api/auth/login` (`scope`: `Customer`, `Staff`, `Admin`; optional `tenantId`) | anonymous |
| POST | `/api/auth/signup/restaurant` (owner account + restaurant awaiting verification) | anonymous |
| POST | `/api/auth/venue-session` (`qrToken`, or `tenantId` + `branchId`; anonymous callers become guests) | anonymous |
| POST | `/api/auth/switch` (staff with several restaurants) | authenticated |
| GET | `/api/auth/memberships` | authenticated |
| GET/POST | `/api/auth/staff`, PUT/DELETE `/api/auth/staff/{userId}`, GET `/api/auth/staff/roles` | `staff.manage` |
| POST | `/api/auth/dev-token` | Development only (test scripts) |
| GET | `/api/auth/me` | authenticated |
| GET/PUT | `/api/consumer/profile` | `consumer.profile` |
| POST | `/api/restaurants` | role `platform_admin` |
| GET | `/api/restaurants/current` | `restaurant.manage` |
| GET | `/api/restaurants/current/summary` (name, status, the caller's branches) | `menu.read` |
| GET | `/api/restaurants/plans` | authenticated |
| GET/PUT | `/api/restaurants/current/ai-settings` (knowledge, tone) | `ai.manage` |
| GET | `/api/restaurants/current/ai-context` (used by the AI platform) | authenticated, tenant |
| GET | `/api/restaurants/current/branches/{branchId}/tables` | `orders.read` |
| POST | `/api/restaurants/current/branches/{branchId}/tables` | `restaurant.manage` |
| PUT/DELETE | `/api/restaurants/current/tables/{tableId}`, POST `…/rotate-qr` | `restaurant.manage` |
| PUT | `/api/restaurants/current/tables/{tableId}/status` | `orders.manage` |
| PUT | `/api/restaurants/current/profile` (description, cuisines, dietary options) | `restaurant.manage` |
| PUT | `/api/restaurants/current/listing` (`{ "listed": true }`) | `restaurant.manage` |
| POST | `/api/restaurants/current/branches` | `restaurant.manage` |
| PUT | `/api/restaurants/current/branches/{branchId}` (address, lat/lng, opening hours) | `restaurant.manage` |
| GET | `/api/discovery/restaurants?q=&cuisines=&diets=&lat=&lng=&radiusKm=&openNow=&branchIds=&tenantIds=&limit=` | public |
| GET | `/api/discovery/restaurants/cuisines` | public |
| GET | `/api/discovery/restaurants/tenants/{tenantId}` (listed branches) | public |
| GET | `/api/discovery/restaurants/qr/{token}` (table QR → venue) | public |
| GET | `/api/discovery/menu-items?q=&diets=&maxPrice=&branchIds=&limit=` | public |
| GET | `/api/consumer/favorites` | `consumer.profile` |
| PUT/DELETE | `/api/consumer/favorites/{restaurantId}` | `consumer.profile` |
| GET | `/api/menu/branches/{branchId}/items` | `menu.read` |
| GET | `/api/menu/branches/{branchId}/items/search?q=&maxPrice=&dietary=` | `menu.read` |
| GET | `/api/menu/branches/{branchId}/items/batch?ids=` | `menu.read` |
| GET | `/api/menu/items/{itemId}/availability` | `menu.read` |
| POST | `/api/menu/branches/{branchId}/items` | `menu.write` |
| PUT | `/api/menu/items/{itemId}/availability` | `menu.write` |
| PUT/DELETE | `/api/menu/items/{itemId}` | `menu.write` |
| POST | `/api/orders` (requires `Idempotency-Key`) | `orders.create` |
| GET | `/api/orders/{orderId}` | `orders.read` |
| POST | `/api/orders/{orderId}/cancel` | `orders.cancel` |
| GET | `/api/orders?branchId=&status=Placed,Accepted&since=&take=` (order board) | `orders.manage` |
| POST | `/api/orders/{orderId}/status` (`Accepted`…`Completed`, `Rejected` with reason) | `orders.manage` |
| GET | `/api/orders/mine` | `orders.read` |
| GET | `/api/orders/stats?from=&to=&branchId=` | `analytics.read` |
| POST | `/api/service-requests` (`CallWaiter`, `RequestBill`, `Assistance`, `AiEscalation`) | `service.request` |
| GET | `/api/service-requests/mine` | `service.request` |
| GET | `/api/service-requests?branchId=`, POST `/{id}/acknowledge`, `/{id}/resolve` | `orders.manage` |
| POST | `/api/reservations` (requires `Idempotency-Key`) | `reservations.create` |
| GET | `/api/reservations/{reservationId}` | `reservations.read` |
| POST | `/api/reservations/{reservationId}/cancel` | `reservations.cancel` |
| GET | `/api/reservations?branchId=&from=&to=` | `reservations.manage` |
| GET | `/api/reservations/mine` | `reservations.read` |
| POST | `/api/reservations/{id}/seat`, `/complete`, `/no-show` | `reservations.manage` |
| GET | `/api/loyalty/balance` | `loyalty.read` |
| POST | `/api/ai/waiter/turns` (restaurant AI waiter) | `menu.read`, tenant |
| POST | `/api/ai/discovery/turns` (cross-restaurant discovery assistant; `latitude`/`longitude` optional) | `discovery.search` |
| GET | `/api/ai/usage/current` | `analytics.read` |
| GET | `/api/admin/overview`, `/api/admin/audit?type=&module=&tenantId=&actorId=&from=&to=` | role `platform_admin` |
| GET | `/api/admin/restaurants?status=&search=`, `/stats`, `/{id}` | role `platform_admin` |
| POST | `/api/admin/restaurants/{id}/approve`, `/suspend`, `/reinstate` | role `platform_admin` |
| PUT | `/api/admin/restaurants/{id}/plan`, `/ai` | role `platform_admin` |
| GET | `/api/admin/identity/users`, `/tenants/{id}/members`; POST `/owners`, `/users/{id}/activate|deactivate` | role `platform_admin` |
| GET | `/api/admin/ai/usage`, `/api/admin/ai/agents` | role `platform_admin` |

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

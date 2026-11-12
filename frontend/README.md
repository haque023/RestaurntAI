# RestaurantAI Web

React frontends for the RestaurantAI platform. The backend lives in `../backend`.

| App | Port | Who | What |
|---|---|---|---|
| `apps/consumer-web` | 3000 | Guests | **Discover** restaurants (name/cuisine, near me, cuisine & diet filters, open now) and **dishes**; save favorites (*My restaurants*); QR menu, cart, ordering, live order tracking, reservations, loyalty, profile |
| `apps/restaurant-web` | 3001 | Owners, managers, staff | Discovery profile & listing, branches with map location and opening hours, menu management, order and reservation lookup/cancel |
| `apps/admin-web` | 3002 | Platform admins | Platform overview, register restaurants (tenants) |

Shared packages (consumed as TypeScript source, no build step):

| Package | Purpose |
|---|---|
| `packages/api-client` | Typed client for every gateway route; problem details → `ApiError` with correlation id; `Idempotency-Key` support |
| `packages/auth` | Token session per tab, claims (`tenant_id`, `branch_id`, roles, permissions), `useApi`, `RequireAuth`, development sign-in |
| `packages/ui` | Design tokens (light/dark), base styles, `AppShell`, buttons, cards, fields, badges, error alert, formatters |

Each app has one folder per navigation menu:

```
apps/restaurant-web/src/
  menus/      dashboard/ orders/ tables/ reservations/ menu/ staff/ ai-waiter/ restaurant/
  auth/       Sign-in and sign-up pages
  shared/     Layout and helpers used by several menus
  main.tsx    router.tsx
```

`admin-web` has `overview/ restaurants/ ai-governance/ audit-log/ users/`; `consumer-web` has
`discover/ my-restaurants/ current-menu/ cart/ my-orders/ account/`. To add a menu, add a folder
under `menus/`, a route in `router.tsx` and an entry in the layout.

Stack: React 19, Vite 8, TypeScript 6, React Router 8, TanStack Query 5, Vitest 5, ESLint 10 (React Compiler rules).

## Run

```bash
npm install
npm run dev:consumer      # http://localhost:3000
npm run dev:restaurant    # http://localhost:3001
npm run dev:admin         # http://localhost:3002
```

In development every app proxies `/api` to the YARP gateway (`http://localhost:5080`, override with
`VITE_GATEWAY_URL` in `apps/<app>/.env.local`), so the browser sees a single origin and the backend
needs no CORS. Start the backend first (see `../backend/docs/operations`): Gateway, Identity,
plus the services you use (Restaurant, Menu, Order, Reservation, Consumer, Loyalty, Admin).

## End-to-end walkthrough

1. **Admin** (3002): sign in → *Restaurants* → onboard a restaurant and its owner, or approve a self sign-up.
2. **Console** (3001): sign in as the owner → add a branch → *Edit* it to set location and
   opening hours → fill in the *Discovery profile* → *Menu* → add items → *List in discovery*.
3. **Guest** (3000): *Discover* shows the restaurant (try *Near me*, *Open now*, cuisine/diet chips,
   or the *Dishes* tab) → ♥ to save it to *My restaurants* → *View menu* → add items →
   *Place order* → watch the order status update.

Discovery needs no sign-in. Saving favorites and ordering do; guests sign in once and the app
switches their session to a restaurant automatically when they open its menu.

Sign-in uses email and password through the Identity service; demo accounts are listed in
`../backend/docs/mvp1.md`.

## Scripts

```bash
npm run typecheck   # all workspaces
npm run lint
npm test            # Vitest (jsdom)
npm run build       # typecheck + production build of every app
```

## Conventions

- **Server state** via TanStack Query; the browser never computes authoritative prices or status.
  The cart total is labelled an estimate; the order service prices from the menu.
- **Idempotency**: placing an order or reservation creates an `Idempotency-Key` once per confirmation
  and reuses it on retry, so a lost response can't create duplicates (`cart.checkoutKey`).
- **Authorization** checks in the UI are for UX only; every API enforces them again.
- **Errors** show the backend message and the correlation id (`Reference: …`) for support.
- **Tenancy**: QR links carry the restaurant and branch; the guest's token must be for the same tenant.

## Known gaps

- Discovery has no map view, paging or relevance ranking yet; text search is a simple contains match.
- Payments, kitchen display integrations, promotions and notifications are not built yet.

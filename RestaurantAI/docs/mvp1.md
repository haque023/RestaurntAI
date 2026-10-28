# MVP 1 scope

MVP 1 = multi-tenant restaurant management, a platform admin panel, customers finding restaurants
and dishes by location through AI, and the AI waiter. Mapped to the BRS functional requirements
(FR-xxx, BRS v2.0 §23):

| BRS | Requirement | MVP 1 |
|---|---|---|
| FR-001 | Restaurant registration, subscription, onboarding | ✅ Self sign-up (pending verification) or admin onboarding; plans assigned by admin (no billing) |
| FR-002 | Multi-branch management | ✅ Branches with address, map location, hours; branch-bound staff; plan branch limits |
| FR-003 | Profile and business hours | ✅ Description, cuisines, dietary options, phone, website, hours per branch |
| FR-004 | Menu / category / item / variant / add-on | ◐ Categories, items, prices, dietary tags, ingredients, availability, edit/delete. Variants and add-ons: later |
| FR-005 | Tables and QR generation | ✅ Tables with capacity and status, unguessable QR tokens, printable QR sheet, rotate code |
| FR-006 | QR-based restaurant AI session | ✅ Scan → guest session bound to restaurant/branch/table, no account needed |
| FR-007 | Multilingual conversation | ✅ The AI replies in the guest's language (menu content itself is single-language) |
| FR-008 | AI discovery and recommendations | ✅ Discovery assistant across all listed restaurants: dish, cuisine, diet, budget, distance, open now |
| FR-009 | Conversational cart | ◐ The waiter builds and confirms orders in conversation; no shared persistent cart object |
| FR-010 | Order submission and tracking | ✅ Live status for guests; idempotent submission |
| FR-011 | Kitchen workflow / KDS | ◐ Order board: Placed → Accepted → Preparing → Ready → Served → Completed, reject with reason, prep-time KPI. KDS hardware sync: later |
| FR-012 | Reservations | ✅ Conflict-free booking (DB constraint), day book, seat / complete / no-show / cancel |
| FR-013/014 | Payments, refunds, reconciliation | ❌ Later |
| FR-015 | Customer profiles, consent, CRM | ◐ Accounts, profile, dietary preferences, marketing consent, favorites; no CRM segments |
| FR-016 | Loyalty | ◐ Balance API (existing); no rewards catalogue |
| FR-017 | Promotions and campaigns | ❌ Later |
| FR-018 | Restaurant analytics | ✅ Revenue, orders, AOV, prep time, channels, top dishes, daily trend |
| FR-019 | AI analytics and telemetry | ✅ Per turn: agent, tokens, tool calls, latency, outcome; restaurant and platform views |
| FR-020 | Platform administration | ✅ Restaurants (verify, suspend, reinstate, plan, AI switch), users, overview |
| FR-021 | Subscription and billing | ◐ Plans and entitlements enforced; no invoices or payment |
| FR-022 | POS/KDS integrations | ❌ Later |
| FR-023 | Notifications | ❌ Later (guests and staff see in-app status) |
| FR-024 | Tenant and branch authorization | ✅ Token claims → gateway → API checks → EF filters → PostgreSQL RLS |
| FR-025 | Audit and security logging | ✅ Every domain event and AI tool call, with actor and tenant, in the admin audit log |
| FR-026 | Knowledge base and retrieval | ◐ Owner-written AI knowledge (policies, FAQs) injected per turn; vector RAG needs an embeddings provider |
| FR-027 | Agent registry and tool permissions | ✅ Registry in admin; tools authorized per call (User + Tenant + Branch + Role + Permission) |
| FR-028 | Support and escalation | ✅ "Call waiter" / "Request bill" and AI hand-over (`request_staff`) to the staff board |
| FR-029 | Monitoring and operational controls | ◐ Health checks, OpenTelemetry, AI switch, rate limits, quotas; no alerting |
| FR-030 | Market localization | ◐ Currency and time zone per restaurant; language packs later |

Business rules BR-001…BR-012 are enforced as before (AI only through authorized tools, live
prices/availability, idempotent orders, conflict-free reservations). BR-005 (payments) and
BR-009 (promotions) apply once those modules exist.

## Demo accounts (local development)

| App | URL | Email | Password |
|---|---|---|---|
| Platform admin | http://localhost:3002 | admin@restaurant-ai.local | Admin#2026dev |
| Restaurant console (owner) | http://localhost:3001 | owner@spiceroute.local | Owner#2026 |
| Restaurant console (waiter) | http://localhost:3001 | waiter@spiceroute.local | Waiter#2026 |
| Restaurant console (kitchen) | http://localhost:3001 | kitchen@spiceroute.local | Kitchen#2026 |
| Guest app | http://localhost:3000 | guest@demo.local | Guest#2026 |

These exist only in the local development database; the admin is created from
`appsettings.Development.json`, the others by a seed script.

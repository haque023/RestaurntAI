import { expect, test } from "@playwright/test";
import { allDay, Api } from "../support/api";
import { env, LIVE_CLAUDE_SKIP_REASON, runId, testPassword } from "../support/env";

/**
 * The MVP 1 API journey (inventory A1–E5), run through the isolated gateway as one ordered story.
 *
 * Provenance, recorded on every test as an annotation:
 *   recovered          – ported one-to-one from the historical e2e_mvp1.py (same request, same assertion)
 *   recovered-adapted  – the historical check, with a documented change (live-Claude gating or a bounded poll)
 */
const provenance = (kind: "recovered" | "recovered-adapted", note?: string) => {
  test.info().annotations.push({ type: "provenance", description: note ? `${kind}: ${note}` : kind });
};
const live = (name: string) => test.info().annotations.push({ type: "provider", description: `real Claude call (${name})` });

test.describe.configure({ mode: "serial" });

let api: Api;
const s: Record<string, any> = {};

/** Bounded wait for eventually consistent reads (audit stream, 60 s AI context cache). */
async function eventually<T>(read: () => Promise<T>, ok: (value: T) => boolean, timeoutMs: number, stepMs = 2_000): Promise<T> {
  const until = Date.now() + timeoutMs;
  let value = await read();
  while (!ok(value) && Date.now() < until) {
    await new Promise((r) => setTimeout(r, stepMs));
    value = await read();
  }
  return value;
}

/** Live AI turn with a bounded timeout and at most one retry on a provider/transport failure. */
async function aiTurn(path: string, data: unknown, token: string) {
  for (let attempt = 1; ; attempt++) {
    const result = await api.call("POST", path, { token, data });
    if (result.status < 500 || attempt === 2) return result;
  }
}

/** Provider usage as recorded by Ai.Api from Claude's own response (never estimated here). */
async function platformTokens() {
  const usage = await api.ok("GET", "/api/admin/ai/usage", 200, { token: s.admin });
  return { input: usage.summary.inputTokens as number, output: usage.summary.outputTokens as number, turns: usage.summary.turns as number };
}

test.beforeAll(async () => {
  api = await Api.create();
});
test.afterAll(async () => {
  await api.dispose();
});

test.describe("A. Accounts and sign-up", () => {
  test("A1 admin login succeeds", async () => {
    provenance("recovered");
    const session = await api.ok("POST", "/api/auth/login", 200, { data: { email: env.adminEmail, password: env.adminPassword, scope: "Admin" } });
    s.admin = session.accessToken;
    expect(session.user.isPlatformAdministrator).toBe(true);
  });

  test("A2 wrong admin password is rejected", async () => {
    provenance("recovered");
    const r = await api.call("POST", "/api/auth/login", { data: { email: env.adminEmail, password: "wrong-pass-1", scope: "Admin" } });
    expect(r.status).toBe(401);
  });

  test("A3 restaurant sign-up issues an owner session", async () => {
    provenance("recovered");
    const owner = await api.signupRestaurant("dhaka");
    Object.assign(s, { owner: owner.token, ownerEmail: owner.email, tenant: owner.tenantId, restaurantName: owner.name });
    expect(owner.session.memberships[0].role).toBe("restaurant_owner");
  });

  test("A4 newly registered restaurant remains pending verification", async () => {
    provenance("recovered");
    const r = await api.ok("GET", "/api/restaurants/current", 200, { token: s.owner });
    expect(r.status).toBe("PendingVerification");

    const b = await api.ok("POST", "/api/restaurants/current/branches", 201, { token: s.owner, data: { name: "Banani", address: "Road 11, Banani, Dhaka" } });
    s.branch = b.id;
    await api.ok("PUT", `/api/restaurants/current/branches/${s.branch}`, 200, {
      token: s.owner,
      data: { name: "Banani", address: "Road 11, Banani, Dhaka", latitude: 23.7937, longitude: 90.4066, openingHours: allDay },
    });
    await api.ok("PUT", "/api/restaurants/current/profile", 200, {
      token: s.owner,
      data: { description: "Fiery Bangladeshi street food and vegan bowls.", cuisines: ["bangladeshi", "street food"], dietaryOptions: ["vegan", "halal"], phone: "+880 1700 000000" },
    });
  });

  test("A5 public listing is blocked until verification", async () => {
    provenance("recovered");
    const r = await api.call("PUT", "/api/restaurants/current/listing", { token: s.owner, data: { listed: true } });
    expect(r.status).toBe(422);
    expect(r.body.title).toBe("restaurant.pending_verification");
  });

  test("A6 Starter plan branch limit is enforced", async () => {
    provenance("recovered");
    const r = await api.call("POST", "/api/restaurants/current/branches", { token: s.owner, data: { name: "Gulshan", address: "x" } });
    expect(r.status).toBe(422);
    expect(r.body.title).toBe("restaurant.branch_limit");
  });
});

test.describe("B. Admin approval and restaurant setup", () => {
  test("B1 admin can see the pending restaurant", async () => {
    provenance("recovered");
    const pending = await api.ok("GET", "/api/admin/restaurants?status=PendingVerification", 200, { token: s.admin });
    expect(pending.some((x: any) => x.id === s.tenant)).toBe(true);
  });

  test("B2 owner can list the restaurant after admin approval", async () => {
    provenance("recovered");
    await api.ok("POST", `/api/admin/restaurants/${s.tenant}/approve`, 200, { token: s.admin });
    const r = await api.ok("PUT", "/api/restaurants/current/listing", 200, { token: s.owner, data: { listed: true } });
    expect(r.status).toBe("Active");
  });

  test("B3 owner can edit a menu item", async () => {
    provenance("recovered");
    s.items = {};
    for (const [category, name, price, tags, description] of [
      ["Mains", `Fuchka Bowl ${runId()}`, 180, ["vegan", "spicy"], "Crispy shells, spicy tamarind water, chickpeas."],
      ["Mains", `Beef Tehari ${runId()}`, 320, ["halal", "spicy"], "Aromatic rice with beef and green chilli."],
      ["Desserts", `Mango Lassi ${runId()}`, 120, ["vegetarian"], "Chilled yoghurt and mango."],
    ] as const) {
      s.items[name] = await api.ok("POST", `/api/menu/branches/${s.branch}/items`, 201, {
        token: s.owner,
        data: { category, name, description, price, currency: "BDT", dietaryTags: tags, ingredients: [] },
      });
    }
    const first = s.items[`Beef Tehari ${runId()}`];
    const updated = await api.ok("PUT", `/api/menu/items/${first.id}`, 200, {
      token: s.owner,
      data: { category: first.category, name: first.name, description: first.description, currency: first.currency, dietaryTags: first.dietaryTags, ingredients: first.ingredients, price: 340 },
    });
    expect(updated.price).toBe(340);
    await api.ok("PUT", "/api/restaurants/current/ai-settings", 200, {
      token: s.owner,
      data: { knowledge: "We do not take card payments under 200 BDT. Free filtered water. Kitchen closes 30 minutes before closing.", tone: "warm, brief, uses a little Bangla" },
    });
  });

  test("B4 duplicate table labels are rejected", async () => {
    provenance("recovered");
    s.table = await api.ok("POST", `/api/restaurants/current/branches/${s.branch}/tables`, 201, { token: s.owner, data: { label: "T1", capacity: 4 } });
    const r = await api.call("POST", `/api/restaurants/current/branches/${s.branch}/tables`, { token: s.owner, data: { label: "T1", capacity: 2 } });
    expect(r.status).toBe(409);
  });

  test("B5 QR code resolves to its table", async () => {
    provenance("recovered");
    const venue = await api.ok("GET", `/api/discovery/restaurants/qr/${s.table.qrToken}`, 200);
    expect(venue.tableId).toBe(s.table.id);
    expect(venue.tableLabel).toBe("T1");
  });

  test("B6 staff list returns correct JSON membership data", async () => {
    provenance("recovered");
    const waiter = await api.addStaff(s.owner, "waiter", "waiter", s.branch);
    s.waiterEmail = waiter.email;
    const staff = await api.ok("GET", "/api/auth/staff", 200, { token: s.owner });
    const emails = new Set(staff.map((m: any) => m.email));
    expect(emails.has(s.ownerEmail) && emails.has(s.waiterEmail)).toBe(true);
  });

  test("B7 waiter cannot edit restaurant details", async () => {
    provenance("recovered");
    const w = await api.ok("POST", "/api/auth/login", 200, { data: { email: s.waiterEmail, password: testPassword("waiter"), scope: "Staff" } });
    s.waiter = w.accessToken;
    const r = await api.call("PUT", "/api/restaurants/current/profile", { token: s.waiter, data: { description: "hack", cuisines: [], dietaryOptions: [] } });
    expect(r.status).toBe(403);
  });
});

test.describe("C. QR ordering and kitchen workflow", () => {
  test("C1 anonymous QR guest session is created", async () => {
    provenance("recovered");
    const guest = await api.venueSession({ qrToken: s.table.qrToken });
    s.guest = guest.accessToken;
    expect(guest.user.isGuest).toBe(true);
    expect(guest.venue.tableId).toBe(s.table.id);
  });

  test("C2 guest order uses the current live menu price", async () => {
    provenance("recovered");
    s.order = await api.ok("POST", "/api/orders/", 201, {
      token: s.guest,
      headers: { "Idempotency-Key": `k-${runId()}` },
      data: { branchId: s.branch, tableId: s.table.id, channel: "QrTable", items: [{ menuItemId: s.items[`Fuchka Bowl ${runId()}`].id, quantity: 2 }] },
    });
    expect(s.order.total).toBe(360);
  });

  test("C3 waiter sees the order on the board", async () => {
    provenance("recovered");
    const board = await api.ok("GET", `/api/orders/?branchId=${s.branch}&status=Placed,Accepted`, 200, { token: s.waiter });
    expect(board.some((o: any) => o.id === s.order.id)).toBe(true);
  });

  test("C4 kitchen workflow transitions the order to Served", async () => {
    provenance("recovered");
    let o: any;
    for (const status of ["Accepted", "Preparing", "Ready", "Served"]) {
      o = await api.ok("POST", `/api/orders/${s.order.id}/status`, 200, { token: s.waiter, data: { status } });
    }
    expect(o.status).toBe("Served");
    expect(o.history).toHaveLength(5);
  });

  test("C5 invalid order-state transition is rejected", async () => {
    provenance("recovered");
    const r = await api.call("POST", `/api/orders/${s.order.id}/status`, { token: s.waiter, data: { status: "Accepted" } });
    expect(r.status).toBe(409);
  });

  test("C6 guest cannot change order status", async () => {
    provenance("recovered");
    const r = await api.call("POST", `/api/orders/${s.order.id}/status`, { token: s.guest, data: { status: "Completed" } });
    expect(r.status).toBe(403);
  });

  test("C7 guest can see their own order", async () => {
    provenance("recovered");
    const mine = await api.ok("GET", "/api/orders/mine", 200, { token: s.guest });
    expect(mine).toHaveLength(1);
  });

  test("C8 service request is bound to the QR table", async () => {
    provenance("recovered");
    s.request = await api.ok("POST", "/api/service-requests/", 201, { token: s.guest, data: { kind: "RequestBill", note: "Card please" } });
    expect(s.request.tableId).toBe(s.table.id);
  });

  test("C9 waiter can see and resolve the service request", async () => {
    provenance("recovered");
    const open = await api.ok("GET", `/api/service-requests/?branchId=${s.branch}`, 200, { token: s.waiter });
    await api.ok("POST", `/api/service-requests/${s.request.id}/resolve`, 200, { token: s.waiter });
    expect(open.some((x: any) => x.id === s.request.id)).toBe(true);
  });

  test("C10 owner statistics reflect the test activity", async () => {
    provenance("recovered");
    const stats = await api.ok("GET", "/api/orders/stats", 200, { token: s.owner });
    expect(stats.orders).toBeGreaterThanOrEqual(1);
    expect(stats.revenue).toBeGreaterThanOrEqual(360);
  });

  test("C11 duplicate registration is rejected", async () => {
    provenance("recovered");
    const c = await api.register("customer");
    Object.assign(s, { customer: c.token, customerId: c.session.user.id, customerEmail: c.email });
    const r = await api.call("POST", "/api/auth/register", { data: { email: c.email, password: testPassword("customer") } });
    expect(r.status).toBe(409);
  });
});

test.describe("D. AI functionality", () => {
  test("D1 AI discovery returns a relevant answer", async () => {
    provenance("recovered-adapted", "skipped unless RUN_LIVE_CLAUDE_TESTS=true");
    test.skip(!env.liveClaude, LIVE_CLAUDE_SKIP_REASON);
    live("discovery assistant");
    const before = await platformTokens();
    const r = await aiTurn("/api/ai/discovery/turns", { message: "I want something vegan and spicy under 250 taka near me", latitude: 23.7925, longitude: 90.4078 }, s.customer);
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    s.discovery = r.body;
    const after = await platformTokens();
    test.info().annotations.push({ type: "provider-usage", description: `turns +${after.turns - before.turns}, input tokens +${after.input - before.input}, output tokens +${after.output - before.output} (as recorded from Claude's response)` });
    expect(r.body.outcome).toBe("Answered");
    expect(r.body.reply.toLowerCase()).toContain("fuchka");
  });

  test("D2 AI discovery links to the relevant menu", async () => {
    provenance("recovered-adapted", "skipped unless RUN_LIVE_CLAUDE_TESTS=true");
    test.skip(!env.liveClaude, LIVE_CLAUDE_SKIP_REASON);
    live("discovery assistant, from D1");
    expect(s.discovery?.reply ?? "").toContain(`/r/${s.tenant}/${s.branch}`);
  });

  test("D3 customer venue session preserves identity", async () => {
    provenance("recovered");
    const cv = await api.venueSession({ tenantId: s.tenant, branchId: s.branch }, s.customer);
    s.customerVenue = cv.accessToken;
    expect(cv.user.id).toBe(s.customerId);
    expect(cv.user.isGuest).toBe(false);
  });

  test("D4 AI Waiter uses restaurant knowledge", async () => {
    provenance("recovered-adapted", "skipped unless RUN_LIVE_CLAUDE_TESTS=true");
    test.skip(!env.liveClaude, LIVE_CLAUDE_SKIP_REASON);
    live("AI waiter");
    const before = await platformTokens();
    const r = await aiTurn("/api/ai/waiter/turns", { branchId: s.branch, message: "Do you take card for a 150 taka order? Also what is spicy here?" }, s.customerVenue);
    expect(r.status, JSON.stringify(r.body)).toBe(200);
    const after = await platformTokens();
    test.info().annotations.push({ type: "provider-usage", description: `turns +${after.turns - before.turns}, input tokens +${after.input - before.input}, output tokens +${after.output - before.output} (as recorded from Claude's response)` });
    expect(r.body.outcome).toBe("Answered");
    expect(r.body.reply.includes("200") || r.body.reply.toLowerCase().includes("card")).toBe(true);
  });

  test("D5 admin AI switch disables the AI Waiter", async () => {
    provenance(
      "recovered-adapted",
      "switch asserted directly; the AI service's 60 s context cache is awaited by polling the non-billable usage endpoint, then exactly one waiter probe is sent, which is refused before any model call",
    );
    try {
      await test.step("the admin switch turns AI off for the restaurant", async () => {
        const r = await api.ok("PUT", `/api/admin/restaurants/${s.tenant}/ai`, 200, { token: s.admin, data: { enabled: false } });
        expect(r.aiEnabled).toBe(false);
        const context = await api.ok("GET", "/api/restaurants/current/ai-context", 200, { token: s.customerVenue });
        expect(context.aiEnabled).toBe(false);
      });
      await test.step("the AI service sees the change (shared per-tenant cache, polled without any model call)", async () => {
        const usage = await eventually(() => api.ok("GET", "/api/ai/usage/current", 200, { token: s.owner }), (u) => u.aiEnabled === false, 75_000, 5_000);
        expect(usage.aiEnabled, "AI service still reports AI enabled after 75 s").toBe(false);
      });
      await test.step("one waiter probe is refused", async () => {
        const r = await api.call("POST", "/api/ai/waiter/turns", { token: s.customerVenue, data: { branchId: s.branch, message: "hello" } });
        expect(r.status).toBe(403);
        expect(r.body.title).toBe("ai.disabled");
      });
    } finally {
      await api.ok("PUT", `/api/admin/restaurants/${s.tenant}/ai`, 200, { token: s.admin, data: { enabled: true } });
    }
  });

  test("D6 admin can change the restaurant plan", async () => {
    provenance("recovered");
    const pl = await api.ok("PUT", `/api/admin/restaurants/${s.tenant}/plan`, 200, { token: s.admin, data: { planCode: "growth" } });
    expect(pl.plan.code).toBe("growth");
  });

  test("D7 restaurant can see its AI usage", async () => {
    provenance("recovered-adapted", "polls for the new plan limit; requires ≥1 used turn only when live Claude tests ran");
    const usage = await eventually(() => api.ok("GET", "/api/ai/usage/current", 200, { token: s.owner }), (u) => u.limit === 3000, 75_000, 5_000);
    expect(usage.limit).toBe(3000);
    expect(usage.usedTurns).toBeGreaterThanOrEqual(env.liveClaude ? 1 : 0);
    expect(usage.summary).toHaveProperty("byAgent");
  });

  test("D8 admin can see platform-wide AI usage", async () => {
    provenance("recovered-adapted", "requires ≥2 turns only when live Claude tests ran");
    const au = await api.ok("GET", "/api/admin/ai/usage", 200, { token: s.admin });
    expect(au.summary.turns).toBeGreaterThanOrEqual(env.liveClaude ? 2 : 0);
    expect(Array.isArray(au.byTenant)).toBe(true);
  });

  test("D9 agent registry is accessible", async () => {
    provenance("recovered");
    const agents = await api.ok("GET", "/api/admin/ai/agents", 200, { token: s.admin });
    const names = new Set(agents.agents.map((a: any) => a.name));
    expect(names.has("consumer-ai-waiter") && names.has("discovery-assistant")).toBe(true);
  });
});

test.describe("E. Audit and administration", () => {
  test("E1 audit log contains the expected domain events", async () => {
    provenance("recovered-adapted", "fixed 3 s sleep replaced by a bounded poll (up to 30 s, every 2 s) until all five required events are visible");
    const required = ["restaurant.RestaurantRegistered", "restaurant.RestaurantVerified", "order.OrderCreated", "order.ServiceRequestOpened", "restaurant.RestaurantPlanChanged"];
    const missing = (rows: any[]) => required.filter((t) => !rows.some((e) => e.type === t && e.tenantId === s.tenant));
    const audit = await eventually(
      () => api.ok("GET", `/api/admin/audit?tenantId=${s.tenant}&take=200`, 200, { token: s.admin }),
      (rows: any[]) => missing(rows).length === 0,
      30_000,
    );
    test.info().annotations.push({ type: "audit-events", description: [...new Set(audit.map((e: any) => e.type))].sort().join(", ") });
    expect(audit.every((e: any) => e.tenantId === s.tenant), "every returned event belongs to the run's tenant").toBe(true);
    expect(missing(audit), "required audit events still missing after 30 s").toEqual([]);
  });

  test("E2 audit log contains AI tool-call events", async () => {
    provenance(
      "recovered-adapted",
      "the historical check relied on the live AI turns; without them this test invokes the MCP tool search_restaurants directly with the customer's token (no Claude call)",
    );
    if (!env.liveClaude) {
      const response = await fetch(env.mcpUrl, {
        method: "POST",
        headers: { Authorization: `Bearer ${s.customer}`, "Content-Type": "application/json", Accept: "application/json, text/event-stream" },
        body: JSON.stringify({ jsonrpc: "2.0", id: 1, method: "tools/call", params: { name: "search_restaurants", arguments: { query: s.restaurantName } } }),
      });
      const text = await response.text();
      expect(response.status, text.slice(0, 300)).toBe(200);
      expect(text).toContain(s.restaurantName);
    }
    const events = await eventually(
      () => api.ok("GET", `/api/admin/audit?type=mcp.ToolExecuted&actorId=${s.customerId}&take=50`, 200, { token: s.admin }),
      (rows: any[]) => rows.length >= 1,
      30_000,
    );
    test.info().annotations.push({ type: "audit-events", description: `mcp.ToolExecuted x${events.length} for the run's customer` });
    expect(events.length).toBeGreaterThanOrEqual(1);
    expect(events.every((e: any) => e.actorId === s.customerId)).toBe(true);
  });

  test("E3 admin overview returns expected data", async () => {
    provenance("recovered");
    const ov = await api.ok("GET", "/api/admin/overview", 200, { token: s.admin });
    expect(ov.month.orders).toBeGreaterThanOrEqual(1);
  });

  test("E4 admin can retrieve restaurant statistics", async () => {
    provenance("recovered");
    const st = await api.ok("GET", "/api/admin/restaurants/stats", 200, { token: s.admin });
    expect(st.restaurants).toBeGreaterThanOrEqual(1);
  });

  test("E5 restaurant owner cannot access admin APIs", async () => {
    provenance("recovered");
    const r = await api.call("GET", "/api/admin/restaurants", { token: s.owner });
    expect(r.status).toBe(403);
  });
});

test.describe("F. Tenant isolation", () => {
  test("F1 another restaurant's owner cannot see this restaurant's orders, tables or staff", async () => {
    test.info().annotations.push({ type: "provenance", description: "new: tenant isolation was not covered by the historical suite" });
    const other = await api.signupRestaurant("other");
    const order = await api.call("GET", `/api/orders/${s.order.id}`, { token: other.token });
    expect([403, 404]).toContain(order.status);
    const tables = await api.call("GET", `/api/restaurants/current/branches/${s.branch}/tables`, { token: other.token });
    expect(tables.status === 200 ? tables.body.length : 0).toBe(0);
    const staff = await api.ok("GET", "/api/auth/staff", 200, { token: other.token });
    expect(staff.some((m: any) => m.email === s.waiterEmail)).toBe(false);
    const stats = await api.ok("GET", "/api/orders/stats", 200, { token: other.token });
    expect(stats.orders).toBe(0);
  });

  test("F2 a second guest at the same table cannot see or change the first guest's order", async () => {
    test.info().annotations.push({ type: "provenance", description: "new" });
    const second = await api.venueSession({ qrToken: s.table.qrToken });
    const mine = await api.ok("GET", "/api/orders/mine", 200, { token: second.accessToken });
    expect(mine.some((o: any) => o.id === s.order.id)).toBe(false);
    const read = await api.call("GET", `/api/orders/${s.order.id}`, { token: second.accessToken });
    expect([403, 404]).toContain(read.status);
    const cancel = await api.call("POST", `/api/orders/${s.order.id}/cancel`, { token: second.accessToken, data: { reason: "not mine" } });
    expect([403, 404]).toContain(cancel.status);
  });
});

test.describe("G. Kitchen staff authorization", () => {
  test("G1 kitchen staff can run the kitchen board and are refused everything else server-side", async () => {
    test.info().annotations.push({ type: "provenance", description: "new: server-side check of the kitchen_staff role (menu.read, orders.read, orders.manage)" });
    const kitchen = await api.addStaff(s.owner, "kitchen", "kitchen_staff", s.branch);
    const k = (await api.ok("POST", "/api/auth/login", 200, { data: { email: kitchen.email, password: kitchen.password, scope: "Staff" } })).accessToken;

    // A new order from the run's guest at the run's table.
    const order = await api.ok("POST", "/api/orders/", 201, {
      token: s.guest,
      headers: { "Idempotency-Key": `kitchen-${runId()}` },
      data: { branchId: s.branch, tableId: s.table.id, channel: "QrTable", items: [{ menuItemId: s.items[`Mango Lassi ${runId()}`].id, quantity: 1 }] },
    });

    await test.step("permitted: board, kitchen transitions, menu and tables of its own restaurant", async () => {
      const board = await api.ok("GET", `/api/orders/?branchId=${s.branch}&status=Placed`, 200, { token: k });
      expect(board.some((o: any) => o.id === order.id)).toBe(true);
      expect(board.every((o: any) => o.branchId === s.branch)).toBe(true);
      for (const status of ["Accepted", "Preparing", "Ready"]) {
        const o = await api.ok("POST", `/api/orders/${order.id}/status`, 200, { token: k, data: { status } });
        expect(o.status).toBe(status);
      }
      await api.ok("GET", `/api/menu/branches/${s.branch}/items`, 200, { token: k });
      await api.ok("GET", `/api/restaurants/current/branches/${s.branch}/tables`, 200, { token: k });
    });

    await test.step("refused: management, staff, menu writes, analytics, reservations, ordering, admin", async () => {
      const item = s.items[`Mango Lassi ${runId()}`];
      const denied: [string, string, unknown?][] = [
        ["PUT", "/api/restaurants/current/profile", { description: "kitchen", cuisines: [], dietaryOptions: [] }],
        ["POST", `/api/restaurants/current/branches/${s.branch}/tables`, { label: "K1", capacity: 2 }],
        ["GET", "/api/auth/staff"],
        ["POST", "/api/auth/staff", { email: `x.${runId()}@e2e.test`, role: "waiter", branchId: s.branch }],
        ["PUT", `/api/menu/items/${item.id}/availability`, { isAvailable: false }],
        ["GET", "/api/orders/stats"],
        ["GET", `/api/reservations?branchId=${s.branch}`],
        ["POST", "/api/orders/", { branchId: s.branch, channel: "QrTable", items: [{ menuItemId: item.id, quantity: 1 }] }],
        ["GET", "/api/admin/restaurants"],
      ];
      for (const [method, url, data] of denied) {
        const r = await api.call(method, url, { token: k, data, headers: { "Idempotency-Key": `k-${runId()}-${method}-${url}` } });
        expect(r.status, `${method} ${url}`).toBe(403);
        if (!url.startsWith("/api/admin")) expect(r.body?.title, `${method} ${url}`).toBe("permission.denied");
      }
      // Nothing changed behind the refusals.
      const menu = await api.ok("GET", `/api/menu/branches/${s.branch}/items`, 200, { token: s.owner });
      expect(menu.find((i: any) => i.id === item.id)?.isAvailable).toBe(true);
    });
  });
});


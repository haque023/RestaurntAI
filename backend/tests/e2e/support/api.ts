import { expect, request, type APIRequestContext } from "@playwright/test";
import { env, runId, testPassword } from "./env";
import { track } from "./registry";

export interface ApiResult<T = any> {
  status: number;
  body: T;
}

export class Api {
  private constructor(private readonly ctx: APIRequestContext) {}

  static async create(): Promise<Api> {
    return new Api(await request.newContext({ baseURL: env.gatewayUrl, timeout: 180_000 }));
  }

  async call<T = any>(method: string, url: string, options: { token?: string; data?: unknown; headers?: Record<string, string> } = {}): Promise<ApiResult<T>> {
    const headers: Record<string, string> = { ...(options.headers ?? {}) };
    if (options.token) headers.Authorization = `Bearer ${options.token}`;
    const response = await this.ctx.fetch(url, { method, data: options.data, headers });
    const text = await response.text();
    let body: any = null;
    try {
      body = text ? JSON.parse(text) : null;
    } catch {
      body = text;
    }
    return { status: response.status(), body };
  }

  /** Calls and asserts the status, with the response body in the failure message. */
  async ok<T = any>(method: string, url: string, expected: number, options: Parameters<Api["call"]>[2] = {}): Promise<T> {
    const result = await this.call<T>(method, url, options);
    expect(result.status, `${method} ${url} -> ${JSON.stringify(result.body)?.slice(0, 400)}`).toBe(expected);
    return result.body;
  }

  async adminToken(): Promise<string> {
    const s = await this.ok("POST", "/api/auth/login", 200, { data: { email: env.adminEmail, password: env.adminPassword, scope: "Admin" } });
    return s.accessToken;
  }

  /** Self sign-up of a run-owned restaurant; tracked before it is used. */
  async signupRestaurant(label: string) {
    const email = `owner.${label}.${runId()}@e2e.test`;
    const name = `E2E ${label} ${runId()}`;
    const s = await this.ok("POST", "/api/auth/signup/restaurant", 200, {
      data: { email, password: testPassword("owner"), displayName: "E2E Owner", restaurantName: name, currency: "BDT", timeZone: "Asia/Dhaka" },
    });
    track({ kind: "user", id: s.user.id, email });
    track({ kind: "tenant", id: s.memberships[0].tenantId, name });
    return { session: s, token: s.accessToken as string, tenantId: s.memberships[0].tenantId as string, email, name };
  }

  async register(label: string) {
    const email = `${label}.${runId()}@e2e.test`;
    const s = await this.ok("POST", "/api/auth/register", 200, { data: { email, password: testPassword("customer"), displayName: `E2E ${label}` } });
    track({ kind: "user", id: s.user.id, email });
    return { session: s, token: s.accessToken as string, email };
  }

  async addStaff(ownerToken: string, label: string, role: string, branchId: string | null) {
    const email = `${label}.${runId()}@e2e.test`;
    const member = await this.ok("POST", "/api/auth/staff", 201, {
      token: ownerToken,
      data: { email, displayName: `E2E ${label}`, password: testPassword(role), role, branchId },
    });
    track({ kind: "user", id: member.userId, email });
    return { email, password: testPassword(role), userId: member.userId as string };
  }

  /** Anonymous guest sessions create no account row, but track the id anyway for cleanup of AI/orders data. */
  async venueSession(data: Record<string, unknown>, token?: string) {
    const s = await this.ok("POST", "/api/auth/venue-session", 200, { data, token });
    if (s.user.isGuest) track({ kind: "user", id: s.user.id, email: "(guest)" });
    return s;
  }

  async dispose() {
    await this.ctx.dispose();
  }
}

export const allDay = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"].map((day) => ({ day, opens: "00:00:00", closes: "23:59:00" }));

/** A verified, listed restaurant with one branch, a menu and a table: the shared setup for browser journeys. */
export async function provisionRestaurant(api: Api, label: string) {
  const owner = await api.signupRestaurant(label);
  const admin = await api.adminToken();
  const branch = await api.ok("POST", "/api/restaurants/current/branches", 201, { token: owner.token, data: { name: "Banani", address: "Road 11, Banani, Dhaka" } });
  await api.ok("PUT", `/api/restaurants/current/branches/${branch.id}`, 200, {
    token: owner.token,
    data: { name: "Banani", address: "Road 11, Banani, Dhaka", latitude: 23.7937, longitude: 90.4066, openingHours: allDay },
  });
  await api.ok("POST", `/api/admin/restaurants/${owner.tenantId}/approve`, 200, { token: admin });
  await api.ok("PUT", "/api/restaurants/current/listing", 200, { token: owner.token, data: { listed: true } });
  const item = await api.ok("POST", `/api/menu/branches/${branch.id}/items`, 201, {
    token: owner.token,
    data: { category: "Mains", name: `Fuchka Bowl ${runId()}`, description: "Crispy shells, spicy tamarind water.", price: 180, currency: "BDT", dietaryTags: ["vegan", "spicy"], ingredients: [] },
  });
  const table = await api.ok("POST", `/api/restaurants/current/branches/${branch.id}/tables`, 201, { token: owner.token, data: { label: "T1", capacity: 4 } });
  return { owner, branchId: branch.id as string, item, table };
}

import { describe, expect, it } from "vitest";
import { isExpired, parseToken } from "./claims";

function token(payload: Record<string, unknown>): string {
  const encode = (value: unknown) => btoa(JSON.stringify(value)).replace(/=+$/, "").replace(/\+/g, "-").replace(/\//g, "_");
  return `${encode({ alg: "HS256" })}.${encode(payload)}.signature`;
}

describe("parseToken", () => {
  it("reads platform claims, normalizing single values to lists", () => {
    const user = parseToken(token({ sub: "u1", tenant_id: "t1", role: "customer", permission: ["menu.read", "orders.create"], exp: 2_000_000_000 }));

    expect(user).toEqual({
      userId: "u1",
      tenantId: "t1",
      branchId: null,
      tableId: null,
      roles: ["customer"],
      permissions: ["menu.read", "orders.create"],
      expiresAt: 2_000_000_000_000,
    });
  });

  it("returns null for malformed tokens or tokens without a subject", () => {
    expect(parseToken("not-a-jwt")).toBeNull();
    expect(parseToken(token({ tenant_id: "t1" }))).toBeNull();
  });

  it("detects expiry", () => {
    const user = parseToken(token({ sub: "u1", exp: 100 }))!;
    expect(isExpired(user, 100_001)).toBe(true);
    expect(isExpired(user, 99_000)).toBe(false);
  });
});

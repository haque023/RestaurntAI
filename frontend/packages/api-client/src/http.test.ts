import { describe, expect, it, vi } from "vitest";
import { ApiError, buildQuery, createHttp, IDEMPOTENCY_HEADER } from "./http";

function jsonResponse(status: number, body: unknown, headers: Record<string, string> = {}) {
  return new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json", ...headers } });
}

describe("createHttp", () => {
  it("sends bearer token, correlation id and idempotency key", async () => {
    const fetch = vi.fn().mockResolvedValue(jsonResponse(201, { id: "o1" }));
    const http = createHttp({ baseUrl: "http://gw/", getAccessToken: () => "t0k", fetch });

    const result = await http<{ id: string }>("POST", "/api/orders", { body: { a: 1 }, idempotencyKey: "key-1" });

    expect(result.id).toBe("o1");
    const [url, init] = fetch.mock.calls[0]!;
    const headers = init.headers as Headers;
    expect(url).toBe("http://gw/api/orders");
    expect(headers.get("Authorization")).toBe("Bearer t0k");
    expect(headers.get(IDEMPOTENCY_HEADER)).toBe("key-1");
    expect(headers.get("X-Correlation-ID")).toMatch(/^[a-f0-9]{32}$/);
    expect(init.body).toBe(JSON.stringify({ a: 1 }));
  });

  it("maps problem details to ApiError", async () => {
    const fetch = vi.fn().mockResolvedValue(
      jsonResponse(409, { title: "order.item_unavailable", detail: "'Ramen' is currently unavailable.", status: 409, code: "order.item_unavailable", correlationId: "c-1" }),
    );
    const http = createHttp({ fetch });

    const error = await http("POST", "/api/orders").catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 409, code: "order.item_unavailable", correlationId: "c-1", message: "'Ramen' is currently unavailable." });
  });

  it("notifies on 401", async () => {
    const onUnauthorized = vi.fn();
    const http = createHttp({ fetch: vi.fn().mockResolvedValue(new Response("", { status: 401 })), onUnauthorized });

    await expect(http("GET", "/api/orders/1")).rejects.toBeInstanceOf(ApiError);
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });
});

describe("buildQuery", () => {
  it("skips empty values and joins arrays", () => {
    expect(buildQuery({ q: "spicy", maxPrice: 15, dietary: ["vegan", "gluten-free"], empty: "", none: undefined, list: [] })).toBe(
      "?q=spicy&maxPrice=15&dietary=vegan%2Cgluten-free",
    );
  });
});

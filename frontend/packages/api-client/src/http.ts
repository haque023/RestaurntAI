import type { ProblemDetails } from "./types";

export const CORRELATION_HEADER = "X-Correlation-ID";
export const IDEMPOTENCY_HEADER = "Idempotency-Key";

/** A failed API call, carrying the backend's problem details and correlation id. */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string;
  readonly correlationId: string | undefined;
  readonly problem: ProblemDetails | undefined;

  constructor(status: number, problem: ProblemDetails | undefined, correlationId: string | undefined) {
    super(problem?.detail ?? problem?.title ?? `Request failed with status ${status}`);
    this.name = "ApiError";
    this.status = status;
    this.code = problem?.code ?? problem?.title ?? `http_${status}`;
    this.correlationId = problem?.correlationId ?? correlationId;
    this.problem = problem;
  }
}

export interface HttpClientOptions {
  /** Base URL of the YARP gateway. Empty string = same origin (dev proxy / edge). */
  baseUrl?: string;
  getAccessToken?: () => string | null | undefined;
  /** Called on 401 so the app can drop an expired session. */
  onUnauthorized?: () => void;
  fetch?: typeof fetch;
}

export interface RequestOptions {
  body?: unknown;
  query?: Record<string, string | number | boolean | string[] | undefined | null>;
  idempotencyKey?: string;
  signal?: AbortSignal;
}

export type HttpMethod = "GET" | "POST" | "PUT" | "DELETE";

export type Http = <T>(method: HttpMethod, path: string, options?: RequestOptions) => Promise<T>;

/** A new random key for a user-confirmed mutation; reuse it when retrying the same confirmation. */
export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

export function newCorrelationId(): string {
  return crypto.randomUUID().replaceAll("-", "");
}

export function buildQuery(query: RequestOptions["query"]): string {
  if (!query) return "";
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(query)) {
    if (value === undefined || value === null || value === "") continue;
    if (Array.isArray(value)) {
      if (value.length > 0) params.set(key, value.join(","));
    } else {
      params.set(key, String(value));
    }
  }
  const text = params.toString();
  return text ? `?${text}` : "";
}

export function createHttp(options: HttpClientOptions = {}): Http {
  const baseUrl = (options.baseUrl ?? "").replace(/\/$/, "");
  const doFetch = options.fetch ?? ((...args: Parameters<typeof fetch>) => fetch(...args));

  return async function request<T>(method: HttpMethod, path: string, request: RequestOptions = {}): Promise<T> {
    const headers = new Headers({ Accept: "application/json", [CORRELATION_HEADER]: newCorrelationId() });
    const token = options.getAccessToken?.();
    if (token) headers.set("Authorization", `Bearer ${token}`);
    if (request.idempotencyKey) headers.set(IDEMPOTENCY_HEADER, request.idempotencyKey);
    if (request.body !== undefined) headers.set("Content-Type", "application/json");

    const response = await doFetch(`${baseUrl}${path}${buildQuery(request.query)}`, {
      method,
      headers,
      body: request.body === undefined ? undefined : JSON.stringify(request.body),
      signal: request.signal,
    });

    const correlationId = response.headers.get(CORRELATION_HEADER) ?? undefined;
    const text = await response.text();
    const payload: unknown = text ? safeJson(text) : undefined;

    if (!response.ok) {
      if (response.status === 401) options.onUnauthorized?.();
      throw new ApiError(response.status, isProblem(payload) ? payload : undefined, correlationId);
    }

    return payload as T;
  };
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function isProblem(value: unknown): value is ProblemDetails {
  return typeof value === "object" && value !== null && ("title" in value || "detail" in value || "status" in value);
}

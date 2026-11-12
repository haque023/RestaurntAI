import { ApiError } from "@restaurant-ai/api-client";

/** Retry transient failures only; 4xx responses (validation, auth, not found) are final. */
export function shouldRetry(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) return false;
  return failureCount < 2;
}

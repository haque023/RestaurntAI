/** The platform user as described by token claims (see backend PlatformClaims). */
export interface SessionUser {
  userId: string;
  tenantId: string | null;
  branchId: string | null;
  tableId: string | null;
  roles: string[];
  permissions: string[];
  expiresAt: number | null;
}

function toList(value: unknown): string[] {
  if (Array.isArray(value)) return value.filter((v): v is string => typeof v === "string");
  return typeof value === "string" ? [value] : [];
}

function toStringOrNull(value: unknown): string | null {
  return typeof value === "string" && value.length > 0 ? value : null;
}

function decodeSegment(segment: string): unknown {
  const base64 = segment.replace(/-/g, "+").replace(/_/g, "/").padEnd(Math.ceil(segment.length / 4) * 4, "=");
  const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
  return JSON.parse(new TextDecoder().decode(bytes));
}

/**
 * Reads claims for display and UI decisions only. The token is NOT verified here;
 * every API validates it, so the browser never makes security decisions on its own.
 */
export function parseToken(token: string): SessionUser | null {
  const payload = token.split(".")[1];
  if (!payload) return null;

  try {
    const claims = decodeSegment(payload) as Record<string, unknown>;
    const userId = toStringOrNull(claims.sub);
    if (!userId) return null;

    return {
      userId,
      tenantId: toStringOrNull(claims.tenant_id),
      branchId: toStringOrNull(claims.branch_id),
      tableId: toStringOrNull(claims.table_id),
      roles: toList(claims.role),
      permissions: toList(claims.permission),
      expiresAt: typeof claims.exp === "number" ? claims.exp * 1000 : null,
    };
  } catch {
    return null;
  }
}

export function isExpired(user: SessionUser, now = Date.now()): boolean {
  return user.expiresAt !== null && user.expiresAt <= now;
}

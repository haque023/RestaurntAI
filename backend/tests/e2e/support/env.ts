import { existsSync, readFileSync } from "node:fs";
import path from "node:path";

export const E2E_ROOT = path.resolve(__dirname, "..");
export const RUN_DIR = path.join(E2E_ROOT, ".run");

/** Values written by `stack/stack.sh up` (never committed). */
function readStackEnv(): Record<string, string> {
  const file = path.join(RUN_DIR, "stack.env");
  if (!existsSync(file)) return {};
  return Object.fromEntries(
    readFileSync(file, "utf8")
      .split("\n")
      .filter((l) => l.includes("="))
      .map((l) => [l.slice(0, l.indexOf("=")), l.slice(l.indexOf("=") + 1)]),
  );
}

const stack = readStackEnv();

/** The API and browser projects need the stack; the cleanup support tests do not. */
export function requireStack(): void {
  if (!stack.E2E_GATEWAY_URL) throw new Error("The isolated E2E stack has not been created. Run `npm run stack:up` first (see README.md).");
}

export const env = {
  gatewayUrl: process.env.E2E_GATEWAY_URL ?? stack.E2E_GATEWAY_URL,
  mcpUrl: process.env.E2E_MCP_URL ?? stack.E2E_MCP_URL,
  adminEmail: stack.E2E_ADMIN_EMAIL,
  adminPassword: stack.E2E_ADMIN_PASSWORD,
  dbName: process.env.E2E_DB_NAME ?? stack.E2E_DB_NAME,
  pgContainer: process.env.E2E_PG_CONTAINER ?? stack.E2E_PG_CONTAINER,
  consumerUrl: "http://localhost:4100",
  restaurantUrl: "http://localhost:4101",
  adminUrl: "http://localhost:4102",
  /** Real Claude calls are opt-in: they cost money and depend on an external provider. */
  liveClaude: process.env.RUN_LIVE_CLAUDE_TESTS === "true",
};

export const LIVE_CLAUDE_SKIP_REASON =
  "Live Claude test: set RUN_LIVE_CLAUDE_TESTS=true (and provide the Anthropic key to Ai.Api via ANTHROPIC_API_KEY or its user-secrets) to run it.";

/** Set by global setup; one id per `playwright test` invocation, used in every name and email. */
export function runId(): string {
  const id = process.env.E2E_RUN_ID;
  if (!id) throw new Error("E2E_RUN_ID is not set; global setup did not run.");
  return id;
}

/**
 * Test passwords come from a random per-run secret (set by global setup, never logged), so they cannot
 * be derived from the run id that appears in every report. The secret is also written to
 * `.run/secrets/` (mode 600, git-ignored) so the artifact scan can prove it never reached a report.
 */
export function testPassword(role: string): string {
  const secret = process.env.E2E_PASSWORD_SECRET;
  if (!secret) throw new Error("E2E_PASSWORD_SECRET is not set; global setup did not run.");
  return `${role[0].toUpperCase()}${role.slice(1)}#${secret}`;
}

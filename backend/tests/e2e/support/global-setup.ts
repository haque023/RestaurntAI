import { randomBytes } from "node:crypto";
import { chmodSync, mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { devFingerprint, e2eDatabase } from "./db";
import { env, requireStack, RUN_DIR } from "./env";

export default async function globalSetup() {
  const runId = `e2e${randomBytes(4).toString("hex")}`;
  process.env.E2E_RUN_ID = runId;
  // Random per-run password secret (see testPassword); kept out of every log and report.
  const secret = randomBytes(18).toString("base64url");
  process.env.E2E_PASSWORD_SECRET = secret;

  requireStack();
  e2eDatabase().assertIsolated();
  const health = await fetch(`${env.gatewayUrl}/health/live`).catch(() => null);
  if (!health?.ok) throw new Error(`E2E gateway ${env.gatewayUrl} is not healthy. Run \`npm run stack:up\` first.`);

  mkdirSync(path.join(RUN_DIR, "secrets"), { recursive: true });
  const secretFile = path.join(RUN_DIR, "secrets", `run-${runId}.secret`);
  writeFileSync(secretFile, secret, { mode: 0o600 });
  chmodSync(secretFile, 0o600);
  writeFileSync(path.join(RUN_DIR, `run-${runId}.json`), JSON.stringify({ runId, startedAt: new Date().toISOString(), devBefore: devFingerprint() }, null, 2));
  console.log(`E2E run ${runId} | database ${env.dbName} (isolated) | live Claude tests: ${env.liveClaude ? "ENABLED" : "skipped (opt-in)"}`);
}

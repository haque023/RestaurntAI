import { existsSync, readdirSync, readFileSync, renameSync, writeFileSync } from "node:fs";
import path from "node:path";
import { devFingerprint, e2eDatabase, type CleanupReport, type E2eDatabase } from "./db";
import { RUN_DIR } from "./env";
import { readRegistry, REGISTRY_FILE, registryFile } from "./registry";

export type RegistryCleanup = CleanupReport | { runId: string; unresolved: string[] };

/**
 * The one cleanup path for run registries, used by global teardown (current run and the sweep of
 * unfinished runs) and by the explicit one-time cleanup of completed (`.done`) runs.
 * Registries are validated; nothing is deleted for an invalid one. A pending registry is marked
 * `.done` only when its cleanup is complete.
 */
export function cleanupRegistries(db: E2eDatabase, runIds: string[], options: { includeDone?: boolean; dir?: string } = {}): RegistryCleanup[] {
  return runIds.map((runId) => {
    const registry = readRegistry(runId, { includeDone: options.includeDone, dir: options.dir });
    if (registry.errors.length > 0) return { runId, unresolved: registry.errors };
    const report = db.cleanupRun(runId, registry.records);
    const pending = registryFile(runId, options.dir);
    if (report.status === "complete" && existsSync(pending)) renameSync(pending, `${pending}.done`);
    return report;
  });
}

/** Run ids with a registry in `dir`: pending ones only, or also completed (`.done`) ones. */
export function registeredRuns(options: { includeDone?: boolean; dir?: string } = {}): string[] {
  const ids = readdirSync(options.dir ?? RUN_DIR)
    .map((name) => REGISTRY_FILE.exec(name))
    .filter((m): m is RegExpExecArray => !!m && (options.includeDone || !m[2]))
    .map((m) => m[1]);
  return [...new Set(ids)].sort();
}

/**
 * After all assertions: report the run's audit events, delete exactly its verified data, verify
 * independently, sweep earlier unfinished runs with the same rules, and prove the dev DB is untouched.
 */
export default async function globalTeardown() {
  const runId = process.env.E2E_RUN_ID;
  if (!runId) return;
  const file = path.join(RUN_DIR, `run-${runId}.json`);
  const run = JSON.parse(readFileSync(file, "utf8"));
  const db = e2eDatabase();
  const keep = process.env.E2E_KEEP_DATA === "true";

  const registry = readRegistry(runId);
  if (registry.errors.length > 0) throw new Error(`Run ${runId} registry is invalid; nothing deleted: ${registry.errors.join("; ")}`);
  const audit = registry.records.length ? db.auditEventsOf(registry.records) : {};
  const [cleanup] = keep || registry.records.length === 0 ? [null] : cleanupRegistries(db, [runId]);

  // Earlier runs whose cleanup never finished (crash, interruption, needs-review): same path.
  const swept = keep ? [] : cleanupRegistries(db, registeredRuns().filter((id) => id !== runId));
  const devAfter = devFingerprint();

  const summary = {
    ...run,
    finishedAt: new Date().toISOString(),
    ownedRecords: { tenants: registry.records.filter((o) => o.kind === "tenant").length, users: registry.records.filter((o) => o.kind === "user").length },
    auditEventsCreated: audit,
    cleanup: cleanup ?? "skipped",
    earlierRunsSwept: swept,
    devAfter,
    devDatabaseUnchanged: devAfter === run.devBefore,
  };
  writeFileSync(file, JSON.stringify(summary, null, 2));

  console.log(`\nE2E run ${runId} summary: ${path.relative(process.cwd(), file)}`);
  console.log(`  audit events created: ${JSON.stringify(audit)}`);
  for (const r of [cleanup, ...swept]) {
    if (!r) continue;
    if ("unresolved" in r) console.log(`  run ${r.runId}: unresolved registry (${r.unresolved.join("; ")}), nothing deleted`);
    else
      console.log(
        `  run ${r.runId}: cleanup ${r.status}, deleted ${JSON.stringify(r.deleted)}, confirmed residual ${JSON.stringify(r.residual.confirmed)}, ambiguous ${JSON.stringify(r.residual.ambiguous)}, admin preserved ${r.adminPreserved}`,
      );
  }
  console.log(`  development database unchanged: ${summary.devDatabaseUnchanged}`);

  const failed = [cleanup, ...swept].filter((r): r is CleanupReport => !!r && "status" in r && r.status === "incomplete");
  if (failed.length > 0) throw new Error(`Cleanup incomplete for ${failed.map((r) => r.runId).join(", ")}: confirmed run-owned rows remain (see ${file}).`);
}

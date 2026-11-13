import { expect, test } from "@playwright/test";
import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import path from "node:path";
import { e2eDatabase, type CleanupReport, type E2eDatabase } from "../../support/db";
import { E2E_ROOT, env } from "../../support/env";
import { cleanupRegistries, registeredRuns, type RegistryCleanup } from "../../support/global-teardown";
import { readRegistry } from "../../support/registry";
import { createDisposable, dropDisposable } from "../fixture-db";

/**
 * One-time cleanup of completed (`.done`) historical runs, through the same `cleanupRegistries` path
 * global teardown uses. Step 1 always runs against a disposable full copy; step 2 touches the real
 * E2E database only with E2E_MAINT_APPLY=restaurant_ai_e2e and only if its candidate rows are exactly
 * the ones step 1 validated.
 */
test.describe.configure({ mode: "serial" });

const CLONE = "restaurant_ai_historyclone_e2e";
const EVIDENCE = path.join(E2E_ROOT, "test-results", "historical-clone-evidence.json");

/** Outbox rows that name a run and whose userId is in that run's registry: the candidate set. */
function candidates(db: E2eDatabase, runs: string[]) {
  const rows: { run: string; id: string; userId: string }[] = [];
  for (const run of runs) {
    const users = readRegistry(run, { includeDone: true }).records.filter((r) => r.kind === "user").map((r) => r.id);
    for (const [id, userId] of db.psql(
      `SELECT id, payload ->> 'userId' FROM identity.outbox_messages
        WHERE payload ->> 'userId' = ANY(:'users'::text[]) AND payload ->> 'email' ~ :'run_re' ORDER BY id;`,
      { users: `{${users.join(",")}}`, run_re: `(^|[^0-9a-f])${run}([^0-9a-f]|$)` },
    )) {
      rows.push({ run, id, userId });
    }
  }
  return rows;
}

/** Row-level fingerprints of the administrator's records, identified by its configured email. */
function adminRecords(db: E2eDatabase) {
  const [[account, outbox, audit]] = db.psql(
    `SELECT (SELECT md5(string_agg(row_to_json(a)::text, '' ORDER BY id)) FROM identity.user_accounts a WHERE email = :'e'),
            (SELECT md5(string_agg(row_to_json(o)::text, '' ORDER BY id)) FROM identity.outbox_messages o WHERE payload ->> 'email' = :'e'),
            (SELECT md5(string_agg(row_to_json(x)::text, '' ORDER BY id)) FROM platform.audit_events x WHERE payload ->> 'email' = :'e');`,
    { e: env.adminEmail },
  );
  return { account, outbox, audit };
}

function counts(db: E2eDatabase) {
  const [[outbox, audit, accounts, restaurants, orders, marker]] = db.psql(
    `SELECT (SELECT count(*) FROM identity.outbox_messages), (SELECT count(*) FROM platform.audit_events), (SELECT count(*) FROM identity.user_accounts),
            (SELECT count(*) FROM restaurant.restaurants), (SELECT count(*) FROM orders.orders), (SELECT string_agg(marker, ',') FROM e2e_meta.environment);`,
  );
  return { outbox: +outbox, audit: +audit, accounts: +accounts, restaurants: +restaurants, orders: +orders, marker };
}

const summarize = (reports: RegistryCleanup[]) =>
  reports.map((r) =>
    "unresolved" in r
      ? { run: r.runId, unresolved: r.unresolved }
      : { run: r.runId, status: r.status, deleted: r.deleted, mismatchedKept: r.mismatchedEventsKept, unverified: r.unverified.length, confirmed: r.residual.confirmed, ambiguous: r.residual.ambiguous, leftovers: r.residual.planLeftovers, adminPreserved: r.adminPreserved },
  );

test("1 disposable full copy: teardown cleanup path with the completed registries", () => {
  const runs = registeredRuns({ includeDone: true });
  expect(runs).toHaveLength(7);
  const clone = createDisposable(CLONE, { withData: true, marker: false });
  try {
    const db = new (clone.constructor as typeof E2eDatabase)({ ...clone.target, adminEmail: env.adminEmail });
    const before = { counts: counts(db), admin: adminRecords(db), candidates: candidates(db, runs) };

    const first = cleanupRegistries(db, runs, { includeDone: true });
    const after = { counts: counts(db), admin: adminRecords(db), candidates: candidates(db, runs) };
    const second = cleanupRegistries(db, runs, { includeDone: true });
    const again = { counts: counts(db), admin: adminRecords(db) };

    const evidence = { clone: CLONE, runs, before, first: summarize(first), after, second: summarize(second), again };
    mkdirSync(path.dirname(EVIDENCE), { recursive: true });
    writeFileSync(EVIDENCE, JSON.stringify(evidence, null, 2));

    for (const r of first) expect("unresolved" in r, `registry ${r.runId}`).toBe(false);
    const reports = first as CleanupReport[];
    expect(before.candidates).toHaveLength(27);
    expect(reports.reduce((n, r) => n + (r.deleted["identity.outbox_messages"] ?? 0), 0)).toBe(27);
    expect(reports.flatMap((r) => Object.keys(r.deleted)).filter((t) => t !== "identity.outbox_messages")).toEqual([]);
    expect(after.candidates).toHaveLength(0);
    expect(after.counts.outbox).toBe(before.counts.outbox - 27);
    expect(after.admin).toEqual(before.admin);
    for (const r of reports) {
      expect(r.status, r.runId).not.toBe("incomplete");
      expect(r.residual.confirmed, r.runId).toEqual({});
      expect(r.residual.planLeftovers, r.runId).toEqual({});
      expect(r.adminPreserved, r.runId).toBe(true);
    }
    // Idempotent: a second pass deletes nothing and changes nothing.
    for (const r of second as CleanupReport[]) expect(r.deleted, r.runId).toEqual({});
    expect(again).toEqual({ counts: after.counts, admin: after.admin });
  } finally {
    dropDisposable(CLONE);
  }
});

test("2 real E2E database: one-time cleanup of the validated candidates", () => {
  test.skip(process.env.E2E_MAINT_APPLY !== "restaurant_ai_e2e", "Real cleanup needs E2E_MAINT_APPLY=restaurant_ai_e2e.");
  expect(existsSync(EVIDENCE), "clone validation evidence").toBe(true);
  const validated = JSON.parse(readFileSync(EVIDENCE, "utf8"));
  const db = e2eDatabase();
  expect(db.target.dbName).toBe("restaurant_ai_e2e");
  db.assertIsolated();

  const runs = registeredRuns({ includeDone: true });
  expect(runs).toEqual(validated.runs);
  const before = { counts: counts(db), admin: adminRecords(db), candidates: candidates(db, runs) };
  // Only the exact set validated on the copy may be deleted.
  expect(before.candidates).toEqual(validated.before.candidates);
  expect(before.admin).toEqual(validated.before.admin);
  expect(before.counts).toEqual(validated.before.counts);

  const reports = cleanupRegistries(db, runs, { includeDone: true });
  const after = { counts: counts(db), admin: adminRecords(db), candidates: candidates(db, runs) };
  writeFileSync(path.join(path.dirname(EVIDENCE), "historical-real-evidence.json"), JSON.stringify({ runs, before, reports: summarize(reports), after }, null, 2));

  expect(summarize(reports)).toEqual(validated.first);
  expect(after.counts).toEqual(validated.after.counts);
  expect(after.admin).toEqual(before.admin);
  expect(after.candidates).toHaveLength(0);
});

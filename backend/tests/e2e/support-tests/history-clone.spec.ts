import { expect, test } from "@playwright/test";
import { readdirSync } from "node:fs";
import { RUN_DIR } from "../support/env";
import { cleanupRegistries } from "../support/global-teardown";
import { REGISTRY_FILE } from "../support/registry";
import { createDisposable, dropDisposable, Seed } from "./fixture-db";

/**
 * Applies the cleanup to a full copy of the E2E database for every run recorded in `.run/`, to show
 * what it would do to the historical leftovers. The real E2E database is only read (pg_dump).
 */
const CLONE = "restaurant_ai_historyclone_e2e";

test("historical runs: a copy of the E2E database is cleaned with the verified rules", () => {
  const runs = readdirSync(RUN_DIR, { withFileTypes: true })
    .map((d) => REGISTRY_FILE.exec(d.name)?.[1])
    .filter((r): r is string => !!r);
  test.skip(runs.length === 0, "no run registries in .run/");

  const db = createDisposable(CLONE, { withData: true, marker: false });
  try {
    const seed = new Seed(db);
    const outbox = (sql: string) => seed.count(sql);
    const before = {
      outbox: outbox(`SELECT count(*) FROM identity.outbox_messages;`),
      outboxWithRunId: outbox(`SELECT count(*) FROM identity.outbox_messages WHERE payload::text ~ 'e2e[0-9a-f]{8}';`),
      admin: outbox(`SELECT count(*) FROM identity.outbox_messages WHERE payload::text !~ 'e2e[0-9a-f]{8}';`),
    };
    // The same path global teardown uses, with the completed registries included.
    const reports = cleanupRegistries(db, [...new Set(runs)], { includeDone: true }).map((r) => {
      if ("unresolved" in r) throw new Error(`registry ${r.runId}: ${r.unresolved.join("; ")}`);
      return r;
    });
    const after = {
      outbox: outbox(`SELECT count(*) FROM identity.outbox_messages;`),
      outboxWithRunId: outbox(`SELECT count(*) FROM identity.outbox_messages WHERE payload::text ~ 'e2e[0-9a-f]{8}';`),
    };
    test.info().annotations.push({
      type: "history",
      description: JSON.stringify({ before, after, perRun: reports.map((r) => ({ run: r.runId, status: r.status, deleted: r.deleted, unverified: r.unverified.length, ambiguous: r.residual.ambiguous })) }),
    });
    for (const r of reports) expect(r.status, r.runId).not.toBe("incomplete");
    expect(after.outboxWithRunId).toBe(0);
    expect(after.outbox).toBe(before.admin);
  } finally {
    dropDisposable(CLONE);
  }
});

import { expect, test } from "@playwright/test";
import { E2eDatabase } from "../support/db";
import { parseRegistry, type Owned } from "../support/registry";
import { CONTAINER, createDisposable, dropDisposable, newRunId, Seed, uuid } from "./fixture-db";

/**
 * Cleanup behaviour against a throwaway database built from the E2E schema. Each test uses its own
 * run id and ids, so tests do not see each other's rows.
 */
test.describe.configure({ mode: "serial" });

const FIXTURE = "restaurant_ai_cleanuptest_e2e";
let db: E2eDatabase;
let seed: Seed;

test.beforeAll(() => {
  db = createDisposable(FIXTURE);
  seed = new Seed(db);
});
test.afterAll(() => dropDisposable(FIXTURE));

const outboxFor = (userId: string) => seed.count(`SELECT count(*) FROM identity.outbox_messages WHERE payload ->> 'userId' = :'u';`, { u: userId });
const auditFor = (userId: string) => seed.count(`SELECT count(*) FROM platform.audit_events WHERE payload ->> 'userId' = :'u';`, { u: userId });

/** A verified owner with registration events in both the outbox and the audit log. */
function owner(run: string) {
  const id = uuid();
  const email = `owner.${run}@e2e.test`;
  seed.account(id, email);
  seed.registeredOutbox(id, email);
  seed.registeredAudit(id, email);
  return { record: { kind: "user", id, email } as Owned, id, email };
}

test("1 a test-owned identity.UserRegistered outbox message is removed", () => {
  const run = newRunId();
  const o = owner(run);
  const report = db.cleanupRun(run, [o.record]);
  expect(outboxFor(o.id)).toBe(0);
  expect(report.deleted["identity.outbox_messages"]).toBe(1);
  expect(report.status).toBe("complete");
});

test("2 a test-owned audit event is removed (user registration and restaurant events)", () => {
  const run = newRunId();
  const o = owner(run);
  const tenant = uuid();
  seed.restaurant(tenant, `E2E fixture ${run}`);
  const report = db.cleanupRun(run, [o.record, { kind: "tenant", id: tenant, name: `E2E fixture ${run}` }]);
  expect(auditFor(o.id)).toBe(0);
  expect(seed.count(`SELECT count(*) FROM platform.audit_events WHERE tenant_id = :'t';`, { t: tenant })).toBe(0);
  expect(seed.count(`SELECT count(*) FROM restaurant.qr_codes WHERE tenant_id = :'t';`, { t: tenant })).toBe(0);
  expect(report.deleted["platform.audit_events"]).toBe(2);
});

test("3 the administrator's registration records are preserved", () => {
  const adminId = uuid();
  seed.account(adminId, "admin@fixture.test", true);
  seed.registeredOutbox(adminId, "admin@fixture.test");
  seed.registeredAudit(adminId, "admin@fixture.test");
  const run = newRunId();
  const o = owner(run);
  const report = db.cleanupRun(run, [o.record]);
  expect(report.adminPreserved).toBe(true);
  expect(outboxFor(adminId)).toBe(1);
  expect(auditFor(adminId)).toBe(1);
  expect(seed.count(`SELECT count(*) FROM identity.user_accounts WHERE id = :'a';`, { a: adminId })).toBe(1);
});

test("4 an event whose user id cannot be verified is preserved and reported", () => {
  const run = newRunId();
  const unverified = uuid();
  // Registered in the run's registry, but no account and no event ties it to the run.
  seed.registeredOutbox(unverified, "someone@elsewhere.test");
  const report = db.cleanupRun(run, [{ kind: "user", id: unverified, email: `ghost.${run}@e2e.test` }]);
  expect(outboxFor(unverified)).toBe(1);
  expect(report.unverified.map((u) => u.id)).toContain(unverified);
  expect(report.deleted["identity.outbox_messages"] ?? 0).toBe(0);
});

test("5 an event naming a different run id is preserved and reported", () => {
  const run = newRunId();
  const other = newRunId();
  const o = owner(run);
  // Same (verified) user id, but the event names another run.
  seed.registeredOutbox(o.id, `owner.${other}@e2e.test`);
  const report = db.cleanupRun(run, [o.record]);
  expect(outboxFor(o.id)).toBe(1);
  expect(report.mismatchedEventsKept["identity.outbox_messages"]).toBe(1);
  expect(report.deleted["identity.outbox_messages"]).toBe(1);
});

test("6 an ambiguous stale-run event is retained and reported for review", () => {
  const stale = newRunId();
  const o = owner(stale);
  // Names the stale run, but its user id is not in the stale run's registry.
  const stranger = uuid();
  seed.registeredOutbox(stranger, `stranger.${stale}@e2e.test`);
  const registry = parseRegistry(stale, JSON.stringify(o.record) + "\n");
  expect(registry.errors).toEqual([]);
  const report = db.cleanupRun(stale, registry.records);
  expect(outboxFor(stranger)).toBe(1);
  expect(report.residual.ambiguous["identity.outbox_messages"]).toBe(1);
  expect(report.status).toBe("needs-review");
});

test("7 a residual test-owned outbox message makes verification fail", () => {
  const run = newRunId();
  const o = owner(run);
  const report = db.cleanupRun(run, [o.record], { skipDeleteFor: ["identity.outbox_messages"] });
  expect(report.status).toBe("incomplete");
  expect(report.residual.confirmed["identity.outbox_messages"]).toBe(1);
});

test("8 a residual test-owned audit event makes verification fail", () => {
  const run = newRunId();
  const o = owner(run);
  const report = db.cleanupRun(run, [o.record], { skipDeleteFor: ["platform.audit_events"] });
  expect(report.status).toBe("incomplete");
  expect(report.residual.confirmed["platform.audit_events"]).toBe(1);
});

test("9 a failed deletion transaction rolls back completely", () => {
  const run = newRunId();
  const o = owner(run);
  expect(() => db.cleanupRun(run, [o.record], { failBeforeCommit: true })).toThrow(/division by zero/);
  expect(seed.count(`SELECT count(*) FROM identity.user_accounts WHERE id = :'u';`, { u: o.id })).toBe(1);
  expect(outboxFor(o.id)).toBe(1);
  expect(auditFor(o.id)).toBe(1);
});

test("10 a database without the E2E marker is rejected before any deletion", () => {
  const run = newRunId();
  const o = owner(run);
  db.psql(`DELETE FROM e2e_meta.environment;`);
  try {
    expect(() => db.cleanupRun(run, [o.record])).toThrow(/not marked/);
    expect(outboxFor(o.id)).toBe(1);
  } finally {
    db.psql(`INSERT INTO e2e_meta.environment (marker) VALUES ('restaurant-ai-e2e-isolated');`);
  }
  const bare = createDisposable("restaurant_ai_nomarker_e2e", { schema: false });
  try {
    expect(() => bare.cleanupRun(run, [o.record])).toThrow(/no E2E marker/);
  } finally {
    dropDisposable("restaurant_ai_nomarker_e2e");
  }
});

test("11 an invalid database name is rejected before any SQL runs", () => {
  // A container that does not exist: had any psql been attempted, the error would differ.
  for (const name of ["restaurant_ai", "restaurant_ai_dev", "e2e", "x_e2e; DROP TABLE t", ""]) {
    const target = new E2eDatabase({ dbName: name, pgContainer: "no-such-container" });
    expect(() => target.cleanupRun(newRunId(), [])).toThrow(/Refusing to touch database/);
  }
  expect(CONTAINER).toBeTruthy();
});

test("12 a successful cleanup reports zero confirmed residual rows", () => {
  const run = newRunId();
  const o = owner(run);
  const tenant = uuid();
  seed.restaurant(tenant, `E2E fixture ${run}`);
  const report = db.cleanupRun(run, [o.record, { kind: "tenant", id: tenant, name: `E2E fixture ${run}` }]);
  expect(report.status).toBe("complete");
  expect(report.residual.confirmed).toEqual({});
  expect(report.residual.ambiguous).toEqual({});
  expect(report.residual.planLeftovers).toEqual({});
  expect(report.residual.inspected).toContain("identity.outbox_messages.payload");
  expect(report.residual.inspected).toContain("platform.audit_events.payload");
});

test("13 malformed or foreign registry records are rejected, so nothing is deleted for them", () => {
  const run = newRunId();
  const text = [
    "not json",
    JSON.stringify({ kind: "user", id: "not-a-uuid", email: `x.${run}@e2e.test` }),
    JSON.stringify({ kind: "user", id: uuid(), email: "someone@elsewhere.test" }),
    JSON.stringify({ kind: "tenant", id: uuid(), name: "Spice Route" }),
    JSON.stringify({ kind: "user", id: uuid(), email: "(guest)" }),
  ].join("\n");
  const parsed = parseRegistry(run, text);
  expect(parsed.errors).toHaveLength(4);
  expect(parsed.records).toHaveLength(1);
});

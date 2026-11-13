import { execFileSync } from "node:child_process";
import { env } from "./env";
import { RUN_ID, type Owned } from "./registry";

/**
 * Test-data teardown for the isolated E2E database. Direct SQL lives only here.
 *
 * Safety model
 *  - Every operation first proves the target is the isolated E2E database (name `*_e2e`, not the
 *    development database, marker row present).
 *  - Values reach SQL only as psql variables (`:'name'`), never by string concatenation; run ids and
 *    record ids are validated before that as well.
 *  - A registry entry is not enough to delete anything. Each id must be verified against the database:
 *      tenant: its restaurant row (or, once that is gone, its RestaurantRegistered event) names the run;
 *      user:   its account row (or, once that is gone, its UserRegistered event) has the run in the email.
 *  - Event rows (audit log and outboxes) that mention a different run id are kept and reported.
 *  - All deletes run in one transaction under ON_ERROR_STOP; any error rolls everything back.
 *  - Afterwards an independent scan looks for the run id in every text/JSON column of the database,
 *    so cleanup cannot report success while run-owned rows remain.
 */

export const DEV_DATABASE = "restaurant_ai";
export const MARKER = "restaurant-ai-e2e-isolated";
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;
/** Any run id appearing as a token, not as part of a longer hex string such as a UUID segment. */
const ANY_RUN_TOKEN = "(?:^|[^0-9a-f])(e2e[0-9a-f]{8})(?![0-9a-f])";

export interface DbTarget {
  dbName: string;
  pgContainer: string;
  /** The stack administrator; its records must survive every cleanup. */
  adminEmail?: string;
}

export type CleanupStatus = "complete" | "needs-review" | "incomplete";

export interface CleanupReport {
  runId: string;
  status: CleanupStatus;
  verified: { tenants: number; users: number };
  /** Registry entries that could not be tied to the run; nothing was deleted on their behalf. */
  unverified: { kind: string; id: string; reason: string }[];
  /** Event rows linked to the run's ids but naming another run id; kept. */
  mismatchedEventsKept: Record<string, number>;
  deleted: Record<string, number>;
  residual: {
    inspected: string[];
    /** Rows naming the run id and referencing one of its registered ids: must be zero. */
    confirmed: Record<string, number>;
    /** Rows naming the run id with no link to a registered id: kept for manual review. */
    ambiguous: Record<string, number>;
    /** Rows the ownership rules still match after cleanup: must be zero. */
    planLeftovers: Record<string, number>;
  };
  adminPreserved: boolean | null;
}

/** Test hooks: only the cleanup support tests use these. */
export interface CleanupTestHooks {
  /** Leave these tables out of the delete step, to prove the verification catches residue. */
  skipDeleteFor?: string[];
  /** Raise an error after the deletes and before COMMIT, to prove the transaction rolls back. */
  failBeforeCommit?: boolean;
}

interface Table {
  name: string;
  columns: Set<string>;
  textColumns: string[];
}

export class E2eDatabase {
  constructor(readonly target: DbTarget) {}

  /** psql with ON_ERROR_STOP; values are passed as variables and referenced as :'name' in SQL. */
  psql(sql: string, vars: Record<string, string> = {}, database = this.target.dbName): string[][] {
    const args = ["exec", "-i", this.target.pgContainer, "psql", "-U", "postgres", "-d", database, "-X", "-q", "-v", "ON_ERROR_STOP=1", "-At", "-F", "\t"];
    for (const [name, value] of Object.entries(vars)) {
      if (!/^[a-z_]+$/.test(name) || /[\n\r]/.test(value)) throw new Error(`Invalid psql variable ${name}`);
      args.push("-v", `${name}=${value}`);
    }
    const out = execFileSync("docker", args, { input: sql, encoding: "utf8", stdio: ["pipe", "pipe", "pipe"] });
    return out
      .split("\n")
      .filter((l) => l.length > 0)
      .map((l) => l.split("\t"));
  }

  /** Throws unless the target is the marked, isolated E2E database. Runs before any other statement. */
  assertIsolated(): void {
    const name = this.target.dbName;
    if (!name || !/^[a-z0-9_]+_e2e$/.test(name) || name === DEV_DATABASE) {
      throw new Error(`Refusing to touch database '${name}': not an isolated E2E database.`);
    }
    let rows: string[][];
    try {
      rows = this.psql(
        `SELECT current_database(), (SELECT count(*) FROM e2e_meta.environment WHERE marker = :'marker');`,
        { marker: MARKER },
      );
    } catch {
      throw new Error(`Database '${name}' has no E2E marker; cleanup aborted before any change.`);
    }
    if (rows[0]?.[0] !== name || rows[0]?.[1] !== "1") {
      throw new Error(`Database '${name}' is not marked as the isolated E2E environment; cleanup aborted before any change.`);
    }
  }

  private tables(): Table[] {
    const rows = this.psql(
      `SELECT c.table_schema || '.' || c.table_name, c.column_name, c.data_type
         FROM information_schema.columns c JOIN information_schema.tables t USING (table_schema, table_name)
        WHERE t.table_type = 'BASE TABLE'
          AND c.table_schema NOT IN ('pg_catalog', 'information_schema', 'e2e_meta')
          AND c.table_name <> '__ef_migrations_history'
        ORDER BY 1, c.ordinal_position;`,
    );
    const map = new Map<string, Table>();
    for (const [name, column, type] of rows) {
      const t = map.get(name) ?? { name, columns: new Set<string>(), textColumns: [] };
      t.columns.add(column);
      if (["text", "character varying", "character", "jsonb", "json", "ARRAY"].includes(type)) t.textColumns.push(column);
      map.set(name, t);
    }
    return [...map.values()];
  }

  /** Children before parents, from the database's own foreign keys. */
  private deleteOrder(tables: Table[]): Table[] {
    const edges = this.psql(`SELECT conrelid::regclass::text, confrelid::regclass::text FROM pg_constraint WHERE contype = 'f';`);
    const remaining = [...tables];
    const order: Table[] = [];
    while (remaining.length > 0) {
      const i = remaining.findIndex((t) => !remaining.some((o) => o !== t && edges.some(([child, parent]) => child === o.name && parent === t.name)));
      if (i === -1) throw new Error("Foreign-key cycle among cleanup tables; refusing to guess a delete order.");
      order.push(...remaining.splice(i, 1));
    }
    return order;
  }

  /** Ownership predicate for one table, in terms of the :'vt' (tenant) and :'vu' (user) arrays. */
  private ownershipPredicate(t: Table): string | null {
    const c = t.columns;
    const parts: string[] = [];
    if (t.name === "restaurant.restaurants") parts.push(`id = ANY(:'vt'::uuid[])`);
    if (t.name === "identity.user_accounts") parts.push(`id = ANY(:'vu'::uuid[])`);
    if (c.has("tenant_id")) parts.push(`tenant_id = ANY(:'vt'::uuid[])`);
    for (const col of ["user_id", "customer_id", "actor_id"]) if (c.has(col)) parts.push(`${col} = ANY(:'vu'::uuid[])`);
    if (c.has("payload")) {
      parts.push(`(payload ->> 'userId') = ANY(:'vu'::text[])`, `(payload ->> 'tenantId') = ANY(:'vt'::text[])`);
    }
    if (parts.length === 0) return null;
    const owned = `(${parts.join(" OR ")})`;
    // Event rows naming another run are never deleted on this run's behalf.
    return c.has("payload")
      ? `${owned} AND NOT EXISTS (SELECT 1 FROM regexp_matches(payload::text, :'any_token', 'g') m WHERE m[1] <> :'run_id')`
      : owned;
  }

  /** Verifies each registry entry against the database (see the class comment). */
  private verify(runId: string, owned: Owned[], vars: Record<string, string>) {
    const tenants = owned.filter((o) => o.kind === "tenant").map((o) => o.id);
    const users = owned.filter((o) => o.kind === "user" && o.email !== "(guest)").map((o) => o.id);
    const guests = owned.filter((o) => o.kind === "user" && o.email === "(guest)");
    const rows = this.psql(
      `-- tenants
       SELECT 'tenant', id::text,
              CASE WHEN EXISTS (SELECT 1 FROM restaurant.restaurants r WHERE r.id = x.id AND r.name !~ :'run_re') THEN 'foreign'
                   WHEN EXISTS (SELECT 1 FROM restaurant.restaurants r WHERE r.id = x.id AND r.name ~ :'run_re') THEN 'verified'
                   WHEN EXISTS (SELECT 1 FROM platform.audit_events e WHERE e.type = 'restaurant.RestaurantRegistered' AND e.payload ->> 'tenantId' = x.id::text AND e.payload ->> 'name' ~ :'run_re')
                     OR EXISTS (SELECT 1 FROM restaurant.outbox_messages e WHERE e.type = 'restaurant.RestaurantRegistered' AND e.payload ->> 'tenantId' = x.id::text AND e.payload ->> 'name' ~ :'run_re') THEN 'verified-by-event'
                   ELSE 'no evidence' END
         FROM unnest(:'tenants'::uuid[]) AS x(id);
       -- users
       SELECT 'user', id::text,
              CASE WHEN EXISTS (SELECT 1 FROM identity.user_accounts a WHERE a.id = x.id AND a.email !~ :'run_re') THEN 'foreign'
                   WHEN EXISTS (SELECT 1 FROM identity.user_accounts a WHERE a.id = x.id AND a.email ~ :'run_re') THEN 'verified'
                   WHEN EXISTS (SELECT 1 FROM platform.audit_events e WHERE e.type = 'identity.UserRegistered' AND e.payload ->> 'userId' = x.id::text AND e.payload ->> 'email' ~ :'run_re')
                     OR EXISTS (SELECT 1 FROM identity.outbox_messages e WHERE e.type = 'identity.UserRegistered' AND e.payload ->> 'userId' = x.id::text AND e.payload ->> 'email' ~ :'run_re') THEN 'verified-by-event'
                   ELSE 'no evidence' END
         FROM unnest(:'users'::uuid[]) AS x(id);`,
      { ...vars, tenants: `{${tenants.join(",")}}`, users: `{${users.join(",")}}` },
    );
    const foreign = rows.filter((r) => r[2] === "foreign");
    if (foreign.length > 0) {
      throw new Error(`Cleanup aborted: ${foreign.length} registered record(s) exist but do not carry run id ${runId}.`);
    }
    return {
      tenants: rows.filter((r) => r[0] === "tenant" && r[2].startsWith("verified")).map((r) => r[1]),
      users: rows.filter((r) => r[0] === "user" && r[2].startsWith("verified")).map((r) => r[1]),
      unverified: [
        ...rows.filter((r) => r[2] === "no evidence").map((r) => ({ kind: r[0], id: r[1], reason: "no record or event in the database ties this id to the run" })),
        ...guests.map((g) => ({ kind: "guest", id: g.id, reason: "anonymous guest: no account or email to verify; its rows are covered through the restaurant (tenant) only" })),
      ],
    };
  }

  /** The administrator's own records (account and registration events), which no cleanup may remove. */
  private adminSnapshot(): string | null {
    if (!this.target.adminEmail) return null;
    const rows = this.psql(
      `SELECT (SELECT count(*) FROM identity.user_accounts WHERE email = :'admin_email'),
              (SELECT count(*) FROM identity.outbox_messages WHERE payload ->> 'email' = :'admin_email'),
              (SELECT count(*) FROM platform.audit_events WHERE payload ->> 'email' = :'admin_email');`,
      { admin_email: this.target.adminEmail },
    );
    return rows[0].join("/");
  }

  /** Deletes exactly the verified rows of one run, in one transaction, then verifies independently. */
  cleanupRun(runId: string, owned: Owned[], hooks: CleanupTestHooks = {}): CleanupReport {
    this.assertIsolated();
    if (!RUN_ID.test(runId)) throw new Error(`Invalid run id '${runId}'.`);
    for (const o of owned) if (!UUID.test(o.id)) throw new Error(`Registry id is not a UUID: ${o.id}`);

    const vars = { run_id: runId, run_re: `(^|[^0-9a-f])${runId}([^0-9a-f]|$)`, any_token: ANY_RUN_TOKEN };
    const adminBefore = this.adminSnapshot();
    const v = this.verify(runId, owned, vars);
    const ids = { ...vars, vt: `{${v.tenants.join(",")}}`, vu: `{${v.users.join(",")}}` };

    const tables = this.tables();
    const plan = this.deleteOrder(tables)
      .map((t) => ({ t, where: this.ownershipPredicate(t) }))
      .filter((p): p is { t: Table; where: string } => p.where !== null);

    // Event rows linked to the run's ids that name another run: reported and kept.
    const mismatchedEventsKept: Record<string, number> = {};
    const eventTables = plan.filter((p) => p.t.columns.has("payload"));
    if (eventTables.length > 0) {
      const sql = eventTables
        .map(
          (p) =>
            `SELECT '${p.t.name}', count(*) FROM ${p.t.name} WHERE ((payload ->> 'userId') = ANY(:'vu'::text[]) OR (payload ->> 'tenantId') = ANY(:'vt'::text[])` +
            (p.t.columns.has("tenant_id") ? ` OR tenant_id = ANY(:'vt'::uuid[])` : "") +
            (p.t.columns.has("actor_id") ? ` OR actor_id = ANY(:'vu'::uuid[])` : "") +
            `) AND EXISTS (SELECT 1 FROM regexp_matches(payload::text, :'any_token', 'g') m WHERE m[1] <> :'run_id');`,
        )
        .join("\n");
      for (const [table, count] of this.psql(sql, ids)) if (Number(count) > 0) mismatchedEventsKept[table] = Number(count);
    }

    const deletes = plan
      .filter((p) => !(hooks.skipDeleteFor ?? []).includes(p.t.name))
      .map((p) => `WITH d AS (DELETE FROM ${p.t.name} WHERE ${p.where} RETURNING 1) SELECT '${p.t.name}', count(*) FROM d;`);
    const sql = ["BEGIN;", ...deletes, hooks.failBeforeCommit ? "SELECT 1 / 0;" : "", "COMMIT;"].join("\n");
    const deleted: Record<string, number> = {};
    for (const [table, count] of this.psql(sql, ids)) if (Number(count) > 0) deleted[table] = Number(count);

    // Verification 1: the ownership rules match nothing any more.
    const planLeftovers: Record<string, number> = {};
    const recheck = plan.map((p) => `SELECT '${p.t.name}', count(*) FROM ${p.t.name} WHERE ${p.where};`).join("\n");
    for (const [table, count] of this.psql(recheck, ids)) if (Number(count) > 0) planLeftovers[table] = Number(count);

    // Verification 2: database-wide scan for the run id, independent of the delete rules.
    const residual = this.residualScan(runId, owned, vars);
    const adminAfter = this.adminSnapshot();

    const status: CleanupStatus =
      Object.keys(residual.confirmed).length > 0 || Object.keys(planLeftovers).length > 0
        ? "incomplete"
        : Object.keys(residual.ambiguous).length > 0
          ? "needs-review"
          : "complete";
    return {
      runId,
      status,
      verified: { tenants: v.tenants.length, users: v.users.length },
      unverified: v.unverified,
      mismatchedEventsKept,
      deleted,
      residual: { ...residual, planLeftovers },
      adminPreserved: adminBefore === null ? null : adminBefore === adminAfter,
    };
  }

  /**
   * Looks for the run id in every text, varchar, char, array and JSON column of every table.
   * A hit that also references one of the run's registered ids (tenant, user, actor, payload ids,
   * or the row id of a restaurant/account) is confirmed run-owned; any other hit is ambiguous.
   * This never deletes anything.
   */
  residualScan(runId: string, owned: Owned[], vars?: Record<string, string>) {
    this.assertIsolated();
    const v = vars ?? { run_id: runId, run_re: `(^|[^0-9a-f])${runId}([^0-9a-f]|$)`, any_token: ANY_RUN_TOKEN };
    const all = {
      ...v,
      rt: `{${owned.filter((o) => o.kind === "tenant").map((o) => o.id).join(",")}}`,
      ru: `{${owned.filter((o) => o.kind === "user").map((o) => o.id).join(",")}}`,
    };
    const tables = this.tables().filter((t) => t.textColumns.length > 0);
    const inspected = tables.flatMap((t) => t.textColumns.map((c) => `${t.name}.${c}`));
    const sql = tables
      .map((t) => {
        const hit = t.textColumns.map((c) => `${c}::text ~ :'run_re'`).join(" OR ");
        const refs: string[] = [];
        if (t.name === "restaurant.restaurants") refs.push(`id = ANY(:'rt'::uuid[])`);
        if (t.name === "identity.user_accounts") refs.push(`id = ANY(:'ru'::uuid[])`);
        if (t.columns.has("tenant_id")) refs.push(`tenant_id = ANY(:'rt'::uuid[])`);
        for (const col of ["user_id", "customer_id", "actor_id"]) if (t.columns.has(col)) refs.push(`${col} = ANY(:'ru'::uuid[])`);
        if (t.columns.has("payload")) refs.push(`(payload ->> 'userId') = ANY(:'ru'::text[])`, `(payload ->> 'tenantId') = ANY(:'rt'::text[])`);
        // coalesce: a NULL tenant/actor must not hide a row from both counts.
        const ref = refs.length ? `coalesce((${refs.join(" OR ")}), false)` : "false";
        return `SELECT '${t.name}', count(*) FILTER (WHERE (${ref})), count(*) FILTER (WHERE NOT (${ref})) FROM ${t.name} WHERE ${hit};`;
      })
      .join("\n");
    const confirmed: Record<string, number> = {};
    const ambiguous: Record<string, number> = {};
    for (const [table, c, a] of this.psql(sql, all)) {
      if (Number(c) > 0) confirmed[table] = Number(c);
      if (Number(a) > 0) ambiguous[table] = Number(a);
    }
    return { inspected, confirmed, ambiguous };
  }

  /** Audit events of a run, by type (read before cleanup, for the report). */
  auditEventsOf(owned: Owned[]): Record<string, number> {
    this.assertIsolated();
    const vars = {
      vt: `{${owned.filter((o) => o.kind === "tenant").map((o) => o.id).join(",")}}`,
      vu: `{${owned.filter((o) => o.kind === "user").map((o) => o.id).join(",")}}`,
    };
    return Object.fromEntries(
      this.psql(
        `SELECT type, count(*) FROM platform.audit_events
          WHERE tenant_id = ANY(:'vt'::uuid[]) OR actor_id = ANY(:'vu'::uuid[]) OR (payload ->> 'userId') = ANY(:'vu'::text[]) OR (payload ->> 'tenantId') = ANY(:'vt'::text[])
          GROUP BY 1 ORDER BY 1;`,
        vars,
      ).map(([t, c]) => [t, Number(c)]),
    );
  }
}

/** The configured E2E database (from `.run/stack.env`). */
export function e2eDatabase(): E2eDatabase {
  return new E2eDatabase({ dbName: env.dbName, pgContainer: env.pgContainer, adminEmail: env.adminEmail });
}

/** Read-only fingerprint of the development database, to prove a run did not touch it. */
export function devFingerprint(): string {
  const db = new E2eDatabase({ dbName: DEV_DATABASE, pgContainer: env.pgContainer });
  const [[hash, users, orders, audit, menu, tables]] = db.psql(
    `SELECT md5(coalesce((SELECT string_agg(id::text || name || status || plan_code || ai_enabled::text || ai_knowledge || ai_tone, ',' ORDER BY id) FROM restaurant.restaurants), '')),
            (SELECT count(*) FROM identity.user_accounts), (SELECT count(*) FROM orders.orders), (SELECT count(*) FROM platform.audit_events),
            (SELECT count(*) FROM menu.menu_items), (SELECT count(*) FROM restaurant.dining_tables);`,
  );
  return `restaurants=${hash} users=${users} orders=${orders} audit=${audit} menu=${menu} tables=${tables}`;
}

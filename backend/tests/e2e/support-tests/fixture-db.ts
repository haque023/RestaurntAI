import { execFileSync } from "node:child_process";
import { randomBytes, randomUUID } from "node:crypto";
import { E2eDatabase, MARKER } from "../support/db";

/**
 * Throwaway databases for the cleanup tests. Only these exact names can be created or dropped here,
 * and the source database is only ever read (pg_dump).
 */
export const CONTAINER = process.env.E2E_PG_CONTAINER ?? "restaurant-ai-postgres";
export const SOURCE_DB = process.env.E2E_DB_NAME ?? "restaurant_ai_e2e";
const DISPOSABLE = /^restaurant_ai_(cleanuptest|historyclone|nomarker)_e2e$/;

function superuser(args: string[], input?: string): string {
  return execFileSync("docker", ["exec", "-i", CONTAINER, ...args], { input, encoding: "utf8", stdio: ["pipe", "pipe", "pipe"] });
}

export function dropDisposable(name: string): void {
  if (!DISPOSABLE.test(name)) throw new Error(`Refusing to drop '${name}': not a disposable test database.`);
  superuser(["psql", "-U", "postgres", "-d", "postgres", "-v", "ON_ERROR_STOP=1", "-q", "-c", `DROP DATABASE IF EXISTS "${name}" WITH (FORCE)`]);
}

/** Creates `name` from the E2E database: schema only, or schema and data (`withData`). */
export function createDisposable(name: string, options: { withData?: boolean; marker?: boolean; schema?: boolean } = {}): E2eDatabase {
  dropDisposable(name);
  superuser(["psql", "-U", "postgres", "-d", "postgres", "-v", "ON_ERROR_STOP=1", "-q", "-c", `CREATE DATABASE "${name}" OWNER restaurant_app`]);
  if (options.schema !== false) {
    const dump = options.withData ? "" : "--schema-only";
    superuser(["sh", "-c", `pg_dump -U postgres ${dump} "$1" | psql -U postgres -v ON_ERROR_STOP=1 -q -d "$2"`, "sh", SOURCE_DB, name]);
  }
  const db = new E2eDatabase({ dbName: name, pgContainer: CONTAINER, adminEmail: "admin@fixture.test" });
  if (options.marker !== false && options.schema !== false) {
    db.psql(`INSERT INTO e2e_meta.environment (marker) VALUES (:'m') ON CONFLICT DO NOTHING;`, { m: MARKER });
  }
  return db;
}

export const newRunId = () => `e2e${randomBytes(4).toString("hex")}`;
export const uuid = () => randomUUID();

/** Seed helpers: every value goes in as a psql variable. */
export class Seed {
  constructor(private readonly db: E2eDatabase) {}

  account(id: string, email: string, admin = false) {
    this.db.psql(
      `INSERT INTO identity.user_accounts (id, external_subject, email, display_name, is_platform_administrator, memberships)
       VALUES (:'id', :'id', :'email', 'Fixture', :'admin'::boolean, '[]'::jsonb);`,
      { id, email, admin: String(admin) },
    );
  }

  registeredOutbox(userId: string, email: string) {
    this.db.psql(
      `INSERT INTO identity.outbox_messages (id, type, payload, occurred_at, processed_at, attempts)
       VALUES (gen_random_uuid(), 'identity.UserRegistered', jsonb_build_object('userId', :'uid', 'email', :'email', 'eventId', gen_random_uuid(), 'occurredAt', now()), now(), now(), 0);`,
      { uid: userId, email },
    );
  }

  registeredAudit(userId: string, email: string) {
    this.db.psql(
      `INSERT INTO platform.audit_events (id, type, module, payload, occurred_at, received_at)
       VALUES (gen_random_uuid(), 'identity.UserRegistered', 'identity', jsonb_build_object('userId', :'uid', 'email', :'email', 'eventId', gen_random_uuid(), 'occurredAt', now()), now(), now());`,
      { uid: userId, email },
    );
  }

  restaurant(id: string, name: string) {
    this.db.psql(
      `INSERT INTO restaurant.restaurants (id, name, default_currency, time_zone, status) VALUES (:'id', :'name', 'BDT', 'UTC', 'Active');
       INSERT INTO restaurant.qr_codes (token, tenant_id, branch_id, table_id, table_label) VALUES (gen_random_uuid()::text, :'id', gen_random_uuid(), gen_random_uuid(), 'T1');
       INSERT INTO platform.audit_events (id, type, module, tenant_id, payload, occurred_at, received_at)
       VALUES (gen_random_uuid(), 'restaurant.RestaurantRegistered', 'restaurant', :'id', jsonb_build_object('tenantId', :'id', 'name', :'name'), now(), now());`,
      { id, name },
    );
  }

  count(sql: string, vars: Record<string, string> = {}): number {
    return Number(this.db.psql(sql, vars)[0][0]);
  }
}

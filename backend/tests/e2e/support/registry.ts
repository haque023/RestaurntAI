import { appendFileSync, existsSync, mkdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { RUN_DIR, runId } from "./env";

/**
 * Every record a test creates is written here before anything else happens with it, so teardown
 * can delete exactly what this run created. The registry is a claim, not proof: cleanup still
 * verifies every id against the database before deleting anything.
 */
export type Owned = { kind: "tenant"; id: string; name: string } | { kind: "user"; id: string; email: string };

export const RUN_ID = /^e2e[0-9a-f]{8}$/;
export const REGISTRY_FILE = /^registry-(e2e[0-9a-f]{8})\.jsonl(\.done)?$/;
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/;

export function registryFile(id = runId(), dir = RUN_DIR): string {
  if (!RUN_ID.test(id)) throw new Error(`Invalid run id '${id}'.`);
  return path.join(dir, `registry-${id}.jsonl`);
}

export function track(record: Owned): void {
  mkdirSync(RUN_DIR, { recursive: true });
  appendFileSync(registryFile(), JSON.stringify(record) + "\n");
}

/** Checks every record's shape and that its name or email carries the run id (guests excepted). */
export function parseRegistry(id: string, text: string): { records: Owned[]; errors: string[] } {
  const records: Owned[] = [];
  const errors: string[] = [];
  text
    .split("\n")
    .filter((l) => l.trim().length > 0)
    .forEach((line, i) => {
      let r: any;
      try {
        r = JSON.parse(line);
      } catch {
        errors.push(`line ${i + 1}: not JSON`);
        return;
      }
      if (typeof r?.id !== "string" || !UUID.test(r.id)) errors.push(`line ${i + 1}: id is not a UUID`);
      else if (r.kind === "tenant" && typeof r.name === "string" && r.name.includes(id)) records.push({ kind: "tenant", id: r.id, name: r.name });
      else if (r.kind === "user" && typeof r.email === "string" && (r.email.includes(id) || r.email === "(guest)")) records.push({ kind: "user", id: r.id, email: r.email });
      else errors.push(`line ${i + 1}: unknown kind or name/email without run id ${id}`);
    });
  return { records, errors };
}

/** Reads and validates a run's registry (`registry-<run>.jsonl`, or its `.done` file when asked). */
export function readRegistry(id: string, options: { includeDone?: boolean; dir?: string } = {}): { records: Owned[]; errors: string[] } {
  const base = registryFile(id, options.dir);
  const file = existsSync(base) ? base : options.includeDone && existsSync(`${base}.done`) ? `${base}.done` : null;
  if (!file) return { records: [], errors: [] };
  return parseRegistry(id, readFileSync(file, "utf8"));
}

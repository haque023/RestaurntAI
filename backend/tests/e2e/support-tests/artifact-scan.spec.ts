import { expect, test } from "@playwright/test";
import { existsSync, readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { E2E_ROOT, env, RUN_DIR } from "../support/env";
import { filesContaining, filesUnder } from "../support/artifacts";

/**
 * After a real run: searches the reports, test output and stack logs for the stack administrator's
 * password and every recorded per-run password secret. Prints counts only, never the values.
 */
test("no credential appears in reports, test output or stack logs", () => {
  const secretsDir = path.join(RUN_DIR, "secrets");
  const secrets = existsSync(secretsDir) ? readdirSync(secretsDir).map((f) => readFileSync(path.join(secretsDir, f), "utf8").trim()) : [];
  const needles = [...secrets, ...(env.adminPassword ? [env.adminPassword] : [])].filter((s) => s.length >= 12);
  test.skip(needles.length === 0, "no recorded credentials to search for");
  const files = [
    ...filesUnder(path.join(E2E_ROOT, "test-results")).filter((f) => !f.includes(`${path.sep}support${path.sep}`)),
    ...filesUnder(path.join(E2E_ROOT, "playwright-report")),
    ...filesUnder(path.join(RUN_DIR, "logs")),
    ...filesUnder(RUN_DIR).filter((f) => /run-e2e[0-9a-f]{8}\.json$/.test(f)),
  ];
  const hits = needles.flatMap((n) => filesContaining(files, n).map((f) => path.relative(E2E_ROOT, f)));
  test.info().annotations.push({ type: "scan", description: `${needles.length} credential(s) searched in ${files.length} file(s); hits: ${hits.length}` });
  expect(files.length).toBeGreaterThan(0);
  expect(hits).toEqual([]);
});

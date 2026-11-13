import { expect, test } from "@playwright/test";
import { execFileSync } from "node:child_process";
import { randomBytes } from "node:crypto";
import { rmSync } from "node:fs";
import path from "node:path";
import { artifactContents, filesContaining, filesUnder } from "../support/artifacts";
import { E2E_ROOT } from "../support/env";

/**
 * Runs two deliberately failing browser tests under the suite's artifact policy and searches every
 * produced artifact (report JSON, HTML report, screenshots, error context, attachments) for canary
 * values typed into a password field. The control proves the search would find a leak.
 */
test("a credential typed with fillSecret appears in no artifact; plain fill would leak", () => {
  const out = path.join(E2E_ROOT, "test-results", "support", "redaction-run");
  rmSync(out, { recursive: true, force: true });
  const safe = `canary-${randomBytes(12).toString("hex")}`;
  const raw = `canary-${randomBytes(12).toString("hex")}`;

  let exitCode = 0;
  try {
    execFileSync("npx", ["playwright", "test", "-c", "support-tests/redaction/redaction.config.ts"], {
      cwd: E2E_ROOT,
      env: { ...process.env, REDACTION_OUT: out, CANARY_SAFE: safe, CANARY_RAW: raw },
      stdio: "pipe",
    });
  } catch (e: any) {
    exitCode = e.status ?? 1;
  }
  expect(exitCode, "the inner tests fail on purpose").not.toBe(0);

  const files = filesUnder(out);
  const kinds = {
    reportJson: files.some((f) => f.endsWith("report.json")),
    htmlReport: files.some((f) => f.endsWith("index.html")),
    screenshots: files.filter((f) => f.endsWith(".png")).length,
    errorContext: files.filter((f) => f.endsWith("error-context.md")).length,
    traces: files.filter((f) => f.endsWith(".zip")).length,
    videos: files.filter((f) => f.endsWith(".webm")).length,
  };
  test.info().annotations.push({ type: "artifacts", description: `${files.length} files: ${JSON.stringify(kinds)}` });
  expect(kinds.reportJson && kinds.htmlReport).toBe(true);
  expect(kinds.screenshots).toBeGreaterThanOrEqual(2);
  expect(kinds.errorContext).toBeGreaterThanOrEqual(2);
  expect(kinds.traces).toBe(0);
  expect(kinds.videos).toBe(0);

  // The scanner must see inside the HTML report's embedded archive, or a clean result means nothing.
  expect(files.filter((f) => f.endsWith("index.html")).some((f) => artifactContents(f).length > 1), "HTML report archive decoded").toBe(true);
  const leakedSafe = filesContaining(files, safe);
  const leakedRaw = filesContaining(files, raw);
  test.info().annotations.push({ type: "canary", description: `fillSecret canary in ${leakedSafe.length} file(s); plain-fill control canary in ${leakedRaw.length} file(s): ${leakedRaw.map((f) => path.relative(out, f)).join(", ")}` });
  expect(leakedSafe.map((f) => path.relative(out, f))).toEqual([]);
  expect(leakedRaw.length, "control: the search finds a value typed with plain fill").toBeGreaterThan(0);
  rmSync(out, { recursive: true, force: true });
});

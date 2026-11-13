import { defineConfig, devices } from "@playwright/test";
import { artifactPolicy } from "../../support/artifacts";

/** Child run for the redaction check: the suite's real artifact policy, with deliberate failures. */
const out = process.env.REDACTION_OUT!;
export default defineConfig({
  testDir: ".",
  testMatch: /\.inner\.ts$/,
  outputDir: `${out}/output`,
  workers: 1,
  retries: 0,
  reporter: [["json", { outputFile: `${out}/report.json` }], ["html", { open: "never", outputFolder: `${out}/html` }], ["list"]],
  use: { ...devices["Desktop Chrome"], ...artifactPolicy },
});

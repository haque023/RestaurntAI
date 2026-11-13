import { defineConfig } from "@playwright/test";

/**
 * Cleanup support tests: no stack, no frontends, no global setup. They create and drop their own
 * throwaway databases (restaurant_ai_cleanuptest_e2e, restaurant_ai_historyclone_e2e,
 * restaurant_ai_nomarker_e2e) and only read the E2E database to copy its schema.
 */
export default defineConfig({
  testDir: "support-tests",
  workers: 1,
  fullyParallel: false,
  retries: 0,
  timeout: 120_000,
  outputDir: "test-results/support/output",
  reporter: [["list"], ["json", { outputFile: "test-results/support/support-results.json" }]],
  // Default project: cleanup tests on throwaway databases. The maintenance project (one-time cleanup of
  // completed runs) only runs when selected explicitly: npm run cleanup:historical.
  projects: [
    { name: "support", testIgnore: [/maintenance\//, /redaction\//] },
    { name: "maintenance", testMatch: /maintenance\/.*\.spec\.ts/ },
  ],
});

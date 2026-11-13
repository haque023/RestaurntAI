import { defineConfig, devices } from "@playwright/test";
import path from "node:path";
import { artifactPolicy } from "./support/artifacts";

const FRONTEND = path.resolve(__dirname, "../../../frontend");
const GATEWAY = "http://localhost:6080";

// Each frontend app on its own port, proxying /api to the isolated E2E gateway.
const app = (workspace: string, port: number) => ({
  command: `npm run dev -w ${workspace} -- --port ${port} --strictPort`,
  cwd: FRONTEND,
  url: `http://localhost:${port}`,
  reuseExistingServer: false,
  timeout: 60_000,
  env: { VITE_GATEWAY_URL: GATEWAY, VITE_CONSUMER_URL: "http://localhost:4100", VITE_RESTAURANT_CONSOLE_URL: "http://localhost:4101" },
});

// The API project needs no frontend servers; `npm run test:api` sets E2E_NO_WEB=true.
const startFrontends = process.env.E2E_NO_WEB !== "true";

export default defineConfig({
  testDir: ".",
  // One worker: the API suite is a single ordered journey, and runs share one isolated database.
  workers: 1,
  fullyParallel: false,
  retries: 0,
  timeout: 240_000,
  expect: { timeout: 15_000 },
  reporter: [["list"], ["json", { outputFile: "test-results/results.json" }], ["html", { open: "never", outputFolder: "playwright-report" }]],
  globalSetup: "./support/global-setup.ts",
  globalTeardown: "./support/global-teardown.ts",
  use: { ...artifactPolicy },
  projects: [
    { name: "api", testMatch: /api\/.*\.spec\.ts/ },
    { name: "browser", testMatch: /browser\/.*\.spec\.ts/, use: { ...devices["Desktop Chrome"] } },
  ],
  webServer: startFrontends
    ? [app("@restaurant-ai/consumer-web", 4100), app("@restaurant-ai/restaurant-web", 4101), app("@restaurant-ai/admin-web", 4102)]
    : undefined,
});

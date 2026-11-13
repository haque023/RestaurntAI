import { expect, test } from "@playwright/test";
import { fillSecret } from "../../browser/helpers";

/** Both tests fail on purpose, so every on-failure artifact is produced. Values are canaries, not credentials. */
const FORM = `<form aria-label="Sign in"><label>Email <input type="email"></label><label>Password <input type="password"></label><p id="status">signed out</p></form>`;

test("credential typed with fillSecret, then a failure", async ({ page }) => {
  await page.setContent(FORM);
  await page.getByLabel("Email").fill("someone@e2e.test");
  await fillSecret(page.getByLabel("Password"), process.env.CANARY_SAFE!);
  console.log("console output from the page under test");
  await expect(page.locator("#status")).toHaveText("signed in", { timeout: 1_000 });
});

test("control: credential typed with locator.fill, then a failure", async ({ page }) => {
  await page.setContent(FORM);
  await page.getByLabel("Password").fill(process.env.CANARY_RAW!);
  await expect(page.locator("#status")).toHaveText("signed in", { timeout: 1_000 });
});

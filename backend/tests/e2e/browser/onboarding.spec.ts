import { expect, test } from "@playwright/test";
import { Api } from "../support/api";
import { env, runId, testPassword } from "../support/env";
import { track } from "../support/registry";
import { fillSecret, newTest, signIn } from "./helpers";

/** Sign-up in the restaurant console → approval in the admin panel → restaurant setup in the console. */
test.describe.configure({ mode: "serial" });

const owner = { email: "", password: "", name: "" };

test("restaurant signs up in the console and waits for verification", async ({ page }) => {
  newTest("restaurant self sign-up through the UI");
  owner.email = `owner.ui.${runId()}@e2e.test`;
  owner.password = testPassword("owner");
  owner.name = `E2E UI ${runId()}`;

  await page.goto(`${env.restaurantUrl}/signup`);
  await page.getByLabel("Restaurant name").fill(owner.name);
  await page.getByLabel("Your name").fill("E2E UI Owner");
  await page.getByLabel("Email").fill(owner.email);
  await fillSecret(page.getByLabel("Password"), owner.password);
  await page.getByRole("button", { name: "Create restaurant" }).click();

  await expect(page).toHaveURL(/\/restaurant$/);
  await expect(page.getByRole("heading", { name: owner.name })).toBeVisible();
  await expect(page.getByText("Not listed")).toBeVisible();
  await expect(page.getByRole("button", { name: "List in discovery" })).toBeDisabled();

  // Register what the UI created so teardown removes exactly it.
  const api = await Api.create();
  const session = await api.ok("POST", "/api/auth/login", 200, { data: { email: owner.email, password: owner.password, scope: "Staff" } });
  track({ kind: "user", id: session.user.id, email: owner.email });
  track({ kind: "tenant", id: session.memberships[0].tenantId, name: owner.name });
  const current = await api.ok("GET", "/api/restaurants/current", 200, { token: session.accessToken });
  expect(current.status).toBe("PendingVerification");
  await api.dispose();
});

test("platform admin approves the restaurant in the admin panel", async ({ page }) => {
  newTest("admin approval through the UI");
  await signIn(page, env.adminUrl, env.adminEmail, env.adminPassword);
  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Restaurants" }).click();
  await page.getByRole("button", { name: "Awaiting verification" }).click();
  await page.getByRole("link", { name: owner.name }).click();
  await page.getByRole("button", { name: "Approve" }).click();
  await expect(page.getByRole("button", { name: "Approve" })).toHaveCount(0);
  await expect(page.locator(".page-header .badge")).toHaveText("Verified");
});

test("owner sets up a branch, listing, menu item and table with a QR code", async ({ page }) => {
  newTest("restaurant setup through the UI");
  await signIn(page, env.restaurantUrl, owner.email, owner.password);

  await page.goto(`${env.restaurantUrl}/restaurant`);
  const addBranch = page.locator("form", { has: page.getByRole("button", { name: "Add branch" }) });
  await addBranch.getByLabel("Name").fill("Banani");
  await addBranch.getByLabel("Address").fill("Road 11, Banani, Dhaka");
  await addBranch.getByRole("button", { name: "Add branch" }).click();
  await expect(page.getByRole("cell", { name: /Banani/ }).first()).toBeVisible();
  await page.getByRole("button", { name: "List in discovery" }).click();
  await expect(page.getByText("Listed in discovery").first()).toBeVisible();

  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Menu", exact: true }).click();
  const dish = `Kacchi ${runId()}`;
  // Synchronization: right after the click, getByLabel("Name") can still match the Restaurant page's
  // "Add branch" field. Wait for a Menu-only heading before using the form (root cause unconfirmed; see README).
  await expect(page.getByRole("heading", { name: "No menu items yet" })).toBeVisible();
  await page.getByLabel("Name").fill(dish);
  await page.getByLabel("Category").fill("Mains");
  await page.getByLabel(/^Price/).fill("450");
  await page.getByRole("button", { name: "Add item" }).click();
  await expect(page.getByRole("cell", { name: new RegExp(dish) })).toBeVisible();

  await page.getByRole("navigation", { name: "Main" }).getByRole("link", { name: "Tables & QR" }).click();
  await page.getByLabel("Label").fill("T1");
  await page.getByRole("button", { name: "Add table" }).click();
  await expect(page.getByRole("heading", { name: "Table T1" })).toBeVisible();
  await expect(page.getByRole("img", { name: /QR code for http:\/\/localhost:4100\/t\// })).toBeVisible();
  await expect(page.getByRole("link", { name: /Open as a guest/ })).toHaveAttribute("href", /\/t\/[A-Za-z0-9_-]{16,}$/);
});

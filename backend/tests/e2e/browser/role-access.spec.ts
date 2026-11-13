import { expect, test } from "@playwright/test";
import { Api, provisionRestaurant } from "../support/api";
import { env, testPassword } from "../support/env";
import { fillSecret, newTest, signIn } from "./helpers";

/** What each role can reach in the UI; the APIs enforce the same rules (see the API suite). */
let fixture: Awaited<ReturnType<typeof provisionRestaurant>>;
let kitchen: { email: string; password: string };

test.beforeAll(async () => {
  const api = await Api.create();
  fixture = await provisionRestaurant(api, "roles");
  kitchen = await api.addStaff(fixture.owner.token, "kitchen.roles", "kitchen_staff", fixture.branchId);
  await api.dispose();
});

test("kitchen staff see only their menus and are refused owner pages", async ({ page }) => {
  newTest("role-based navigation and page guards");
  await signIn(page, env.restaurantUrl, kitchen.email, kitchen.password);
  const nav = page.getByRole("navigation", { name: "Main" });
  await expect(nav.getByRole("link", { name: "Orders" })).toBeVisible();
  for (const hidden of ["Dashboard", "Staff", "Restaurant", "AI waiter"]) {
    await expect(nav.getByRole("link", { name: hidden, exact: true })).toHaveCount(0);
  }
  await page.goto(`${env.restaurantUrl}/staff`);
  await expect(page.getByRole("alert")).toHaveText("You do not have access to this page.");
});

test("a restaurant owner cannot sign in to the platform admin panel", async ({ page }) => {
  newTest("admin panel refuses non-administrators");
  await page.goto(`${env.adminUrl}/login`);
  const form = page.getByRole("form", { name: "Sign in" });
  await form.getByLabel("Email").fill(fixture.owner.email);
  await fillSecret(form.getByLabel("Password"), testPassword("owner"));
  await form.getByRole("button", { name: "Sign in" }).click();
  await expect(form.getByRole("alert")).toContainText("not a platform administrator");
});

test("signed-out visitors are sent to sign-in", async ({ page }) => {
  newTest("console requires sign-in");
  await page.goto(`${env.restaurantUrl}/orders`);
  await expect(page).toHaveURL(/\/login$/);
});

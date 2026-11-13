import { expect, test } from "@playwright/test";
import { Api, provisionRestaurant } from "../support/api";
import { env, LIVE_CLAUDE_SKIP_REASON } from "../support/env";
import { newTest, signIn } from "./helpers";

/** A guest scans the table QR, orders and asks for the bill; the waiter runs the kitchen flow on the board. */
test.describe.configure({ mode: "serial" });

let fixture: Awaited<ReturnType<typeof provisionRestaurant>>;
let waiter: { email: string; password: string };

test.beforeAll(async () => {
  const api = await Api.create();
  fixture = await provisionRestaurant(api, "qr");
  waiter = await api.addStaff(fixture.owner.token, "waiter.qr", "waiter", fixture.branchId);
  await api.dispose();
});

test("guest orders from the table QR and the waiter serves it", async ({ browser }) => {
  newTest("QR ordering and kitchen workflow through the UI");
  const guest = await (await browser.newContext()).newPage();
  await guest.goto(`${env.consumerUrl}/t/${fixture.table.qrToken}`);
  await expect(guest.getByText("you're at a table")).toBeVisible();

  await guest.getByRole("button", { name: `Add ${fixture.item.name} to cart` }).click();
  await guest.getByRole("link", { name: /Cart \(1\)/ }).click();
  await expect(guest.getByRole("heading", { name: "Your cart" })).toBeVisible();
  await guest.getByRole("button", { name: "Place order" }).click();
  await expect(guest).toHaveURL(/\/orders\/[0-9a-f-]{36}$/);
  await expect(guest.locator(".page-header .badge")).toHaveText("Placed");

  await guest.goto(`${env.consumerUrl}/t/${fixture.table.qrToken}`);
  await guest.getByRole("button", { name: /Request the bill/ }).click();
  await expect(guest.getByRole("button", { name: "Bill requested" })).toBeVisible();

  const staff = await (await browser.newContext()).newPage();
  await signIn(staff, env.restaurantUrl, waiter.email, waiter.password);
  await expect(staff).toHaveURL(/\/orders$/);
  await expect(staff.getByRole("region", { name: "Guests asking for staff" }).getByText("Wants the bill")).toBeVisible();

  const ticket = staff.locator("article.ticket", { hasText: fixture.item.name });
  for (const action of ["Accept", "Start cooking", "Ready", "Served"]) {
    await ticket.getByRole("button", { name: action, exact: true }).click();
    await expect(ticket.getByRole("button", { name: action, exact: true })).toHaveCount(0);
  }
  await expect(staff.getByRole("region", { name: "Served" }).locator("article.ticket", { hasText: fixture.item.name })).toBeVisible();

  await guest.goto(`${env.consumerUrl}/r/${fixture.owner.tenantId}/${fixture.branchId}/orders`);
  await expect(guest.getByText("Served").first()).toBeVisible();
});

// Live policy for the browser: one attempt, no retry (a retry would send a second chat message), and up
// to 120 s for the reply. The API live tests differ: 180 s per call and one retry on a 5xx (see README).
test("AI waiter answers a guest at the table", async ({ page }) => {
  newTest("AI waiter chat through the UI");
  test.skip(!env.liveClaude, LIVE_CLAUDE_SKIP_REASON);
  test.info().annotations.push({ type: "provider", description: "real Claude call (AI waiter)" });
  await page.goto(`${env.consumerUrl}/t/${fixture.table.qrToken}`);
  await page.getByRole("button", { name: "Ask the AI waiter" }).click();
  await page.getByRole("textbox", { name: "Message AI waiter" }).fill("What vegan dishes do you have?");
  await page.getByRole("button", { name: "Send" }).click();
  await expect(page.locator(".chat-bubble-assistant").first()).toContainText(/fuchka/i, { timeout: 120_000 });
});

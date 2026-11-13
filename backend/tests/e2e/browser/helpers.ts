import { expect, test, type Locator, type Page } from "@playwright/test";

/** Every browser test is new: the historical suite had no browser automation. */
export const newTest = (note: string) => test.info().annotations.push({ type: "provenance", description: `new: ${note}` });

/** Email/password sign-in through the app's own form. */
export async function signIn(page: Page, appUrl: string, email: string, password: string) {
  await page.goto(`${appUrl}/login`);
  const form = page.getByRole("form", { name: "Sign in" });
  await form.getByLabel("Email").fill(email);
  await fillSecret(form.getByLabel("Password"), password);
  await form.getByRole("button", { name: "Sign in" }).click();
  await expect(page).not.toHaveURL(/\/login$/);
}

/**
 * Types a credential without exposing it: `locator.fill(value)` puts the value in the step title, and
 * so in the HTML/JSON reports. This sets the value with the native setter inside the page and fires
 * the input event React listens to; the step is reported as "Evaluate" without its argument.
 * Verified by support-tests/redaction.spec.ts.
 */
export async function fillSecret(field: Locator, value: string): Promise<void> {
  const typed = await field.evaluate((element, secret) => {
    const input = element as HTMLInputElement;
    Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, "value")!.set!.call(input, secret);
    input.dispatchEvent(new Event("input", { bubbles: true }));
    return input.value.length;
  }, value);
  // Compare lengths only, so a failure message can never echo the credential.
  expect(typed, "credential field was filled").toBe(value.length);
}

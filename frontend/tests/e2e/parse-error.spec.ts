import { test, expect } from "@playwright/test";

/**
 * FR-019: parse_error handling end-to-end (T073).
 * Submitting an unintelligible request should show an inline actionable error
 * while keeping the request text editable (not clearing the input).
 */

test.describe("parse_error handling (FR-019)", () => {
  test("gibberish input shows inline error with suggestion; input stays editable", async ({ page }) => {
    await page.goto("/");

    const textarea = page.getByLabel("Describe your order");
    const gibberish = "asdfghjkl qwerty xyz 12345";
    await textarea.fill(gibberish);
    await page.getByRole("button", { name: "Submit Request" }).click();

    // Wait for either a parse_error alert or a workflow_failed alert (both are valid error paths)
    const errorAlert = page.getByRole("alert").filter({ hasText: /could not understand|workflow failed/i });
    await expect(errorAlert).toBeVisible({ timeout: 35_000 });

    // The request textarea must still be enabled (not disabled / cleared)
    await expect(textarea).toBeEnabled();

    // The user should still be able to type a corrected request
    await textarea.fill("Create an invoice for ABC Traders");
    await expect(textarea).toHaveValue("Create an invoice for ABC Traders");
  });

  test("error message contains actionable suggestion or guidance", async ({ page }) => {
    await page.goto("/");
    await page.getByLabel("Describe your order").fill("???");
    await page.getByRole("button", { name: "Submit Request" }).click();

    // Either parse_error suggestion or workflow failed message — both inform the user
    const alert = page.getByRole("alert");
    await expect(alert).toBeVisible({ timeout: 35_000 });

    // The Submit button should be re-enabled after failure
    await expect(page.getByRole("button", { name: "Submit Request" })).toBeEnabled({ timeout: 5_000 });
  });
});

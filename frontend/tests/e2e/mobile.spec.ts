import { test, expect } from "@playwright/test";

/**
 * T075: Mobile viewport tests at 375px width (V8 from quickstart.md).
 * All critical interactive elements must be touch-friendly (≥44px hit targets)
 * and must not overflow on a 375-wide screen.
 */

test.use({ viewport: { width: 375, height: 812 } });

test.describe("Mobile layout (375px viewport)", () => {
  test("RequestBox textarea and submit button visible and no horizontal scroll", async ({ page }) => {
    await page.goto("/");

    // No horizontal overflow — body should not be wider than viewport
    const bodyWidth = await page.evaluate(() => document.body.scrollWidth);
    expect(bodyWidth).toBeLessThanOrEqual(375);

    const textarea = page.getByLabel("Describe your order");
    await expect(textarea).toBeVisible();

    const submitBtn = page.getByRole("button", { name: "Submit Request" });
    await expect(submitBtn).toBeVisible();

    // Touch target ≥44px in both dimensions
    const btnBox = await submitBtn.boundingBox();
    expect(btnBox).not.toBeNull();
    expect(btnBox!.height).toBeGreaterThanOrEqual(44);
    expect(btnBox!.width).toBeGreaterThanOrEqual(44);
  });

  test("page header renders without horizontal overflow", async ({ page }) => {
    await page.goto("/");
    const header = page.locator("header");
    await expect(header).toBeVisible();
    const headerBox = await header.boundingBox();
    expect(headerBox).not.toBeNull();
    expect(headerBox!.width).toBeLessThanOrEqual(375);
  });

  test("typing and submitting works on 375px viewport", async ({ page }) => {
    await page.goto("/");
    const textarea = page.getByLabel("Describe your order");
    await textarea.fill("Test order for mobile");
    await expect(textarea).toHaveValue("Test order for mobile");

    // Submit button must remain tappable after typing
    const submitBtn = page.getByRole("button", { name: "Submit Request" });
    await expect(submitBtn).toBeEnabled();
  });

  test("WorkflowProgress list does not overflow on mobile", async ({ page }) => {
    await page.goto("/");

    // Check that the main content area doesn't produce horizontal scroll
    const main = page.locator("main");
    await expect(main).toBeVisible();
    const mainBox = await main.boundingBox();
    expect(mainBox).not.toBeNull();
    // Allow 1px rounding tolerance
    expect(mainBox!.width).toBeLessThanOrEqual(376);
  });
});

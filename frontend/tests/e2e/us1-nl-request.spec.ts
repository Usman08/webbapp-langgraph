import { test, expect } from "@playwright/test";

/**
 * US1 acceptance scenario V1: ABC Traders → correct AI-generated draft within 30s.
 * US1 acceptance scenario V2: out-of-stock line flagged with alternative.
 *
 * These tests require the full stack (backend + ai-engine + postgres) to be running.
 * Run with: PLAYWRIGHT_BASE_URL=http://localhost:5173 npx playwright test
 */

test.describe("US1: Natural Language Invoice Request", () => {
  test("V1 – submits NL request and receives a draft invoice within 30s", async ({ page }) => {
    await page.goto("/");

    const textarea = page.getByLabel("Describe your order");
    await textarea.fill(
      "Create an invoice for ABC Traders. Same products as last month. Increase quantities by 20% and apply the usual discount."
    );

    await page.getByRole("button", { name: "Submit Request" }).click();

    // Wait up to 30 seconds for the draft invoice to appear (SC-001)
    await expect(page.getByLabel("Invoice preview")).toBeVisible({ timeout: 30_000 });

    // Verify the customer name appears
    await expect(page.getByText("ABC Traders")).toBeVisible();

    // Verify totals section is rendered
    await expect(page.getByText("Total")).toBeVisible();
  });

  test("V2 – out-of-stock line is flagged in the draft", async ({ page }) => {
    await page.goto("/");

    await page.getByLabel("Describe your order").fill(
      "Invoice for Global Supplies Ltd. 2 units of Monitor 24 and 3 Laptops."
    );
    await page.getByRole("button", { name: "Submit Request" }).click();

    await expect(page.getByLabel("Invoice preview")).toBeVisible({ timeout: 30_000 });

    // Monitor 24" is out of stock — should show an alternative flag
    const altBadge = page.getByText("Alt. Suggested").or(page.getByText("Back Order"));
    await expect(altBadge).toBeVisible();
  });
});

import { test, expect } from "@playwright/test";

/**
 * US4 acceptance scenarios (T072):
 * V5 – reject → edit → approve → appears in history; full journey under 2 minutes (SC-004).
 * V6 – approved invoice persists across page reload.
 *
 * Requires the full stack running: PLAYWRIGHT_BASE_URL=http://localhost:5173 npx playwright test
 */

const REQUEST_TEXT =
  "Create an invoice for ABC Traders. Same products as last month. Apply the usual discount.";

test.describe("US4: Human-in-the-Loop Approval Gate", () => {
  test.setTimeout(120_000); // SC-004: full journey under 2 minutes

  test("V5 – reject, edit quantity, approve, verify in history", async ({ page }) => {
    const start = Date.now();

    await page.goto("/");

    // Submit a request and wait for the draft invoice
    await page.getByLabel("Describe your order").fill(REQUEST_TEXT);
    await page.getByRole("button", { name: "Submit Request" }).click();

    await expect(page.getByLabel("Invoice preview")).toBeVisible({ timeout: 35_000 });

    // Approval gate must be visible
    const approvalGate = page.getByLabel("Approval gate");
    await expect(approvalGate).toBeVisible({ timeout: 5_000 });

    // Click "Reject & Edit"
    await approvalGate.getByRole("button", { name: /reject.*edit/i }).click();

    // Edit invoice lines panel should appear
    const editPanel = page.getByLabel("Edit invoice lines");
    await expect(editPanel).toBeVisible({ timeout: 3_000 });

    // Modify the first quantity input (set to 5)
    const firstQtyInput = editPanel.locator("input[type=number]").first();
    await firstQtyInput.fill("5");

    // Save changes
    await editPanel.getByRole("button", { name: "Save Changes" }).click();

    // Approval gate reappears after save
    await expect(page.getByLabel("Approval gate")).toBeVisible({ timeout: 5_000 });

    // Click Approve
    await page.getByLabel("Approval gate").getByRole("button", { name: "Approve" }).click();

    // Confirm dialog appears — confirm
    await page.getByRole("button", { name: /yes.*approve/i }).click();

    // Finalised banner should appear
    await expect(page.getByText("Invoice finalised.")).toBeVisible({ timeout: 5_000 });

    // History section should show the finalised invoice
    const history = page.getByLabel("Invoice history");
    await expect(history).toBeVisible();
    await expect(history.getByText("Finalised")).toBeVisible({ timeout: 5_000 });

    // SC-004: full journey under 2 minutes
    const elapsed = (Date.now() - start) / 1000;
    expect(elapsed).toBeLessThan(120);
  });

  test("V6 – approved invoice persists across page reload", async ({ page }) => {
    await page.goto("/");

    // Submit and wait for draft
    await page.getByLabel("Describe your order").fill(REQUEST_TEXT);
    await page.getByRole("button", { name: "Submit Request" }).click();
    await expect(page.getByLabel("Invoice preview")).toBeVisible({ timeout: 35_000 });

    // Approve directly
    await expect(page.getByLabel("Approval gate")).toBeVisible({ timeout: 5_000 });
    await page.getByLabel("Approval gate").getByRole("button", { name: "Approve" }).click();
    await page.getByRole("button", { name: /yes.*approve/i }).click();
    await expect(page.getByText("Invoice finalised.")).toBeVisible({ timeout: 5_000 });

    // Reload the page
    await page.reload();

    // History section should still show the finalised invoice
    const history = page.getByLabel("Invoice history");
    await expect(history).toBeVisible({ timeout: 5_000 });
    await expect(history.getByText("Finalised")).toBeVisible({ timeout: 5_000 });
  });
});

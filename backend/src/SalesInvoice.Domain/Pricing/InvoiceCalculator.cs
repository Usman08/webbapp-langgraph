using SalesInvoice.Domain.Enums;

namespace SalesInvoice.Domain.Pricing;

public record LineInput(int Quantity, decimal UnitPrice, LineStockStatus StockStatus);

public record InvoiceTotals(
    decimal Subtotal,
    decimal DiscountPercentage,
    decimal DiscountAmount,
    decimal TaxPercentage,
    decimal TaxAmount,
    decimal Total);

/// <summary>
/// Computes invoice totals from line items (SC-006: rounding error ≤ 0.01).
/// BackOrder lines are excluded from subtotal unless explicitly retained.
/// </summary>
public static class InvoiceCalculator
{
    public static InvoiceTotals Calculate(
        IEnumerable<LineInput> lines,
        decimal discountPercentage,
        decimal taxPercentage,
        bool includeBackOrder = false)
    {
        var subtotal = lines
            .Where(l => l.StockStatus != LineStockStatus.BackOrder || includeBackOrder)
            .Sum(l => l.Quantity * l.UnitPrice);

        var discountAmount = Math.Round(subtotal * discountPercentage / 100m, 2, MidpointRounding.AwayFromZero);
        var taxBase = subtotal - discountAmount;
        var taxAmount = Math.Round(taxBase * taxPercentage / 100m, 2, MidpointRounding.AwayFromZero);
        var total = taxBase + taxAmount;

        return new InvoiceTotals(subtotal, discountPercentage, discountAmount, taxPercentage, taxAmount, total);
    }
}

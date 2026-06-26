namespace SalesInvoice.Domain.Pricing;

/// <summary>
/// Adjusts a quantity by a percentage delta, rounding half-up to a whole unit (FR-017).
/// </summary>
public static class QuantityCalculator
{
    public static int Adjust(int previousQuantity, decimal deltaPercent)
    {
        var exact = previousQuantity * (1 + deltaPercent / 100m);
        return (int)Math.Floor(exact + 0.5m);
    }
}

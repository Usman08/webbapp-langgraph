using FluentAssertions;
using SalesInvoice.Domain.Enums;
using SalesInvoice.Domain.Pricing;

namespace SalesInvoice.UnitTests.Pricing;

public class InvoiceCalculatorTests
{
    [Fact]
    public void Calculate_NoDiscount_NoTax_ReturnsCorrectTotals()
    {
        var lines = new[] { new LineInput(10, 5.00m, LineStockStatus.InStock) };
        var result = InvoiceCalculator.Calculate(lines, 0, 0);

        result.Subtotal.Should().Be(50.00m);
        result.DiscountAmount.Should().Be(0);
        result.TaxAmount.Should().Be(0);
        result.Total.Should().Be(50.00m);
    }

    [Fact]
    public void Calculate_WithDiscount_ReducesTotal()
    {
        var lines = new[] { new LineInput(10, 10.00m, LineStockStatus.InStock) };
        var result = InvoiceCalculator.Calculate(lines, 10, 0);

        result.Subtotal.Should().Be(100.00m);
        result.DiscountAmount.Should().Be(10.00m);
        result.Total.Should().Be(90.00m);
    }

    [Fact]
    public void Calculate_WithTax_CorrectlyAppliedAfterDiscount()
    {
        var lines = new[] { new LineInput(2, 100.00m, LineStockStatus.InStock) };
        var result = InvoiceCalculator.Calculate(lines, 10, 15);

        result.Subtotal.Should().Be(200.00m);
        result.DiscountAmount.Should().Be(20.00m);
        result.TaxAmount.Should().Be(27.00m);    // (200 - 20) * 0.15
        result.Total.Should().Be(207.00m);
    }

    [Fact]
    public void Calculate_BackOrderExcludedByDefault()
    {
        var lines = new[]
        {
            new LineInput(5, 10.00m, LineStockStatus.InStock),
            new LineInput(3, 10.00m, LineStockStatus.BackOrder),
        };
        var result = InvoiceCalculator.Calculate(lines, 0, 0);

        result.Subtotal.Should().Be(50.00m);
    }

    [Fact]
    public void Calculate_BackOrderIncludedWhenRetained()
    {
        var lines = new[]
        {
            new LineInput(5, 10.00m, LineStockStatus.InStock),
            new LineInput(3, 10.00m, LineStockStatus.BackOrder),
        };
        var result = InvoiceCalculator.Calculate(lines, 0, 0, includeBackOrder: true);

        result.Subtotal.Should().Be(80.00m);
    }

    [Fact]
    public void Calculate_RoundingError_WithinTolerance()
    {
        // Verify that rounding error stays ≤ 0.01 (SC-006)
        var lines = new[] { new LineInput(3, 0.10m, LineStockStatus.InStock) };
        var result = InvoiceCalculator.Calculate(lines, 33, 10);

        var manualTotal = result.Subtotal - result.DiscountAmount + result.TaxAmount;
        Math.Abs(result.Total - manualTotal).Should().BeLessThanOrEqualTo(0.01m);
    }
}

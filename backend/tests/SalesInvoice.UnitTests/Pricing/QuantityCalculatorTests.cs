using FluentAssertions;
using SalesInvoice.Domain.Pricing;

namespace SalesInvoice.UnitTests.Pricing;

public class QuantityCalculatorTests
{
    [Theory]
    [InlineData(10, 20, 12)]   // 10 * 1.2 = 12.0 → 12
    [InlineData(5, 20, 6)]     // 5 * 1.2 = 6.0 → 6
    [InlineData(3, 20, 4)]     // 3 * 1.2 = 3.6 → rounds up to 4
    [InlineData(7, 20, 8)]     // 7 * 1.2 = 8.4 → rounds down to 8? No — 8.4 + 0.5 = 8.9 → floor = 8
    [InlineData(4, 25, 5)]     // 4 * 1.25 = 5.0 → 5
    [InlineData(1, 50, 2)]     // 1 * 1.5 = 1.5 → half-up → 2
    [InlineData(2, 50, 3)]     // 2 * 1.5 = 3.0 → 3
    [InlineData(0, 20, 0)]     // 0 → 0
    public void Adjust_ReturnsHalfUpRoundedWholeUnit(int prev, decimal delta, int expected)
    {
        QuantityCalculator.Adjust(prev, delta).Should().Be(expected);
    }

    [Fact]
    public void Adjust_NegativeDelta_ReducesQuantity()
    {
        QuantityCalculator.Adjust(10, -20).Should().Be(8); // 10 * 0.8 = 8
    }
}

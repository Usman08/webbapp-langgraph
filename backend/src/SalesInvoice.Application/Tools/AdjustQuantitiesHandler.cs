using SalesInvoice.Domain.Pricing;

namespace SalesInvoice.Application.Tools;

public record AdjustLine(Guid ProductId, int Quantity);

public record AdjustQuantitiesRequest(List<AdjustLine> Lines, decimal DeltaPercent);

public record AdjustQuantitiesResponse(List<AdjustLine> Lines);

public class AdjustQuantitiesHandler
{
    public static AdjustQuantitiesResponse Handle(AdjustQuantitiesRequest request)
    {
        var adjusted = request.Lines
            .Select(l => new AdjustLine(l.ProductId, QuantityCalculator.Adjust(l.Quantity, request.DeltaPercent)))
            .ToList();
        return new AdjustQuantitiesResponse(adjusted);
    }
}

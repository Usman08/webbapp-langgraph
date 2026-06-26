using SalesInvoice.Domain.Entities;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record SaveRecommendationRequest(Guid WorkflowRunId, Guid ProductId, string Sku, string Basis);

public record SaveRecommendationResponse(Guid RecommendationId, string Sku, string Basis);

public class SaveRecommendationHandler(AppDbContext db)
{
    public async Task<SaveRecommendationResponse> HandleAsync(SaveRecommendationRequest request)
    {
        var rec = new ProductRecommendation
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = request.WorkflowRunId,
            ProductId = request.ProductId,
            Basis = request.Basis,
            Accepted = null,
        };

        db.ProductRecommendations.Add(rec);
        await db.SaveChangesAsync();

        return new SaveRecommendationResponse(rec.Id, request.Sku, request.Basis);
    }
}

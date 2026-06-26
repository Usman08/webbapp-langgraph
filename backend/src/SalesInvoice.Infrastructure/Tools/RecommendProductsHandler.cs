using Microsoft.EntityFrameworkCore;
using SalesInvoice.Domain.Entities;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record RecommendProductsRequest(Guid CustomerId, List<Guid> DraftProductIds);

public record ProductRecommendationDto(Guid ProductId, string Sku, string Basis);

public record RecommendProductsResponse(List<ProductRecommendationDto> Recommendations);

public class RecommendProductsHandler(AppDbContext db)
{
    public async Task<RecommendProductsResponse> HandleAsync(RecommendProductsRequest request)
    {
        // Find products co-purchased with the draft products across this customer's history
        var invoices = await db.Invoices
            .Include(i => i.LineItems)
            .ThenInclude(l => l.Product)
            .Where(i => i.CustomerId == request.CustomerId)
            .ToListAsync();

        if (invoices.Count == 0)
            return new RecommendProductsResponse([]);

        var coFrequency = new Dictionary<Guid, int>();
        var draftSet = request.DraftProductIds.ToHashSet();

        foreach (var inv in invoices)
        {
            var invProductIds = inv.LineItems.Select(l => l.ProductId).ToHashSet();
            if (!invProductIds.Overlaps(draftSet)) continue;

            foreach (var productId in invProductIds.Except(draftSet))
                coFrequency[productId] = coFrequency.GetValueOrDefault(productId) + 1;
        }

        if (coFrequency.Count == 0)
            return new RecommendProductsResponse([]);

        var topIds = coFrequency.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key).ToList();
        var products = await db.Products.Where(p => topIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var recs = topIds
            .Where(id => products.ContainsKey(id) && products[id].InventoryQty > 0)
            .Select(id => new ProductRecommendationDto(
                id,
                products[id].Sku,
                $"co-purchased in {coFrequency[id]}/{invoices.Count} prior invoices"))
            .ToList();

        return new RecommendProductsResponse(recs);
    }
}


using Microsoft.EntityFrameworkCore;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record SearchProductsRequest(string Hint, int MaxResults = 5);

public record ProductMatch(Guid ProductId, string Sku, string Name, decimal UnitPrice, int InventoryQty);

public record SearchProductsResponse(List<ProductMatch> Products);

public class SearchProductsHandler(AppDbContext db)
{
    public async Task<SearchProductsResponse> HandleAsync(SearchProductsRequest request)
    {
        var hint = request.Hint.Trim();

        // EF Core translates ILIKE on Postgres; OrdinalIgnoreCase signals case-insensitive intent.
        var matches = await db.Products
            .Where(p => EF.Functions.ILike(p.Sku, $"%{hint}%") || EF.Functions.ILike(p.Name, $"%{hint}%"))
            .OrderBy(p => p.Name)
            .Take(request.MaxResults)
            .Select(p => new ProductMatch(p.Id, p.Sku, p.Name, p.UnitPrice, p.InventoryQty))
            .ToListAsync();

        return new SearchProductsResponse(matches);
    }
}

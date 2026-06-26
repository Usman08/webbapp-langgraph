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
        var hint = request.Hint.Trim().ToLower();

        var matches = await db.Products
            .Where(p => p.Sku.ToLower().Contains(hint) || p.Name.ToLower().Contains(hint))
            .OrderBy(p => p.Name)
            .Take(request.MaxResults)
            .Select(p => new ProductMatch(p.Id, p.Sku, p.Name, p.UnitPrice, p.InventoryQty))
            .ToListAsync();

        return new SearchProductsResponse(matches);
    }
}

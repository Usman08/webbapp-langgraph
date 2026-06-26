using Microsoft.EntityFrameworkCore;
using SalesInvoice.Domain.Enums;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record InventoryLine(Guid ProductId, int Quantity);

public record AlternativeInfo(Guid ProductId, string Sku);

public record InventoryResultLine(Guid ProductId, int Quantity, string StockStatus, AlternativeInfo? Alternative = null);

public record ValidateInventoryRequest(List<InventoryLine> Lines);

public record ValidateInventoryResponse(List<InventoryResultLine> Lines);

public class ValidateInventoryHandler(AppDbContext db)
{
    public async Task<ValidateInventoryResponse> HandleAsync(ValidateInventoryRequest request)
    {
        var productIds = request.Lines.Select(l => l.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var alternatives = await db.ProductAlternatives
            .Include(pa => pa.AlternativeProduct)
            .Where(pa => productIds.Contains(pa.ProductId))
            .OrderBy(pa => pa.Rank)
            .ToListAsync();

        var result = new List<InventoryResultLine>();
        foreach (var line in request.Lines)
        {
            if (!products.TryGetValue(line.ProductId, out var product))
                continue;

            if (product.InventoryQty > 0)
            {
                result.Add(new InventoryResultLine(line.ProductId, line.Quantity, LineStockStatus.InStock.ToString()));
                continue;
            }

            // Out of stock â€” look for an in-stock alternative
            var alt = alternatives
                .Where(a => a.ProductId == line.ProductId && a.AlternativeProduct.InventoryQty > 0)
                .OrderBy(a => a.Rank)
                .FirstOrDefault();

            if (alt is not null)
            {
                result.Add(new InventoryResultLine(line.ProductId, line.Quantity,
                    LineStockStatus.AlternativeSuggested.ToString(),
                    Alternative: new AlternativeInfo(alt.AlternativeProductId, alt.AlternativeProduct.Sku)));
            }
            else
            {
                // No in-stock alternative â†’ BackOrder (FR-020)
                result.Add(new InventoryResultLine(line.ProductId, line.Quantity, LineStockStatus.BackOrder.ToString()));
            }
        }

        return new ValidateInventoryResponse(result);
    }
}


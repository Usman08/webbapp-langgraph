using Microsoft.EntityFrameworkCore;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record GetPurchaseHistoryRequest(Guid CustomerId, string Lookback = "last_month");

public record HistoryLine(Guid ProductId, string Sku, int Quantity);

public record RecentInvoice(Guid InvoiceId, DateTimeOffset Date, List<HistoryLine> Lines);

public record CoPurchasePattern(string WithSku, string RecommendSku, string Support);

public record GetPurchaseHistoryResponse(RecentInvoice? MostRecentInvoice, List<CoPurchasePattern> CoPurchasePatterns);

public class GetPurchaseHistoryHandler(AppDbContext db)
{
    public async Task<GetPurchaseHistoryResponse> HandleAsync(GetPurchaseHistoryRequest request)
    {
        var invoices = await db.Invoices
            .Include(i => i.LineItems)
            .ThenInclude(l => l.Product)
            .Where(i => i.CustomerId == request.CustomerId)
            .OrderByDescending(i => i.InvoiceDate)
            .Take(10)
            .ToListAsync();

        if (invoices.Count == 0)
            return new GetPurchaseHistoryResponse(null, []);

        var recent = invoices[0];
        var recentDto = new RecentInvoice(
            recent.Id,
            recent.InvoiceDate,
            recent.LineItems.Select(l => new HistoryLine(l.ProductId, l.Product.Sku, l.Quantity)).ToList());

        // Co-purchase patterns: products that frequently appear together
        var patterns = new List<CoPurchasePattern>();
        var skuPairCounts = new Dictionary<(string, string), int>();

        foreach (var inv in invoices)
        {
            var skus = inv.LineItems.Select(l => l.Product.Sku).OrderBy(s => s).ToList();
            for (int i = 0; i < skus.Count; i++)
                for (int j = i + 1; j < skus.Count; j++)
                {
                    var pair = (skus[i], skus[j]);
                    skuPairCounts[pair] = skuPairCounts.GetValueOrDefault(pair) + 1;
                }
        }

        foreach (var kv in skuPairCounts.Where(kv => kv.Value >= 2).OrderByDescending(kv => kv.Value))
        {
            patterns.Add(new CoPurchasePattern(
                kv.Key.Item1, kv.Key.Item2,
                $"{kv.Value}/{invoices.Count} invoices"));
        }

        return new GetPurchaseHistoryResponse(recentDto, patterns);
    }
}


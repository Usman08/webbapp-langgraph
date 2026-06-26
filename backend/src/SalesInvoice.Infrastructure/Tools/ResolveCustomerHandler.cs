using Microsoft.EntityFrameworkCore;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record ResolveCustomerRequest(string NameHint);

public record CustomerInfo(Guid Id, string Name, string Type, string? DiscountTier);

public record ResolveCustomerResponse(string Status, CustomerInfo? Customer = null, List<CustomerInfo>? Candidates = null);

public class ResolveCustomerHandler(AppDbContext db)
{
    public async Task<ResolveCustomerResponse> HandleAsync(ResolveCustomerRequest request)
    {
        var hint = request.NameHint.Trim();
        var matches = await db.Customers
            .Where(c => EF.Functions.ILike(c.Name, $"%{hint}%"))
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (matches.Count == 0)
            return new ResolveCustomerResponse("not_found");

        if (matches.Count == 1)
        {
            var c = matches[0];
            return new ResolveCustomerResponse("resolved",
                Customer: new CustomerInfo(c.Id, c.Name, c.Type.ToString(), c.DiscountTier));
        }

        // Exact match wins
        var exact = matches.FirstOrDefault(c => c.Name.Equals(hint, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return new ResolveCustomerResponse("resolved",
                Customer: new CustomerInfo(exact.Id, exact.Name, exact.Type.ToString(), exact.DiscountTier));

        return new ResolveCustomerResponse("ambiguous",
            Candidates: matches.Select(c => new CustomerInfo(c.Id, c.Name, c.Type.ToString(), c.DiscountTier)).ToList());
    }
}


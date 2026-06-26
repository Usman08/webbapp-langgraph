using Microsoft.EntityFrameworkCore;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record ResolveDiscountRequest(Guid CustomerId);

public record ResolveDiscountResponse(string Status, decimal? Percentage = null, string? RuleKey = null);

public class ResolveDiscountHandler(AppDbContext db)
{
    public async Task<ResolveDiscountResponse> HandleAsync(ResolveDiscountRequest request)
    {
        var customer = await db.Customers.FindAsync(request.CustomerId);
        if (customer is null)
            return new ResolveDiscountResponse("no_rule");

        // Look for customer-specific rule first, then by type
        var rule = await db.DiscountRules
            .Where(r => r.Active && (r.AppliesToCustomerId == request.CustomerId || r.AppliesToType == customer.Type))
            .OrderByDescending(r => r.AppliesToCustomerId.HasValue) // customer-specific wins
            .FirstOrDefaultAsync();

        if (rule is null)
            return new ResolveDiscountResponse("no_rule");

        return new ResolveDiscountResponse("resolved", rule.Percentage, rule.Key);
    }
}


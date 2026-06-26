using SalesInvoice.Domain.Enums;

namespace SalesInvoice.Domain.Entities;

public class Customer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public CustomerType Type { get; set; }
    public string? DiscountTier { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = [];
    public ICollection<DiscountRule> DiscountRules { get; set; } = [];
}

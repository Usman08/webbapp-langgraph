using SalesInvoice.Domain.Enums;

namespace SalesInvoice.Domain.Entities;

public class DiscountRule
{
    public Guid Id { get; set; }
    public string Key { get; set; } = default!;
    public CustomerType? AppliesToType { get; set; }
    public Guid? AppliesToCustomerId { get; set; }
    public decimal Percentage { get; set; }
    public bool Active { get; set; } = true;

    public Customer? AppliesToCustomer { get; set; }
}

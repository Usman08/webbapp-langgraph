namespace SalesInvoice.Domain.Entities;

public class ProductAlternative
{
    public Guid ProductId { get; set; }
    public Guid AlternativeProductId { get; set; }
    public int Rank { get; set; }

    public Product Product { get; set; } = default!;
    public Product AlternativeProduct { get; set; } = default!;
}

namespace SalesInvoice.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Category { get; set; } = default!;
    public decimal UnitPrice { get; set; }
    public int InventoryQty { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<ProductAlternative> Alternatives { get; set; } = [];
    public ICollection<ProductAlternative> AlternativeFor { get; set; } = [];
    public ICollection<ProductRecommendation> Recommendations { get; set; } = [];
}

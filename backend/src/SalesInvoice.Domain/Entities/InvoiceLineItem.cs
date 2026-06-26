using SalesInvoice.Domain.Enums;

namespace SalesInvoice.Domain.Entities;

public class InvoiceLineItem
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public LineStockStatus StockStatus { get; set; } = LineStockStatus.InStock;

    public Invoice Invoice { get; set; } = default!;
    public Product Product { get; set; } = default!;
}

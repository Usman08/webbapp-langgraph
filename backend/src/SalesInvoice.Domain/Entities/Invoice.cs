using SalesInvoice.Domain.Enums;

namespace SalesInvoice.Domain.Entities;

public class Invoice
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public DateTimeOffset InvoiceDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercentage { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public Guid? WorkflowRunId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinalisedAt { get; set; }

    public Customer Customer { get; set; } = default!;
    public WorkflowRun? WorkflowRun { get; set; }
    public ICollection<InvoiceLineItem> LineItems { get; set; } = [];
}

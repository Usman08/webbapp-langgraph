namespace SalesInvoice.Domain.Entities;

public class ProductRecommendation
{
    public Guid Id { get; set; }
    public Guid WorkflowRunId { get; set; }
    public Guid ProductId { get; set; }
    public string Basis { get; set; } = default!;
    public bool? Accepted { get; set; }

    public WorkflowRun WorkflowRun { get; set; } = default!;
    public Product Product { get; set; } = default!;
}

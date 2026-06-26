using SalesInvoice.Domain.Enums;

namespace SalesInvoice.Domain.Entities;

public class WorkflowRun
{
    public Guid Id { get; set; }
    public string RequestText { get; set; } = default!;
    public RunStatus Status { get; set; } = RunStatus.Running;
    public Guid? CustomerId { get; set; }
    public Guid? InvoiceId { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    public Customer? Customer { get; set; }
    public Invoice? Invoice { get; set; }
    public ICollection<WorkflowStep> Steps { get; set; } = [];
    public ICollection<ProductRecommendation> Recommendations { get; set; } = [];
}

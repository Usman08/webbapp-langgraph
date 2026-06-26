namespace SalesInvoice.Domain.Entities;

public class WorkflowStep
{
    public Guid Id { get; set; }
    public Guid WorkflowRunId { get; set; }
    public int Sequence { get; set; }
    public string Name { get; set; } = default!;
    public string? ToolInvoked { get; set; }
    public string? InputPayload { get; set; }
    public string? OutputResult { get; set; }
    public bool IsException { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public WorkflowRun WorkflowRun { get; set; } = default!;
}

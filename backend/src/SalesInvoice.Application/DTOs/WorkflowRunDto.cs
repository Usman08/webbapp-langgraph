namespace SalesInvoice.Application.DTOs;

public record WorkflowStepDto(
    Guid Id, int Sequence, string Name, string? ToolInvoked,
    string? InputPayload, string? OutputResult, bool IsException, DateTimeOffset Timestamp);

public record WorkflowRunDto(
    Guid RunId, string Status, CustomerDto? Customer,
    Guid? DraftInvoiceId, List<WorkflowStepDto> Steps);

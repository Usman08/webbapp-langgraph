using System.Text.Json;
using SalesInvoice.Domain.Entities;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record RecordStepRequest(
    Guid WorkflowRunId,
    int Sequence,
    string Name,
    string? ToolInvoked,
    object? Input,
    object? Output,
    bool IsException = false);

public record RecordStepResponse(Guid StepId);

public class RecordStepHandler(AppDbContext db)
{
    public async Task<RecordStepResponse> HandleAsync(RecordStepRequest request)
    {
        var step = new WorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = request.WorkflowRunId,
            Sequence = request.Sequence,
            Name = request.Name,
            ToolInvoked = request.ToolInvoked,
            InputPayload = request.Input is null ? null : JsonSerializer.Serialize(request.Input),
            OutputResult = request.Output is null ? null : JsonSerializer.Serialize(request.Output),
            IsException = request.IsException,
            Timestamp = DateTimeOffset.UtcNow,
        };

        db.WorkflowSteps.Add(step);
        await db.SaveChangesAsync();

        return new RecordStepResponse(step.Id);
    }
}


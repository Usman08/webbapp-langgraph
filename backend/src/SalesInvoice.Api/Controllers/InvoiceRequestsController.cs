using System.Net.Mime;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInvoice.Application.DTOs;
using SalesInvoice.Domain.Entities;
using SalesInvoice.Domain.Enums;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Api.Controllers;

[ApiController]
[Route("api/invoices/requests")]
public class InvoiceRequestsController(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IServiceScopeFactory scopeFactory) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [HttpPost]
    public async Task<IActionResult> SubmitRequest([FromBody] SubmitRequestBody body)
    {
        if (string.IsNullOrWhiteSpace(body.RequestText) || body.RequestText.Length > 2000)
            return BadRequest(new { title = "Invalid request text.", status = 400 });

        var sanitised = body.RequestText.Trim();

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = sanitised,
            Status = RunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
        };
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync();

        // Trigger the AI engine asynchronously (fire and forget)
        _ = TriggerAiEngineAsync(run.Id, sanitised);

        return StatusCode(201, new
        {
            runId = run.Id,
            status = run.Status.ToString(),
            streamUrl = $"/api/invoices/requests/{run.Id}/stream",
        });
    }

    [HttpGet("{runId:guid}")]
    public async Task<IActionResult> GetRun(Guid runId)
    {
        var run = await db.WorkflowRuns
            .Include(r => r.Customer)
            .Include(r => r.Steps.OrderBy(s => s.Sequence))
            .FirstOrDefaultAsync(r => r.Id == runId);

        if (run is null) return NotFound();

        CustomerDto? customerDto = run.Customer is null
            ? null
            : new CustomerDto(run.Customer.Id, run.Customer.Name, run.Customer.Type.ToString(),
                run.Customer.DiscountTier, run.Customer.ContactEmail);

        var dto = new WorkflowRunDto(
            run.Id, run.Status.ToString(), customerDto, run.InvoiceId,
            run.Steps.Select(s => new WorkflowStepDto(s.Id, s.Sequence, s.Name, s.ToolInvoked,
                s.InputPayload, s.OutputResult, s.IsException, s.Timestamp)).ToList());

        return Ok(dto);
    }

    [HttpGet("{runId:guid}/stream")]
    public async Task StreamRun(Guid runId, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";

        var aiEngineUrl = config["AiEngineUrl"] ?? "http://localhost:8000";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };

        try
        {
            using var upstream = await http.GetAsync(
                $"{aiEngineUrl}/run/{runId}/stream",
                HttpCompletionOption.ResponseHeadersRead, ct);

            await using var stream = await upstream.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(ct)) is not null && !ct.IsCancellationRequested)
            {
                var bytes = Encoding.UTF8.GetBytes(line + "\n");
                await Response.Body.WriteAsync(bytes, ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (Exception)
        {
            // Engine not reachable or run ended — emit a terminal event
            var msg = $"data: {{\"type\":\"workflow_failed\",\"reason\":\"stream_unavailable\"}}\n\n";
            await Response.Body.WriteAsync(Encoding.UTF8.GetBytes(msg), ct);
        }
    }

    [HttpPost("{runId:guid}/disambiguate")]
    public async Task<IActionResult> Disambiguate(Guid runId, [FromBody] DisambiguateBody body)
    {
        var run = await db.WorkflowRuns.FindAsync(runId);
        if (run is null) return NotFound();

        run.CustomerId = body.CustomerId;
        await db.SaveChangesAsync();

        // Notify the engine to resume (best-effort)
        var client = httpClientFactory.CreateClient("AiEngine");
        try
        {
            await client.PostAsJsonAsync($"/run/{runId}/disambiguate", new { customerId = body.CustomerId });
        }
        catch (Exception) { /* log in production */ }

        return Ok(new { runId, customerId = body.CustomerId });
    }

    private async Task TriggerAiEngineAsync(Guid runId, string requestText)
    {
        try
        {
            var client = httpClientFactory.CreateClient("AiEngine");
            await client.PostAsJsonAsync("/run", new { runId, requestText });
        }
        catch (Exception)
        {
            using var scope = scopeFactory.CreateScope();
            var scopedDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var run = await scopedDb.WorkflowRuns.FindAsync(runId);
            if (run is not null)
            {
                run.Status = RunStatus.Failed;
                run.CompletedAt = DateTimeOffset.UtcNow;
                await scopedDb.SaveChangesAsync();
            }
        }
    }
}

public record SubmitRequestBody(string RequestText);
public record DisambiguateBody(Guid CustomerId);

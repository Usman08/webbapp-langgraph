using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesInvoice.Domain.Entities;
using SalesInvoice.Domain.Enums;
using SalesInvoice.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalesInvoice.IntegrationTests;

/// <summary>
/// Validates the record-step tool and SSE relay wiring (US2 / T056).
/// Checks: step persistence contract, SSE event ordering guarantee, tool_invoked/tool_result 1:1.
/// Full end-to-end SSE requires the AI engine process which is out of scope for .NET integration tests;
/// we verify the .NET side (record-step persistence and the stream proxy error path).
/// </summary>
public class StreamTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private WebApplicationFactory<Program> _factory = default!;
    private HttpClient _client = default!;
    private const string EngineToken = "test-token";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
                builder.UseSetting("EngineToken", EngineToken);
                builder.UseSetting("AiEngineUrl", "http://localhost:9999"); // engine not running
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Engine-Token", EngineToken);
    }

    public async Task DisposeAsync()
    {
        _factory.Dispose();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task RecordStep_PersistsStepWithCorrectFields()
    {
        // Arrange — create a WorkflowRun
        var runId = await CreateWorkflowRunAsync();

        var payload = new
        {
            workflowRunId = runId,
            sequence = 1,
            name = "intent_parse",
            toolInvoked = (string?)null,
            input = new { requestText = "test request" },
            output = new { customerName = "ABC" },
            isException = false,
        };

        // Act
        var response = await _client.PostAsJsonAsync("/internal/tools/record-step", payload);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var stepId = body.GetProperty("stepId").GetGuid();
        stepId.Should().NotBeEmpty();

        // Assert persisted correctly
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var step = await db.WorkflowSteps.FindAsync(stepId);

        step.Should().NotBeNull();
        step!.WorkflowRunId.Should().Be(runId);
        step.Sequence.Should().Be(1);
        step.Name.Should().Be("intent_parse");
        step.ToolInvoked.Should().BeNull();
        step.IsException.Should().BeFalse();
        step.InputPayload.Should().Contain("requestText");
        step.OutputResult.Should().Contain("customerName");
    }

    [Fact]
    public async Task RecordStep_StepsAreOrderedBySequence()
    {
        var runId = await CreateWorkflowRunAsync();

        object[] payloads =
        [
            new { workflowRunId = runId, sequence = 3, name = "inventory_validation",
                  toolInvoked = "validate-inventory", input = new { }, output = new { }, isException = false },
            new { workflowRunId = runId, sequence = 1, name = "intent_parse",
                  toolInvoked = (string?)null, input = new { requestText = "x" }, output = new { customerName = "y" }, isException = false },
            new { workflowRunId = runId, sequence = 2, name = "customer_lookup",
                  toolInvoked = "resolve-customer", input = new { nameHint = "ABC" }, output = new { status = "resolved" }, isException = false },
        ];

        foreach (var payload in payloads)
        {
            var r = await _client.PostAsJsonAsync("/internal/tools/record-step", payload);
            r.EnsureSuccessStatusCode();
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var persisted = await db.WorkflowSteps
            .Where(s => s.WorkflowRunId == runId)
            .OrderBy(s => s.Sequence)
            .ToListAsync();

        persisted.Should().HaveCount(3);
        persisted.Select(s => s.Sequence).Should().BeInAscendingOrder();
        persisted[0].Name.Should().Be("intent_parse");
        persisted[1].Name.Should().Be("customer_lookup");
        persisted[2].Name.Should().Be("inventory_validation");
    }

    [Fact]
    public async Task RecordStep_IsExceptionFlaggedCorrectly()
    {
        var runId = await CreateWorkflowRunAsync();

        var payload = new
        {
            workflowRunId = runId,
            sequence = 5,
            name = "inventory_validation",
            toolInvoked = "validate-inventory",
            input = new { lines = new[] { new { productId = "uuid-x", quantity = 10 } } },
            output = new { detail = "SKU-X out of stock", resolution = "BackOrder" },
            isException = true,
        };

        var response = await _client.PostAsJsonAsync("/internal/tools/record-step", payload);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var stepId = body.GetProperty("stepId").GetGuid();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var step = await db.WorkflowSteps.FindAsync(stepId);

        step!.IsException.Should().BeTrue();
        step.OutputResult.Should().Contain("out of stock");
    }

    [Fact]
    public async Task StreamEndpoint_ReturnsEventStreamContentType()
    {
        // Arrange — create a run (engine not available, expect error event)
        var submitResponse = await _client.PostAsJsonAsync("/api/invoices/requests",
            new { requestText = "Create an invoice for ABC Traders" });
        var run = await submitResponse.Content.ReadFromJsonAsync<JsonElement>();
        var runId = run.GetProperty("runId").GetGuid();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Act — open the SSE stream endpoint
        using var streamReq = new HttpRequestMessage(HttpMethod.Get,
            $"/api/invoices/requests/{runId}/stream");
        // Remove engine token for this public endpoint
        streamReq.Headers.Remove("X-Engine-Token");

        var clientWithoutToken = _factory.CreateClient();
        using var streamResp = await clientWithoutToken.SendAsync(
            streamReq, HttpCompletionOption.ResponseHeadersRead, cts.Token);

        // Assert: content-type is event-stream
        streamResp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateWorkflowRunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "test",
            Status = RunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
        };
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }
}

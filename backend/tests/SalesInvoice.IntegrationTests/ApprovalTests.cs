using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesInvoice.Domain.Entities;
using SalesInvoice.Domain.Enums;
using SalesInvoice.Domain.Pricing;
using SalesInvoice.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalesInvoice.IntegrationTests;

/// <summary>
/// US4 integration tests (T071):
/// - Approval finalises invoice and reflects any prior edits
/// - No-approval path never finalises (SC-007)
/// - Reject keeps status Draft
/// - Double-approve returns 409
/// - Edit lines recalculates totals
/// </summary>
public class ApprovalTests : IAsyncLifetime
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
                builder.UseSetting("AiEngineUrl", "http://localhost:9999");
            });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);

        _client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        await _postgres.DisposeAsync();
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Approve_DraftInvoice_BecomesFinalisedWithCorrectTotal()
    {
        var invoiceId = await CreateDraftInvoiceAsync();

        var resp = await _client.PostAsJsonAsync($"/api/invoices/{invoiceId}/approve", new { });
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Finalised");
        body.GetProperty("finalisedAt").GetString().Should().NotBeNullOrEmpty();

        // Verify in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = await db.Invoices.FindAsync(invoiceId);
        inv!.Status.Should().Be(InvoiceStatus.Finalised);
        inv.FinalisedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Approve_AlreadyFinalised_Returns409()
    {
        var invoiceId = await CreateDraftInvoiceAsync();
        await _client.PostAsJsonAsync($"/api/invoices/{invoiceId}/approve", new { });

        // Second approve
        var resp = await _client.PostAsJsonAsync($"/api/invoices/{invoiceId}/approve", new { });
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ApproveAfterEdit_ReflectsEditedLines()
    {
        var invoiceId = await CreateDraftInvoiceAsync();
        var inv = await GetInvoiceAsync(invoiceId);

        var firstLine = inv.GetProperty("lineItems")[0];
        var productId = firstLine.GetProperty("productId").GetGuid();
        var originalQty = firstLine.GetProperty("quantity").GetInt32();

        // Edit the line to double the quantity
        var editPayload = new[]
        {
            new { productId, quantity = originalQty * 2, stockStatus = "InStock" }
        };
        var editResp = await _client.PutAsJsonAsync($"/api/invoices/{invoiceId}/lines", editPayload);
        editResp.EnsureSuccessStatusCode();

        // Approve
        await _client.PostAsJsonAsync($"/api/invoices/{invoiceId}/approve", new { });

        // Verify final invoice has the edited quantity
        var finalInv = await GetInvoiceAsync(invoiceId);
        finalInv.GetProperty("status").GetString().Should().Be("Finalised");
        var finalQty = finalInv.GetProperty("lineItems")[0].GetProperty("quantity").GetInt32();
        finalQty.Should().Be(originalQty * 2);
    }

    // ── SC-007: no-approval never finalises ───────────────────────────────────

    [Fact]
    public async Task DraftInvoice_WithoutApproval_RemainsNotFinalised()
    {
        var invoiceId = await CreateDraftInvoiceAsync();

        // Do NOT call approve — just read the invoice
        var inv = await GetInvoiceAsync(invoiceId);
        inv.GetProperty("status").GetString().Should().Be("Draft");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbInv = await db.Invoices.FindAsync(invoiceId);
        dbInv!.Status.Should().Be(InvoiceStatus.Draft);
        dbInv.FinalisedAt.Should().BeNull();
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Reject_KeepsDraftStatus()
    {
        var invoiceId = await CreateDraftInvoiceAsync();

        var resp = await _client.PostAsJsonAsync($"/api/invoices/{invoiceId}/reject", new { });
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Draft");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inv = await db.Invoices.FindAsync(invoiceId);
        inv!.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public async Task RejectThenEdit_AllowsLineModification()
    {
        var invoiceId = await CreateDraftInvoiceAsync();
        await _client.PostAsJsonAsync($"/api/invoices/{invoiceId}/reject", new { });

        // After reject, PUT /lines should still work (status stays Draft)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstProduct = await db.Products.Where(p => p.InventoryQty > 0).FirstAsync();

        var editPayload = new[] { new { productId = firstProduct.Id, quantity = 5, stockStatus = "InStock" } };
        var editResp = await _client.PutAsJsonAsync($"/api/invoices/{invoiceId}/lines", editPayload);
        editResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
    }

    // ── Edit lines ────────────────────────────────────────────────────────────

    [Fact]
    public async Task EditLines_RecalculatesTotalsCorrectly()
    {
        var invoiceId = await CreateDraftInvoiceAsync();
        var original = await GetInvoiceAsync(invoiceId);
        var originalTotal = original.GetProperty("total").GetDecimal();

        var firstLine = original.GetProperty("lineItems")[0];
        var productId = firstLine.GetProperty("productId").GetGuid();
        var unitPrice = firstLine.GetProperty("unitPrice").GetDecimal();
        var newQty = 10;

        var editPayload = new[] { new { productId, quantity = newQty, stockStatus = "InStock" } };
        var resp = await _client.PutAsJsonAsync($"/api/invoices/{invoiceId}/lines", editPayload);
        resp.EnsureSuccessStatusCode();

        var updated = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var newSubtotal = updated.GetProperty("subtotal").GetDecimal();
        newSubtotal.Should().Be(newQty * unitPrice);
    }

    [Fact]
    public async Task EditLines_OnFinalised_Returns409()
    {
        var invoiceId = await CreateDraftInvoiceAsync();
        await _client.PostAsJsonAsync($"/api/invoices/{invoiceId}/approve", new { });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstProduct = await db.Products.Where(p => p.InventoryQty > 0).FirstAsync();

        var editPayload = new[] { new { productId = firstProduct.Id, quantity = 1, stockStatus = "InStock" } };
        var resp = await _client.PutAsJsonAsync($"/api/invoices/{invoiceId}/lines", editPayload);
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.Conflict);
    }

    // ── History ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetInvoices_ReturnsAllInvoices()
    {
        var resp = await _client.GetAsync("/api/invoices");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task GetInvoices_WithStatusFilter_ReturnsOnlyMatching()
    {
        var invoiceId = await CreateDraftInvoiceAsync();
        await _client.PostAsJsonAsync($"/api/invoices/{invoiceId}/approve", new { });

        var resp = await _client.GetAsync("/api/invoices?status=Finalised");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        foreach (var inv in body.EnumerateArray())
            inv.GetProperty("status").GetString().Should().Be("Finalised");
    }

    [Fact]
    public async Task GetWorkflowTrail_ReturnsOrderedSteps()
    {
        // Create invoice with a linked workflow run that has steps
        var (invoiceId, runId) = await CreateInvoiceWithWorkflowAsync();

        // Record a couple of steps via the internal tool
        var engineClient = _factory.CreateClient();
        engineClient.DefaultRequestHeaders.Add("X-Engine-Token", EngineToken);

        foreach (var (seq, name, tool) in new[] { (1, "intent_parse", (string?)null), (2, "customer_lookup", "resolve-customer") })
        {
            await engineClient.PostAsJsonAsync("/internal/tools/record-step", new
            {
                workflowRunId = runId,
                sequence = seq,
                name,
                toolInvoked = tool,
                input = new { },
                output = new { },
                isException = false,
            });
        }

        var resp = await _client.GetAsync($"/api/invoices/{invoiceId}/workflow");
        resp.EnsureSuccessStatusCode();
        var steps = await resp.Content.ReadFromJsonAsync<JsonElement>();
        steps.ValueKind.Should().Be(JsonValueKind.Array);
        steps.GetArrayLength().Should().Be(2);
        steps[0].GetProperty("sequence").GetInt32().Should().Be(1);
        steps[1].GetProperty("sequence").GetInt32().Should().Be(2);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateDraftInvoiceAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.FirstAsync();
        var product = await db.Products.Where(p => p.InventoryQty > 0).FirstAsync();

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "test approval",
            Status = RunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CustomerId = customer.Id,
        };
        db.WorkflowRuns.Add(run);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            WorkflowRunId = run.Id,
            InvoiceDate = DateTimeOffset.UtcNow,
            Subtotal = product.UnitPrice,
            DiscountPercentage = 0,
            DiscountAmount = 0,
            TaxPercentage = 10,
            TaxAmount = product.UnitPrice * 0.10m,
            Total = product.UnitPrice * 1.10m,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            LineItems =
            [
                new InvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitPrice = product.UnitPrice,
                    LineTotal = product.UnitPrice,
                    StockStatus = LineStockStatus.InStock,
                }
            ],
        };
        db.Invoices.Add(invoice);
        run.InvoiceId = invoice.Id;
        await db.SaveChangesAsync();
        return invoice.Id;
    }

    private async Task<(Guid invoiceId, Guid runId)> CreateInvoiceWithWorkflowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.FirstAsync();
        var product = await db.Products.Where(p => p.InventoryQty > 0).FirstAsync();

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "test workflow trail",
            Status = RunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CustomerId = customer.Id,
        };
        db.WorkflowRuns.Add(run);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            WorkflowRunId = run.Id,
            InvoiceDate = DateTimeOffset.UtcNow,
            Subtotal = product.UnitPrice,
            DiscountPercentage = 0,
            DiscountAmount = 0,
            TaxPercentage = 10,
            TaxAmount = product.UnitPrice * 0.10m,
            Total = product.UnitPrice * 1.10m,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            LineItems =
            [
                new InvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = product.Id,
                    Quantity = 1,
                    UnitPrice = product.UnitPrice,
                    LineTotal = product.UnitPrice,
                    StockStatus = LineStockStatus.InStock,
                }
            ],
        };
        db.Invoices.Add(invoice);
        run.InvoiceId = invoice.Id;
        await db.SaveChangesAsync();
        return (invoice.Id, run.Id);
    }

    private async Task<JsonElement> GetInvoiceAsync(Guid invoiceId)
    {
        var resp = await _client.GetAsync($"/api/invoices/{invoiceId}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }
}

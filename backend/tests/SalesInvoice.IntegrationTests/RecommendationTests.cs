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
/// US3 integration tests: recommendation generation + accept/decline + recalculation (T063).
/// </summary>
public class RecommendationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private WebApplicationFactory<Program> _factory = default!;
    private HttpClient _client = default!;
    private HttpClient _engineClient = default!;
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
        _engineClient = _factory.CreateClient();
        _engineClient.DefaultRequestHeaders.Add("X-Engine-Token", EngineToken);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _engineClient.Dispose();
        _factory.Dispose();
        await _postgres.DisposeAsync();
    }

    // ── Recommend-products tool ────────────────────────────────────────────────

    [Fact]
    public async Task RecommendProducts_WithCoPurchaseHistory_ReturnsRecommendations()
    {
        // Get a customer that has history in the seeded dataset
        var custResp = await _engineClient.PostAsJsonAsync("/internal/tools/resolve-customer",
            new { nameHint = "ABC Traders" });
        custResp.EnsureSuccessStatusCode();
        var custBody = await custResp.Content.ReadFromJsonAsync<JsonElement>();
        var customerId = custBody.GetProperty("customer").GetProperty("id").GetGuid();

        // Get some product IDs from the reference endpoint
        var prodResp = await _client.GetAsync("/api/products");
        prodResp.EnsureSuccessStatusCode();
        var prods = await prodResp.Content.ReadFromJsonAsync<JsonElement>();
        var firstProductId = prods[0].GetProperty("id").GetGuid();

        // Act
        var recResp = await _engineClient.PostAsJsonAsync("/internal/tools/recommend-products",
            new { customerId, draftProductIds = new[] { firstProductId } });

        recResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var recBody = await recResp.Content.ReadFromJsonAsync<JsonElement>();
        recBody.TryGetProperty("recommendations", out var recs).Should().BeTrue();
        recs.ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task RecommendProducts_NoHistory_ReturnsEmptyList()
    {
        // Use a customer that has no purchase history (create one ad-hoc)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var newCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Brand New Co",
            Type = CustomerType.Retail,
            DiscountTier = "retail-standard",
            ContactEmail = "new@test.com",
        };
        db.Customers.Add(newCustomer);
        await db.SaveChangesAsync();

        var resp = await _engineClient.PostAsJsonAsync("/internal/tools/recommend-products",
            new { customerId = newCustomer.Id, draftProductIds = Array.Empty<Guid>() });

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var recs = body.GetProperty("recommendations");
        recs.GetArrayLength().Should().Be(0);
    }

    // ── Save-recommendation tool ───────────────────────────────────────────────

    [Fact]
    public async Task SaveRecommendation_PersistsToDatabase()
    {
        var runId = await CreateWorkflowRunAsync();

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.FirstAsync();

        var resp = await _engineClient.PostAsJsonAsync("/internal/tools/save-recommendation",
            new { workflowRunId = runId, productId = product.Id, sku = product.Sku, basis = "test basis" });

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var recId = body.GetProperty("recommendationId").GetGuid();
        recId.Should().NotBeEmpty();

        var saved = await db.ProductRecommendations.FindAsync(recId);
        saved.Should().NotBeNull();
        saved!.WorkflowRunId.Should().Be(runId);
        saved.ProductId.Should().Be(product.Id);
        saved.Basis.Should().Be("test basis");
        saved.Accepted.Should().BeNull();
    }

    // ── Accept/Decline recommendation endpoint ────────────────────────────────

    [Fact]
    public async Task AcceptRecommendation_AddsLineAndRecalculates()
    {
        var (runId, invoiceId, recId, recProductId) = await SetupRunWithDraftAndRecommendation();

        var invoiceBefore = await GetInvoiceAsync(invoiceId);
        var totalBefore = invoiceBefore.GetProperty("total").GetDecimal();

        // Act — accept
        var resp = await _client.PostAsJsonAsync(
            $"/api/invoices/requests/{runId}/recommendations/{recId}",
            new { accepted = true });

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accepted").GetBoolean().Should().BeTrue();

        // The invoice should have a new line and a higher total
        var invoiceAfter = await GetInvoiceAsync(invoiceId);
        var totalAfter = invoiceAfter.GetProperty("total").GetDecimal();
        totalAfter.Should().BeGreaterThan(totalBefore);

        // Recommendation should be marked accepted in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rec = await db.ProductRecommendations.FindAsync(recId);
        rec!.Accepted.Should().BeTrue();
    }

    [Fact]
    public async Task DeclineRecommendation_MarksDeclinedNoLineAdded()
    {
        var (runId, invoiceId, recId, _) = await SetupRunWithDraftAndRecommendation();

        var invoiceBefore = await GetInvoiceAsync(invoiceId);
        var lineCountBefore = invoiceBefore.GetProperty("lineItems").GetArrayLength();

        // Act — decline
        var resp = await _client.PostAsJsonAsync(
            $"/api/invoices/requests/{runId}/recommendations/{recId}",
            new { accepted = false });

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accepted").GetBoolean().Should().BeFalse();

        // No new lines
        var invoiceAfter = await GetInvoiceAsync(invoiceId);
        invoiceAfter.GetProperty("lineItems").GetArrayLength().Should().Be(lineCountBefore);

        // Recommendation marked declined in DB
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rec = await db.ProductRecommendations.FindAsync(recId);
        rec!.Accepted.Should().BeFalse();
    }

    [Fact]
    public async Task ActOnRecommendation_WrongRunId_ReturnsNotFound()
    {
        var (runId, _, recId, _) = await SetupRunWithDraftAndRecommendation();
        var wrongRunId = Guid.NewGuid();

        var resp = await _client.PostAsJsonAsync(
            $"/api/invoices/requests/{wrongRunId}/recommendations/{recId}",
            new { accepted = true });

        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Guid> CreateWorkflowRunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "test recommendation run",
            Status = RunStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
        };
        db.WorkflowRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    private async Task<(Guid runId, Guid invoiceId, Guid recId, Guid recProductId)> SetupRunWithDraftAndRecommendation()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.FirstAsync();
        var products = await db.Products.Where(p => p.InventoryQty > 0).Take(2).ToListAsync();
        var draftProduct = products[0];
        var recProduct = products[1];

        // Create run
        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "test",
            Status = RunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CustomerId = customer.Id,
        };
        db.WorkflowRuns.Add(run);

        // Create draft invoice
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            WorkflowRunId = run.Id,
            InvoiceDate = DateTimeOffset.UtcNow,
            Subtotal = draftProduct.UnitPrice,
            DiscountPercentage = 0,
            DiscountAmount = 0,
            TaxPercentage = 10,
            TaxAmount = draftProduct.UnitPrice * 0.10m,
            Total = draftProduct.UnitPrice * 1.10m,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            LineItems =
            [
                new InvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = draftProduct.Id,
                    Quantity = 1,
                    UnitPrice = draftProduct.UnitPrice,
                    LineTotal = draftProduct.UnitPrice,
                    StockStatus = LineStockStatus.InStock,
                }
            ],
        };
        db.Invoices.Add(invoice);
        run.InvoiceId = invoice.Id;

        // Create recommendation
        var rec = new ProductRecommendation
        {
            Id = Guid.NewGuid(),
            WorkflowRunId = run.Id,
            ProductId = recProduct.Id,
            Basis = "co-purchased in 3/5 prior invoices",
            Accepted = null,
        };
        db.ProductRecommendations.Add(rec);

        await db.SaveChangesAsync();

        return (run.Id, invoice.Id, rec.Id, recProduct.Id);
    }

    private async Task<JsonElement> GetInvoiceAsync(Guid invoiceId)
    {
        var resp = await _client.GetAsync($"/api/invoices/{invoiceId}");
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }
}

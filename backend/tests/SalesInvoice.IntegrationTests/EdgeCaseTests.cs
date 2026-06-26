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
/// T074: Edge-case integration tests.
/// - First-time customer (no purchase history)
/// - Fractional rounding (≥3 decimal places rounds to 2)
/// - Missing discount rule (graceful degradation)
/// - BackOrder cascade (invoice created even when stock is partial)
/// </summary>
public class EdgeCaseTests : IAsyncLifetime
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

    // ── First-time customer ───────────────────────────────────────────────────

    [Fact]
    public async Task FirstTimeCustomer_InvoiceCreatedWithNoPurchaseHistory()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create a brand-new customer with no prior invoices
        var newCustomer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "Brand New Corp",
            Type = "Standard",
            CreditLimit = 5000,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Customers.Add(newCustomer);
        await db.SaveChangesAsync();

        // Verify customer has no prior invoices
        var priorInvoices = await db.Invoices.Where(i => i.CustomerId == newCustomer.Id).ToListAsync();
        priorInvoices.Should().BeEmpty();

        // Create an invoice for the new customer (simulating the tool endpoint)
        var product = await db.Products.FirstAsync(p => p.InventoryQty > 0);
        var engineClient = _factory.CreateClient();
        engineClient.DefaultRequestHeaders.Add("X-Engine-Token", EngineToken);

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "invoice for brand new corp",
            Status = RunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CustomerId = newCustomer.Id,
        };
        db.WorkflowRuns.Add(run);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = newCustomer.Id,
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

        // The invoice must be retrievable even though customer is new
        var resp = await _client.GetAsync($"/api/invoices/{invoice.Id}");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Draft");
        body.GetProperty("lineItems").GetArrayLength().Should().Be(1);
    }

    // ── Fractional rounding ───────────────────────────────────────────────────

    [Fact]
    public async Task EditLines_FractionalUnitPrice_RoundsToTwoDecimalPlaces()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.FirstAsync();

        // Create a product with a unit price that produces a 3-decimal total
        // e.g., 1/3 ≈ 0.333... → quantity 3 = 1.00, but at 0.123456 × 3 = 0.370368 rounds to 0.37
        var weirdProduct = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "FRAC-001",
            Name = "Fractional Price Widget",
            UnitPrice = 0.123m,      // 3 decimal digits
            InventoryQty = 100,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Products.Add(weirdProduct);

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "fractional test",
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
            Subtotal = weirdProduct.UnitPrice,
            DiscountPercentage = 0,
            DiscountAmount = 0,
            TaxPercentage = 10,
            TaxAmount = Math.Round(weirdProduct.UnitPrice * 0.10m, 2, MidpointRounding.AwayFromZero),
            Total = Math.Round(weirdProduct.UnitPrice * 1.10m, 2, MidpointRounding.AwayFromZero),
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            LineItems =
            [
                new InvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = weirdProduct.Id,
                    Quantity = 1,
                    UnitPrice = weirdProduct.UnitPrice,
                    LineTotal = weirdProduct.UnitPrice,
                    StockStatus = LineStockStatus.InStock,
                }
            ],
        };
        db.Invoices.Add(invoice);
        run.InvoiceId = invoice.Id;
        await db.SaveChangesAsync();

        // Edit lines to qty = 3 → subtotal = 3 × 0.123 = 0.369 → rounds to 0.37
        var editPayload = new[] { new { productId = weirdProduct.Id, quantity = 3, stockStatus = "InStock" } };
        var resp = await _client.PutAsJsonAsync($"/api/invoices/{invoice.Id}/lines", editPayload);
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        var subtotal = body.GetProperty("subtotal").GetDecimal();
        // Verify it is a properly rounded monetary value (≤ 2 decimal places)
        subtotal.Should().Be(Math.Round(subtotal, 2, MidpointRounding.AwayFromZero));
        var total = body.GetProperty("total").GetDecimal();
        total.Should().Be(Math.Round(total, 2, MidpointRounding.AwayFromZero));
    }

    // ── Missing discount rule ─────────────────────────────────────────────────

    [Fact]
    public async Task Approve_InvoiceWithNoDiscountRule_StillFinalises()
    {
        // An invoice with 0% discount (no rule matched) should still be approvable
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.FirstAsync();
        var product = await db.Products.FirstAsync(p => p.InventoryQty > 0);

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "no discount test",
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
            DiscountPercentage = 0,   // explicitly no discount
            DiscountAmount = 0,
            TaxPercentage = 10,
            TaxAmount = Math.Round(product.UnitPrice * 0.10m, 2, MidpointRounding.AwayFromZero),
            Total = Math.Round(product.UnitPrice * 1.10m, 2, MidpointRounding.AwayFromZero),
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

        var resp = await _client.PostAsJsonAsync($"/api/invoices/{invoice.Id}/approve", new { });
        resp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetString().Should().Be("Finalised");
        body.GetProperty("discountPercentage").GetDecimal().Should().Be(0);
    }

    // ── BackOrder cascade ─────────────────────────────────────────────────────

    [Fact]
    public async Task Invoice_WithBackOrderLine_IsCreatedAndApprovable()
    {
        // Even when some lines are BackOrder, the invoice should be created,
        // shown to the user, and approvable.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var customer = await db.Customers.FirstAsync();
        var inStockProduct = await db.Products.FirstAsync(p => p.InventoryQty > 0);

        // Create a "low stock" product to simulate BackOrder
        var lowStockProduct = new Product
        {
            Id = Guid.NewGuid(),
            Sku = "LOW-001",
            Name = "Low Stock Widget",
            UnitPrice = 10.00m,
            InventoryQty = 0,   // out of stock
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Products.Add(lowStockProduct);

        var run = new WorkflowRun
        {
            Id = Guid.NewGuid(),
            RequestText = "backorder test",
            Status = RunStatus.AwaitingApproval,
            StartedAt = DateTimeOffset.UtcNow,
            CustomerId = customer.Id,
        };
        db.WorkflowRuns.Add(run);

        var subtotal = inStockProduct.UnitPrice + lowStockProduct.UnitPrice;
        var taxAmt = Math.Round(subtotal * 0.10m, 2, MidpointRounding.AwayFromZero);
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            WorkflowRunId = run.Id,
            InvoiceDate = DateTimeOffset.UtcNow,
            Subtotal = subtotal,
            DiscountPercentage = 0,
            DiscountAmount = 0,
            TaxPercentage = 10,
            TaxAmount = taxAmt,
            Total = subtotal + taxAmt,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            LineItems =
            [
                new InvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = inStockProduct.Id,
                    Quantity = 1,
                    UnitPrice = inStockProduct.UnitPrice,
                    LineTotal = inStockProduct.UnitPrice,
                    StockStatus = LineStockStatus.InStock,
                },
                new InvoiceLineItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = lowStockProduct.Id,
                    Quantity = 1,
                    UnitPrice = lowStockProduct.UnitPrice,
                    LineTotal = lowStockProduct.UnitPrice,
                    StockStatus = LineStockStatus.BackOrder,  // BackOrder line
                },
            ],
        };
        db.Invoices.Add(invoice);
        run.InvoiceId = invoice.Id;
        await db.SaveChangesAsync();

        // Invoice should be retrievable with both lines
        var getResp = await _client.GetAsync($"/api/invoices/{invoice.Id}");
        getResp.EnsureSuccessStatusCode();
        var body = await getResp.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("lineItems").GetArrayLength().Should().Be(2);

        var lines = body.GetProperty("lineItems").EnumerateArray().ToList();
        lines.Should().Contain(l => l.GetProperty("stockStatus").GetString() == "BackOrder");

        // BackOrder invoice should still be approvable
        var approveResp = await _client.PostAsJsonAsync($"/api/invoices/{invoice.Id}/approve", new { });
        approveResp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var approved = await approveResp.Content.ReadFromJsonAsync<JsonElement>();
        approved.GetProperty("status").GetString().Should().Be("Finalised");
    }
}

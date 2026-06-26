using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesInvoice.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalesInvoice.IntegrationTests;

public class ToolsTests : IAsyncLifetime
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
                builder.UseSetting("AiEngineUrl", "http://localhost:9999"); // not used in tool tests
            });

        // Ensure migrations run
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);

        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Engine-Token", EngineToken);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task ResolveCustomer_ExistingCustomer_ReturnsResolved()
    {
        var response = await _client.PostAsJsonAsync("/internal/tools/resolve-customer",
            new { nameHint = "ABC Traders" });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["status"].ToString().Should().Be("resolved");
    }

    [Fact]
    public async Task ResolveCustomer_UnknownCustomer_ReturnsNotFound()
    {
        var response = await _client.PostAsJsonAsync("/internal/tools/resolve-customer",
            new { nameHint = "XYZ Unknown Corp" });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body!["status"].ToString().Should().Be("not_found");
    }

    [Fact]
    public async Task GetPurchaseHistory_ExistingCustomer_ReturnsMostRecentInvoice()
    {
        // Get ABC Traders ID first
        var resolveResp = await _client.PostAsJsonAsync("/internal/tools/resolve-customer",
            new { nameHint = "ABC Traders" });
        var resolved = await resolveResp.Content.ReadFromJsonAsync<ResolveCustomerResult>();
        var customerId = resolved!.Customer!.Id;

        var response = await _client.PostAsJsonAsync("/internal/tools/get-purchase-history",
            new { customerId, lookback = "last_month" });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("mostRecentInvoice");
    }

    [Fact]
    public async Task AdjustQuantities_AppliesHalfUpRounding()
    {
        var response = await _client.PostAsJsonAsync("/internal/tools/adjust-quantities",
            new
            {
                lines = new[] { new { productId = Guid.NewGuid(), quantity = 3 } },
                deltaPercent = 20,
            });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdjustResult>();
        body!.Lines[0].Quantity.Should().Be(4); // 3 * 1.2 = 3.6 → rounds half-up to 4
    }

    [Fact]
    public async Task ValidateInventory_OutOfStockWithAlternative_ReturnsAlternativeSuggested()
    {
        // Get Monitor 24" product ID (out of stock, has alt)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var monitor = await db.Products.FirstAsync(p => p.Sku == "ELC-004");

        var response = await _client.PostAsJsonAsync("/internal/tools/validate-inventory",
            new { lines = new[] { new { productId = monitor.Id, quantity = 1 } } });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("AlternativeSuggested");
    }

    [Fact]
    public async Task ValidateInventory_OutOfStockNoAlternative_ReturnsBackOrder()
    {
        // Wireless Mouse has no in-stock alternative
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var mouse = await db.Products.FirstAsync(p => p.Sku == "ELC-003");

        var response = await _client.PostAsJsonAsync("/internal/tools/validate-inventory",
            new { lines = new[] { new { productId = mouse.Id, quantity = 1 } } });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("BackOrder");
    }

    [Fact]
    public async Task ResolveDiscount_RetailCustomer_Returns5Percent()
    {
        var resolveResp = await _client.PostAsJsonAsync("/internal/tools/resolve-customer",
            new { nameHint = "ABC Traders" });
        var resolved = await resolveResp.Content.ReadFromJsonAsync<ResolveCustomerResult>();
        var customerId = resolved!.Customer!.Id;

        var response = await _client.PostAsJsonAsync("/internal/tools/resolve-discount",
            new { customerId });

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("resolved");
    }

    private record CustomerInfo(Guid Id, string Name, string Type, string? DiscountTier);
    private record ResolveCustomerResult(string Status, CustomerInfo? Customer, List<CustomerInfo>? Candidates);
    private record AdjustLine(Guid ProductId, int Quantity);
    private record AdjustResult(List<AdjustLine> Lines);
}

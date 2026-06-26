using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesInvoice.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace SalesInvoice.IntegrationTests;

public class SeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    private AppDbContext _db = default!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _db = new AppDbContext(opts);
        await _db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task Seed_CreatesExpectedCustomers()
    {
        await DbSeeder.SeedAsync(_db);

        var customers = await _db.Customers.ToListAsync();
        customers.Should().HaveCount(3);
        customers.Select(c => c.Type.ToString()).Should().BeEquivalentTo(["Retail", "Wholesale", "VIP"]);
    }

    [Fact]
    public async Task Seed_CreatesExpectedProducts_IncludingOutOfStock()
    {
        await DbSeeder.SeedAsync(_db);

        var products = await _db.Products.ToListAsync();
        products.Should().HaveCount(12);
        products.Count(p => p.InventoryQty == 0).Should().BeGreaterThanOrEqualTo(2, "FR-016 requires ≥2 out-of-stock");
    }

    [Fact]
    public async Task Seed_IsIdempotent()
    {
        await DbSeeder.SeedAsync(_db);
        await DbSeeder.SeedAsync(_db); // second call should be a no-op

        var customers = await _db.Customers.ToListAsync();
        customers.Should().HaveCount(3, "idempotent seed must not duplicate data");
    }

    [Fact]
    public async Task Seed_CreatesHistoricalInvoices_EstablishingCoPurchasePatterns()
    {
        await DbSeeder.SeedAsync(_db);

        // FR-016: seeded invoices exist for all 3 customers
        var customerIds = await _db.Customers.Select(c => c.Id).ToListAsync();
        foreach (var cid in customerIds)
        {
            var invoices = await _db.Invoices.Where(i => i.CustomerId == cid).ToListAsync();
            invoices.Should().NotBeEmpty($"customer {cid} should have historical invoices");
        }
    }

    [Fact]
    public async Task Seed_HasAlternatives_ForOutOfStockProducts()
    {
        await DbSeeder.SeedAsync(_db);

        // At least one out-of-stock product has an alternative (FR-007 exercises)
        var outOfStock = await _db.Products.Where(p => p.InventoryQty == 0).Select(p => p.Id).ToListAsync();
        var hasAlternative = await _db.ProductAlternatives
            .AnyAsync(pa => outOfStock.Contains(pa.ProductId));
        hasAlternative.Should().BeTrue("at least one out-of-stock product must have alternatives");
    }
}

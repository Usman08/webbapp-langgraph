using Microsoft.EntityFrameworkCore;
using SalesInvoice.Domain.Entities;
using SalesInvoice.Domain.Enums;

namespace SalesInvoice.Infrastructure.Persistence;

/// <summary>
/// Idempotent seed: only inserts data if the Customers table is empty.
/// Satisfies FR-015/016/021.
/// </summary>
public static class DbSeeder
{
    // Fixed IDs for idempotency
    private static readonly Guid RetailCustomerId = Guid.Parse("11111111-0000-0000-0000-000000000001");
    private static readonly Guid WholesaleCustomerId = Guid.Parse("11111111-0000-0000-0000-000000000002");
    private static readonly Guid VipCustomerId = Guid.Parse("11111111-0000-0000-0000-000000000003");

    // Products
    private static readonly Guid ProdA1 = Guid.Parse("22222222-0000-0000-0000-000000000001"); // Office Chair (Furniture)
    private static readonly Guid ProdA2 = Guid.Parse("22222222-0000-0000-0000-000000000002"); // Desk (Furniture)
    private static readonly Guid ProdA3 = Guid.Parse("22222222-0000-0000-0000-000000000003"); // Desk Lamp (Furniture)
    private static readonly Guid ProdB1 = Guid.Parse("22222222-0000-0000-0000-000000000004"); // Laptop (Electronics)
    private static readonly Guid ProdB2 = Guid.Parse("22222222-0000-0000-0000-000000000005"); // Keyboard (Electronics)
    private static readonly Guid ProdB3 = Guid.Parse("22222222-0000-0000-0000-000000000006"); // Mouse (Electronics) — OUT OF STOCK; no in-stock alt → BackOrder exercise
    private static readonly Guid ProdB4 = Guid.Parse("22222222-0000-0000-0000-000000000007"); // Monitor (Electronics) — OUT OF STOCK; alt = ProdB5
    private static readonly Guid ProdB5 = Guid.Parse("22222222-0000-0000-0000-000000000008"); // Monitor Pro (Electronics) — alt for ProdB4
    private static readonly Guid ProdC1 = Guid.Parse("22222222-0000-0000-0000-000000000009"); // Notebook (Stationery)
    private static readonly Guid ProdC2 = Guid.Parse("22222222-0000-0000-0000-000000000010"); // Pens Box (Stationery)
    private static readonly Guid ProdC3 = Guid.Parse("22222222-0000-0000-0000-000000000011"); // Stapler (Stationery) — OUT OF STOCK; alt also out of stock → BackOrder
    private static readonly Guid ProdC4 = Guid.Parse("22222222-0000-0000-0000-000000000012"); // Stapler Pro (Stationery) — alt for ProdC3; also OUT OF STOCK

    // Discount rules
    private static readonly Guid RetailRuleId = Guid.Parse("33333333-0000-0000-0000-000000000001");
    private static readonly Guid WholesaleRuleId = Guid.Parse("33333333-0000-0000-0000-000000000002");
    private static readonly Guid VipRuleId = Guid.Parse("33333333-0000-0000-0000-000000000003");

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.Customers.AnyAsync()) return;

        var now = DateTimeOffset.UtcNow;

        // --- Customers ---
        var retail = new Customer
        {
            Id = RetailCustomerId, Name = "ABC Traders", Type = CustomerType.Retail,
            DiscountTier = "retail-standard", ContactEmail = "orders@abctraders.com",
            CreatedAt = now,
        };
        var wholesale = new Customer
        {
            Id = WholesaleCustomerId, Name = "Global Supplies Ltd", Type = CustomerType.Wholesale,
            DiscountTier = "wholesale-bulk", ContactEmail = "procurement@globalsupplies.com",
            CreatedAt = now,
        };
        var vip = new Customer
        {
            Id = VipCustomerId, Name = "Premium Corp", Type = CustomerType.VIP,
            DiscountTier = "vip-standard", ContactEmail = "vip@premiumcorp.com",
            CreatedAt = now,
        };
        db.Customers.AddRange(retail, wholesale, vip);

        // --- Discount Rules ---
        db.DiscountRules.AddRange(
            new DiscountRule { Id = RetailRuleId, Key = "retail-standard", AppliesToType = CustomerType.Retail, Percentage = 5m, Active = true },
            new DiscountRule { Id = WholesaleRuleId, Key = "wholesale-bulk", AppliesToType = CustomerType.Wholesale, Percentage = 12m, Active = true },
            new DiscountRule { Id = VipRuleId, Key = "vip-standard", AppliesToType = CustomerType.VIP, Percentage = 20m, Active = true }
        );

        // --- Products ---
        db.Products.AddRange(
            new Product { Id = ProdA1, Sku = "FRN-001", Name = "Office Chair", Category = "Furniture", UnitPrice = 249.99m, InventoryQty = 50, CreatedAt = now },
            new Product { Id = ProdA2, Sku = "FRN-002", Name = "Adjustable Desk", Category = "Furniture", UnitPrice = 399.00m, InventoryQty = 30, CreatedAt = now },
            new Product { Id = ProdA3, Sku = "FRN-003", Name = "Desk Lamp", Category = "Furniture", UnitPrice = 49.99m, InventoryQty = 100, CreatedAt = now },
            new Product { Id = ProdB1, Sku = "ELC-001", Name = "Laptop 15\"", Category = "Electronics", UnitPrice = 1299.00m, InventoryQty = 20, CreatedAt = now },
            new Product { Id = ProdB2, Sku = "ELC-002", Name = "Mechanical Keyboard", Category = "Electronics", UnitPrice = 129.99m, InventoryQty = 75, CreatedAt = now },
            new Product { Id = ProdB3, Sku = "ELC-003", Name = "Wireless Mouse", Category = "Electronics", UnitPrice = 59.99m, InventoryQty = 0, CreatedAt = now }, // OUT OF STOCK, no in-stock alt
            new Product { Id = ProdB4, Sku = "ELC-004", Name = "Monitor 24\"", Category = "Electronics", UnitPrice = 329.00m, InventoryQty = 0, CreatedAt = now }, // OUT OF STOCK, alt = B5
            new Product { Id = ProdB5, Sku = "ELC-005", Name = "Monitor Pro 27\"", Category = "Electronics", UnitPrice = 449.00m, InventoryQty = 15, CreatedAt = now }, // alt for B4
            new Product { Id = ProdC1, Sku = "STN-001", Name = "Notebook A4 (Pack 10)", Category = "Stationery", UnitPrice = 12.99m, InventoryQty = 200, CreatedAt = now },
            new Product { Id = ProdC2, Sku = "STN-002", Name = "Pens Box (50pc)", Category = "Stationery", UnitPrice = 8.99m, InventoryQty = 150, CreatedAt = now },
            new Product { Id = ProdC3, Sku = "STN-003", Name = "Stapler Heavy Duty", Category = "Stationery", UnitPrice = 24.99m, InventoryQty = 0, CreatedAt = now }, // OUT OF STOCK; alt also OOS → BackOrder
            new Product { Id = ProdC4, Sku = "STN-004", Name = "Stapler Pro Heavy", Category = "Stationery", UnitPrice = 34.99m, InventoryQty = 0, CreatedAt = now }  // alt for C3; also OUT OF STOCK
        );

        // --- ProductAlternatives ---
        db.ProductAlternatives.AddRange(
            // Monitor 24" → Monitor Pro 27" (rank 1)
            new ProductAlternative { ProductId = ProdB4, AlternativeProductId = ProdB5, Rank = 1 },
            // Stapler Heavy Duty → Stapler Pro (rank 1) — both OOS → exercises BackOrder path
            new ProductAlternative { ProductId = ProdC3, AlternativeProductId = ProdC4, Rank = 1 }
            // Wireless Mouse has NO alternatives → pure BackOrder
        );

        await db.SaveChangesAsync();

        // --- Historical Invoices (FR-016: establishes co-purchase patterns) ---
        var taxRate = 10m; // fixed tax rate

        // Retail customer: 3 invoices — Chair+Desk co-purchase pattern, Keyboard+Mouse
        await SeedInvoiceAsync(db, RetailCustomerId, 5m, taxRate, now.AddMonths(-3),
            (ProdA1, 2), (ProdA2, 1), (ProdA3, 2));
        await SeedInvoiceAsync(db, RetailCustomerId, 5m, taxRate, now.AddMonths(-2),
            (ProdA1, 1), (ProdA2, 1), (ProdB2, 1));
        await SeedInvoiceAsync(db, RetailCustomerId, 5m, taxRate, now.AddMonths(-1),
            (ProdB1, 1), (ProdB2, 1), (ProdC1, 3));

        // Wholesale customer: 3 invoices — Electronics bulk pattern
        await SeedInvoiceAsync(db, WholesaleCustomerId, 12m, taxRate, now.AddMonths(-4),
            (ProdB1, 5), (ProdB2, 5));
        await SeedInvoiceAsync(db, WholesaleCustomerId, 12m, taxRate, now.AddMonths(-2),
            (ProdB1, 3), (ProdB2, 3), (ProdB4, 2));
        await SeedInvoiceAsync(db, WholesaleCustomerId, 12m, taxRate, now.AddMonths(-1),
            (ProdC1, 20), (ProdC2, 10));

        // VIP customer: 2 invoices
        await SeedInvoiceAsync(db, VipCustomerId, 20m, taxRate, now.AddMonths(-5),
            (ProdB1, 2), (ProdA1, 3), (ProdA2, 2));
        await SeedInvoiceAsync(db, VipCustomerId, 20m, taxRate, now.AddMonths(-1),
            (ProdB1, 1), (ProdB5, 1), (ProdC1, 5), (ProdC2, 5));
    }

    private static async Task SeedInvoiceAsync(
        AppDbContext db,
        Guid customerId,
        decimal discountPct,
        decimal taxPct,
        DateTimeOffset date,
        params (Guid productId, int qty)[] lines)
    {
        // Look up prices
        var productIds = lines.Select(l => l.productId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var lineItems = lines.Select(l => new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            ProductId = l.productId,
            Quantity = l.qty,
            UnitPrice = products[l.productId].UnitPrice,
            LineTotal = l.qty * products[l.productId].UnitPrice,
            StockStatus = LineStockStatus.InStock,
        }).ToList();

        var subtotal = lineItems.Sum(li => li.LineTotal);
        var discountAmt = Math.Round(subtotal * discountPct / 100m, 2, MidpointRounding.AwayFromZero);
        var taxBase = subtotal - discountAmt;
        var taxAmt = Math.Round(taxBase * taxPct / 100m, 2, MidpointRounding.AwayFromZero);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            InvoiceDate = date,
            Subtotal = subtotal,
            DiscountPercentage = discountPct,
            DiscountAmount = discountAmt,
            TaxPercentage = taxPct,
            TaxAmount = taxAmt,
            Total = taxBase + taxAmt,
            Status = InvoiceStatus.Finalised,
            CreatedAt = date,
            FinalisedAt = date.AddMinutes(5),
            LineItems = lineItems,
        };

        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
    }
}

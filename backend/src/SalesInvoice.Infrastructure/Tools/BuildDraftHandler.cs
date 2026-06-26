using Microsoft.EntityFrameworkCore;
using SalesInvoice.Domain.Entities;
using SalesInvoice.Domain.Enums;
using SalesInvoice.Domain.Pricing;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Infrastructure.Tools;

public record DraftLine(Guid ProductId, int Quantity, string StockStatus);

public record BuildDraftRequest(Guid WorkflowRunId, Guid CustomerId, List<DraftLine> Lines, decimal DiscountPercentage);

public record BuildDraftResponse(Guid InvoiceId, decimal Subtotal, decimal DiscountAmount, decimal TaxAmount, decimal Total, string Status);

public class BuildDraftHandler(AppDbContext db)
{
    private const decimal FixedTaxRate = 10m;

    public async Task<BuildDraftResponse> HandleAsync(BuildDraftRequest request)
    {
        var productIds = request.Lines.Select(l => l.ProductId).ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var lineInputs = request.Lines.Select(l => new LineInput(
            l.Quantity,
            products[l.ProductId].UnitPrice,
            Enum.Parse<LineStockStatus>(l.StockStatus))).ToList();

        var totals = InvoiceCalculator.Calculate(lineInputs, request.DiscountPercentage, FixedTaxRate);

        var lineItems = request.Lines.Select(l => new InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            ProductId = l.ProductId,
            Quantity = l.Quantity,
            UnitPrice = products[l.ProductId].UnitPrice,
            LineTotal = l.Quantity * products[l.ProductId].UnitPrice,
            StockStatus = Enum.Parse<LineStockStatus>(l.StockStatus),
        }).ToList();

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            CustomerId = request.CustomerId,
            WorkflowRunId = request.WorkflowRunId,
            InvoiceDate = DateTimeOffset.UtcNow,
            Subtotal = totals.Subtotal,
            DiscountPercentage = totals.DiscountPercentage,
            DiscountAmount = totals.DiscountAmount,
            TaxPercentage = totals.TaxPercentage,
            TaxAmount = totals.TaxAmount,
            Total = totals.Total,
            Status = InvoiceStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
            LineItems = lineItems,
        };

        db.Invoices.Add(invoice);

        // Link invoice back to the workflow run
        var run = await db.WorkflowRuns.FindAsync(request.WorkflowRunId);
        if (run is not null)
        {
            run.InvoiceId = invoice.Id;
            run.Status = RunStatus.AwaitingApproval;
            run.CompletedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync();

        return new BuildDraftResponse(invoice.Id, totals.Subtotal, totals.DiscountAmount, totals.TaxAmount, totals.Total, "Draft");
    }
}


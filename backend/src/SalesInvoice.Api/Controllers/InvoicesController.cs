using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInvoice.Application.DTOs;
using SalesInvoice.Domain.Enums;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController(AppDbContext db) : ControllerBase
{
    [HttpGet("{invoiceId:guid}")]
    public async Task<IActionResult> GetInvoice(Guid invoiceId)
    {
        var invoice = await db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.LineItems)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice is null) return NotFound();

        return Ok(MapToDto(invoice));
    }

    [HttpGet]
    public async Task<IActionResult> GetInvoices([FromQuery] string? status)
    {
        var query = db.Invoices
            .Include(i => i.Customer)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<InvoiceStatus>(status, true, out var statusEnum))
            query = query.Where(i => i.Status == statusEnum);

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new
            {
                id = i.Id,
                customer = new { i.Customer.Id, i.Customer.Name, type = i.Customer.Type.ToString() },
                date = i.InvoiceDate,
                total = i.Total,
                status = i.Status.ToString(),
            })
            .ToListAsync();

        return Ok(invoices);
    }

    [HttpGet("{invoiceId:guid}/workflow")]
    public async Task<IActionResult> GetWorkflowTrail(Guid invoiceId)
    {
        var invoice = await db.Invoices
            .Include(i => i.WorkflowRun)
            .ThenInclude(r => r!.Steps.OrderBy(s => s.Sequence))
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice is null) return NotFound();
        if (invoice.WorkflowRun is null) return Ok(new List<object>());

        var steps = invoice.WorkflowRun.Steps
            .Select(s => new WorkflowStepDto(s.Id, s.Sequence, s.Name, s.ToolInvoked,
                s.InputPayload, s.OutputResult, s.IsException, s.Timestamp))
            .ToList();

        return Ok(steps);
    }

    [HttpPost("{invoiceId:guid}/approve")]
    public async Task<IActionResult> Approve(Guid invoiceId)
    {
        var invoice = await db.Invoices.FindAsync(invoiceId);
        if (invoice is null) return NotFound();
        if (invoice.Status == InvoiceStatus.Finalised)
            return Conflict(new { title = "Invoice already finalised.", status = 409 });

        invoice.Status = InvoiceStatus.Finalised;
        invoice.FinalisedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        return Ok(new { id = invoice.Id, status = invoice.Status.ToString(), finalisedAt = invoice.FinalisedAt });
    }

    [HttpPost("{invoiceId:guid}/reject")]
    public async Task<IActionResult> Reject(Guid invoiceId)
    {
        var invoice = await db.Invoices.FindAsync(invoiceId);
        if (invoice is null) return NotFound();
        // Status stays Draft — unlock editing
        await db.SaveChangesAsync();
        return Ok(new { id = invoice.Id, status = invoice.Status.ToString() });
    }

    [HttpPut("{invoiceId:guid}/lines")]
    public async Task<IActionResult> EditLines(Guid invoiceId, [FromBody] List<EditLineBody> lines)
    {
        var invoice = await db.Invoices
            .Include(i => i.LineItems)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);

        if (invoice is null) return NotFound();
        if (invoice.Status == InvoiceStatus.Finalised)
            return Conflict(new { title = "Cannot edit a finalised invoice.", status = 409 });

        // Replace all line items
        db.InvoiceLineItems.RemoveRange(invoice.LineItems);
        await db.SaveChangesAsync();

        var productIds = lines.Select(l => l.ProductId).ToList();
        var products = await db.Products.Where(p => productIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);

        var newLines = lines.Select(l => new SalesInvoice.Domain.Entities.InvoiceLineItem
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            ProductId = l.ProductId,
            Quantity = l.Quantity,
            UnitPrice = products[l.ProductId].UnitPrice,
            LineTotal = l.Quantity * products[l.ProductId].UnitPrice,
            StockStatus = Enum.Parse<LineStockStatus>(l.StockStatus),
        }).ToList();

        db.InvoiceLineItems.AddRange(newLines);

        // Recalculate totals
        var lineInputs = newLines.Select(l => new SalesInvoice.Domain.Pricing.LineInput(l.Quantity, l.UnitPrice, l.StockStatus)).ToList();
        var totals = SalesInvoice.Domain.Pricing.InvoiceCalculator.Calculate(lineInputs, invoice.DiscountPercentage, invoice.TaxPercentage, includeBackOrder: true);

        invoice.Subtotal = totals.Subtotal;
        invoice.DiscountAmount = totals.DiscountAmount;
        invoice.TaxAmount = totals.TaxAmount;
        invoice.Total = totals.Total;

        await db.SaveChangesAsync();

        // Reload with product info
        invoice = (await db.Invoices
            .Include(i => i.Customer)
            .Include(i => i.LineItems)
            .ThenInclude(l => l.Product)
            .FirstOrDefaultAsync(i => i.Id == invoiceId))!;

        return Ok(MapToDto(invoice));
    }

    private static InvoiceDto MapToDto(SalesInvoice.Domain.Entities.Invoice invoice)
    {
        var customerDto = new CustomerDto(
            invoice.Customer.Id, invoice.Customer.Name, invoice.Customer.Type.ToString(),
            invoice.Customer.DiscountTier, invoice.Customer.ContactEmail);

        var lines = invoice.LineItems.Select(l => new LineItemDto(
            l.Id, l.ProductId, l.Product.Sku, l.Product.Name,
            l.Quantity, l.UnitPrice, l.LineTotal, l.StockStatus.ToString())).ToList();

        return new InvoiceDto(
            invoice.Id, invoice.Status.ToString(), customerDto, lines,
            invoice.Subtotal, invoice.DiscountPercentage, invoice.DiscountAmount,
            invoice.TaxPercentage, invoice.TaxAmount, invoice.Total,
            invoice.WorkflowRunId, invoice.CreatedAt, invoice.FinalisedAt);
    }
}

public record EditLineBody(Guid ProductId, int Quantity, string StockStatus);

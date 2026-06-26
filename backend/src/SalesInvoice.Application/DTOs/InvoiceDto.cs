namespace SalesInvoice.Application.DTOs;

public record LineItemDto(
    Guid Id, Guid ProductId, string Sku, string Name,
    int Quantity, decimal UnitPrice, decimal LineTotal, string StockStatus);

public record InvoiceDto(
    Guid Id, string Status, CustomerDto Customer,
    List<LineItemDto> LineItems,
    decimal Subtotal, decimal DiscountPercentage, decimal DiscountAmount,
    decimal TaxPercentage, decimal TaxAmount, decimal Total,
    Guid? WorkflowRunId, DateTimeOffset CreatedAt, DateTimeOffset? FinalisedAt);

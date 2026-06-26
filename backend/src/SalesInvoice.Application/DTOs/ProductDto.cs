namespace SalesInvoice.Application.DTOs;

public record ProductDto(Guid Id, string Sku, string Name, string Category, decimal UnitPrice, int InventoryQty);

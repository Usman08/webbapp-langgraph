namespace SalesInvoice.Application.DTOs;

public record CustomerDto(Guid Id, string Name, string Type, string? DiscountTier, string? ContactEmail);

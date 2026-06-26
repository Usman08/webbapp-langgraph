using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesInvoice.Application.DTOs;
using SalesInvoice.Infrastructure.Persistence;

namespace SalesInvoice.Api.Controllers;

[ApiController]
[Route("api")]
public class ReferenceController(AppDbContext db) : ControllerBase
{
    [HttpGet("customers")]
    public async Task<IActionResult> GetCustomers()
    {
        var customers = await db.Customers
            .OrderBy(c => c.Name)
            .Select(c => new CustomerDto(c.Id, c.Name, c.Type.ToString(), c.DiscountTier, c.ContactEmail))
            .ToListAsync();
        return Ok(customers);
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await db.Products
            .OrderBy(p => p.Category).ThenBy(p => p.Name)
            .Select(p => new ProductDto(p.Id, p.Sku, p.Name, p.Category, p.UnitPrice, p.InventoryQty))
            .ToListAsync();
        return Ok(products);
    }
}

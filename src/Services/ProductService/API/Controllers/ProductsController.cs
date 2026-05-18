using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProductService.Application.Commands;
using ProductService.Application.Queries;

namespace ProductService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class ProductsController : ControllerBase
{
    private readonly ISender _sender;

    public ProductsController(ISender sender) => _sender = sender;

    // GET /api/products?search=&lowStockOnly=false&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetProducts(
        [FromQuery] string? search,
        [FromQuery] bool lowStockOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _sender.Send(
            new GetProductsQuery(search, lowStockOnly, page, pageSize), ct);
        return Ok(result);
    }

    // GET /api/products/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProduct(Guid id, CancellationToken ct)
    {
        var product = await _sender.Send(new GetProductByIdQuery(id), ct);
        return product is null ? NotFound() : Ok(product);
    }

    // POST /api/products
    [HttpPost]
    public async Task<IActionResult> CreateProduct(
        [FromBody] CreateProductCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetProduct), new { id = result.Value }, new { id = result.Value });
    }

    // PATCH /api/products/{id}/stock
    [HttpPatch("{id:guid}/stock")]
    public async Task<IActionResult> IncreaseStock(
        Guid id,
        [FromBody] IncreaseStockRequest request,
        CancellationToken ct)
    {
        var result = await _sender.Send(new IncreaseStockCommand(id, request.Quantity), ct);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }

    // GET /api/products/search?q=&page=1&pageSize=20
    [HttpGet("search")]
    [AllowAnonymous]
    public async Task<IActionResult> SearchProducts(
        [FromQuery] string q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q))
            return BadRequest(new { error = "Search query 'q' is required." });

        var result = await _sender.Send(new SearchProductsQuery(q, page, pageSize), ct);
        return Ok(result);
    }

    // GET /api/products/{id}/history — Stok değişiklik geçmişi (Event Sourcing)
    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetStockHistory(Guid id, CancellationToken ct)
    {
        var events = await _sender.Send(new GetStockHistoryQuery(id), ct);
        return Ok(events);
    }
}

public sealed record IncreaseStockRequest(int Quantity);

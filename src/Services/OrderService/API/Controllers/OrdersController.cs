using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderService.Application.Commands;
using OrderService.Application.Queries;

namespace OrderService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class OrdersController : ControllerBase
{
    private readonly ISender _sender;

    public OrdersController(ISender sender) => _sender = sender;

    // GET /api/orders?email=&status=&page=1&pageSize=20
    [HttpGet]
    public async Task<IActionResult> GetOrders(
        [FromQuery] string? email,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetOrdersQuery(email, status is not null ? Enum.Parse<Domain.Entities.OrderStatus>(status, true) : null, page, pageSize);
        var result = await _sender.Send(query, ct);
        return Ok(result);
    }

    // GET /api/orders/{id}
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken ct)
    {
        var order = await _sender.Send(new GetOrderByIdQuery(id), ct);
        return order is null ? NotFound() : Ok(order);
    }

    // POST /api/orders
    [HttpPost]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderCommand command,
        CancellationToken ct)
    {
        var result = await _sender.Send(command, ct);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return CreatedAtAction(nameof(GetOrder), new { id = result.Value }, new { id = result.Value });
    }

    // DELETE /api/orders/{id}?reason=
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> CancelOrder(Guid id, [FromQuery] string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return BadRequest(new { error = "Cancellation reason is required." });

        var result = await _sender.Send(new CancelOrderCommand(id, reason), ct);
        if (result.IsFailure)
            return BadRequest(new { error = result.Error });

        return NoContent();
    }
}

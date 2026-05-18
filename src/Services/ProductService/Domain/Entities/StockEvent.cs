namespace ProductService.Domain.Entities;

public sealed class StockEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid ProductId { get; init; }
    public string EventType { get; init; } = string.Empty;  // Created, Decreased, Increased, Reserved, Released
    public int OldStock { get; init; }
    public int NewStock { get; init; }
    public int Quantity { get; init; }
    public string? ReferenceId { get; init; }  // orderId, shipmentId etc.
    public string? Description { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

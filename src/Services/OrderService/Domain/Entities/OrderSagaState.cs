using MassTransit;

namespace OrderService.Domain.Entities;

public sealed class OrderSagaState : SagaStateMachineInstance
{
    public Guid CorrelationId { get; set; }
    public string CurrentState { get; set; } = string.Empty;

    public Guid OrderId { get; set; }
    public string CustomerEmail { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }

    // gRPC reservation reference
    public bool StockReserved { get; set; }

    // Timestamps
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? FailReason { get; set; }

    // RowVersion for optimistic concurrency
    public byte[]? RowVersion { get; set; }
}

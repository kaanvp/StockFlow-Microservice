using StockFlow.Shared.Domain;
using OrderService.Domain.Events;

namespace OrderService.Domain.Entities;

public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled
}

public sealed class Order : BaseEntity
{
    public string CustomerEmail { get; private set; } = string.Empty;
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public int Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TotalPrice { get; private set; }
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
    public string? CancelReason { get; private set; }

    private Order() { }

    public static Order Create(string customerEmail, Guid productId, string productName,
        int quantity, decimal unitPrice)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            CustomerEmail = customerEmail,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TotalPrice = quantity * unitPrice,
            Status = OrderStatus.Pending
        };

        order.RaiseDomainEvent(new OrderCreatedDomainEvent(
            Guid.NewGuid(), DateTime.UtcNow, order.Id, order.CustomerEmail,
            order.ProductId, order.ProductName, order.Quantity, order.TotalPrice));

        return order;
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException($"Cannot confirm order in '{Status}' status.");

        Status = OrderStatus.Confirmed;
    }

    public void Cancel(string reason)
    {
        if (Status == OrderStatus.Cancelled)
            throw new InvalidOperationException("Order is already cancelled.");

        Status = OrderStatus.Cancelled;
        CancelReason = reason;

        RaiseDomainEvent(new OrderCancelledDomainEvent(
            Guid.NewGuid(), DateTime.UtcNow, Id, CustomerEmail, reason));
    }
}

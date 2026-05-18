using StockFlow.Shared.Domain;

namespace OrderService.Domain.Events;

public sealed record OrderCreatedDomainEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    string CustomerEmail,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal TotalPrice
) : IDomainEvent;

public sealed record OrderCancelledDomainEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    string CustomerEmail,
    string Reason
) : IDomainEvent;

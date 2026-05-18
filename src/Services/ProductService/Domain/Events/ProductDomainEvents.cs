using StockFlow.Shared.Domain;

namespace ProductService.Domain.Events;

public sealed record ProductCreatedDomainEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ProductId,
    string ProductName,
    string Sku
) : IDomainEvent;

public sealed record StockDecreasedDomainEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ProductId,
    string ProductName,
    string Sku,
    int OldStock,
    int NewStock
) : IDomainEvent;

public sealed record StockIncreasedDomainEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ProductId,
    string ProductName,
    string Sku,
    int OldStock,
    int NewStock
) : IDomainEvent;

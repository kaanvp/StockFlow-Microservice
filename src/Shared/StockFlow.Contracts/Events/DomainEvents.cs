using StockFlow.Shared.Domain;

namespace StockFlow.Contracts.Events;

// ─── Saga Events ────────────────────────────────────────────────

public sealed record CheckStockCommand(Guid OrderId, Guid ProductId, int Quantity);

public sealed record StockReservedEvent(Guid OrderId, Guid ProductId, int Quantity);

public sealed record StockReservationFailedEvent(Guid OrderId, Guid ProductId, int Quantity, string Reason);

public sealed record OrderCompensationEvent(Guid OrderId, Guid ProductId, int Quantity);

public sealed record OrderConfirmedEvent(Guid EventId, DateTime OccurredAt, Guid OrderId, string CustomerEmail);

// ─── Order Events ───────────────────────────────────────────────

public sealed record OrderCreatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    string CustomerEmail,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal TotalPrice
) : IDomainEvent;

public sealed record OrderCancelledEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid OrderId,
    string CustomerEmail,
    string Reason
) : IDomainEvent;

// ─── Stock Events ────────────────────────────────────────────────

public sealed record StockUpdatedEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ProductId,
    string ProductName,
    int OldStock,
    int NewStock
) : IDomainEvent;

public sealed record LowStockAlertEvent(
    Guid EventId,
    DateTime OccurredAt,
    Guid ProductId,
    string ProductName,
    string Sku,
    int CurrentStock,
    int Threshold
) : IDomainEvent;

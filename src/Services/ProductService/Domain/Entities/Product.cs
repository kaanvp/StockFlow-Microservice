using StockFlow.Shared.Domain;
using StockFlow.Contracts.Events;
using ProductService.Domain.Events;

namespace ProductService.Domain.Entities;

public sealed class Product : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Sku { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public int Stock { get; private set; }
    public int LowStockThreshold { get; private set; } = 10;

    public bool IsLowStock => Stock <= LowStockThreshold;

    private Product() { }

    public static Product Create(string name, string description, string sku, decimal price, int initialStock, int lowStockThreshold = 10)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Name = name,
            Description = description,
            Sku = sku,
            Price = price,
            Stock = initialStock,
            LowStockThreshold = lowStockThreshold
        };

        product.RaiseDomainEvent(new ProductCreatedDomainEvent(
            Guid.NewGuid(), DateTime.UtcNow, product.Id, product.Name, product.Sku));

        return product;
    }

    public void DecreaseStock(int quantity, string? referenceId = null, string? description = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        if (Stock < quantity)
            throw new InvalidOperationException(
                $"Insufficient stock for product '{Name}'. Available: {Stock}, Requested: {quantity}");

        var oldStock = Stock;
        Stock -= quantity;

        AddStockEvent("Decreased", oldStock, Stock, quantity, referenceId, description);

        RaiseDomainEvent(new StockDecreasedDomainEvent(
            Guid.NewGuid(), DateTime.UtcNow, Id, Name, Sku, oldStock, Stock));

        if (IsLowStock)
            RaiseDomainEvent(new LowStockAlertEvent(
                Guid.NewGuid(), DateTime.UtcNow, Id, Name, Sku, Stock, LowStockThreshold));
    }

    public void IncreaseStock(int quantity, string? referenceId = null, string? description = null)
    {
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be positive.", nameof(quantity));

        var oldStock = Stock;
        Stock += quantity;

        AddStockEvent("Increased", oldStock, Stock, quantity, referenceId, description);

        RaiseDomainEvent(new StockIncreasedDomainEvent(
            Guid.NewGuid(), DateTime.UtcNow, Id, Name, Sku, oldStock, Stock));
    }

    public void Update(string name, string description, decimal price, int lowStockThreshold)
    {
        Name = name;
        Description = description;
        Price = price;
        LowStockThreshold = lowStockThreshold;
    }

    // ─── Event Sourcing ────────────────────────────────────────────
    private readonly List<StockEvent> _stockEvents = new();
    public IReadOnlyCollection<StockEvent> StockEvents => _stockEvents.AsReadOnly();

    private void AddStockEvent(string eventType, int oldStock, int newStock, int quantity,
        string? referenceId, string? description)
    {
        _stockEvents.Add(new StockEvent
        {
            ProductId = Id,
            EventType = eventType,
            OldStock = oldStock,
            NewStock = newStock,
            Quantity = quantity,
            ReferenceId = referenceId,
            Description = description ?? eventType switch
            {
                "Decreased" => $"Stock decreased by {quantity}",
                "Increased" => $"Stock increased by {quantity}",
                _ => $"Stock {eventType.ToLower()} by {quantity}"
            }
        });
    }
}

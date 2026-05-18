using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using StockFlow.Contracts.Events;

namespace NotificationService.Consumers;

public sealed class LowStockAlertConsumer : IConsumer<LowStockAlertEvent>
{
    private readonly IHubContext<StockHub> _hubContext;
    private readonly ILogger<LowStockAlertConsumer> _logger;

    public LowStockAlertConsumer(IHubContext<StockHub> hubContext, ILogger<LowStockAlertConsumer> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<LowStockAlertEvent> context)
    {
        var @event = context.Message;

        _logger.LogWarning(
            "Low stock alert for product {ProductName} (SKU: {Sku}). Current stock: {CurrentStock}, Threshold: {Threshold}",
            @event.ProductName, @event.Sku, @event.CurrentStock, @event.Threshold);

        // SignalR → product-{id} grubuna bildirim
        await _hubContext.Clients.Group($"product-{@event.ProductId}").SendAsync("LowStockAlert", new
        {
            @event.ProductId,
            @event.ProductName,
            @event.Sku,
            @event.CurrentStock,
            @event.Threshold,
            @event.OccurredAt
        });

        // SignalR → admins grubuna da bildirim
        await _hubContext.Clients.Group("admins").SendAsync("LowStockAlert", new
        {
            @event.ProductId,
            @event.ProductName,
            @event.Sku,
            @event.CurrentStock,
            @event.Threshold,
            @event.OccurredAt
        });
    }
}

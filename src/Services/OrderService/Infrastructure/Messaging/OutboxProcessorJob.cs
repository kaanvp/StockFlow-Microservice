using MassTransit;
using Microsoft.EntityFrameworkCore;
using OrderService.Infrastructure.Persistence;
using StockFlow.Shared.Outbox;
using System.Text.Json;

namespace OrderService.Infrastructure.Messaging;

public sealed class OutboxProcessorJob : BackgroundService
{
    private static readonly Dictionary<string, Type> _eventTypeMap = new()
    {
        [typeof(OrderService.Domain.Events.OrderCreatedDomainEvent).FullName!]   = typeof(OrderService.Domain.Events.OrderCreatedDomainEvent),
        [typeof(OrderService.Domain.Events.OrderCancelledDomainEvent).FullName!] = typeof(OrderService.Domain.Events.OrderCancelledDomainEvent),
        [typeof(StockFlow.Contracts.Events.OrderCreatedEvent).FullName!]   = typeof(StockFlow.Contracts.Events.OrderCreatedEvent),
        [typeof(StockFlow.Contracts.Events.OrderCancelledEvent).FullName!] = typeof(StockFlow.Contracts.Events.OrderCancelledEvent),
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorJob> _logger;

    public OutboxProcessorJob(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessorJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxMessagesAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db        = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 3)
            .OrderBy(m => m.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                if (_eventTypeMap.TryGetValue(message.Type, out var eventType))
                {
                    var @event = JsonSerializer.Deserialize(message.Payload, eventType);
                    if (@event is not null)
                    {
                        // Domain event'leri contract event'lere dönüştür (NotificationService tüketir)
                        object? contractEvent = @event switch
                        {
                            OrderService.Domain.Events.OrderCreatedDomainEvent e =>
                                new StockFlow.Contracts.Events.OrderCreatedEvent(
                                    e.EventId, e.OccurredAt, e.OrderId, e.CustomerEmail,
                                    e.ProductId, e.ProductName, e.Quantity, e.TotalPrice),
                            OrderService.Domain.Events.OrderCancelledDomainEvent e =>
                                new StockFlow.Contracts.Events.OrderCancelledEvent(
                                    e.EventId, e.OccurredAt, e.OrderId, e.CustomerEmail, e.Reason),
                            _ => @event
                        };

                        await publisher.Publish(contractEvent!, ct);
                    }
                }
                else
                {
                    _logger.LogWarning("Unknown outbox event type: {Type}", message.Type);
                }

                message.ProcessedAt = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                message.RetryCount++;
                message.Error = ex.Message;
            }
        }

        if (messages.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}

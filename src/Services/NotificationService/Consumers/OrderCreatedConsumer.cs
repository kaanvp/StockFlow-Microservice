using MassTransit;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using NotificationService.Services;
using StockFlow.Contracts.Events;

namespace NotificationService.Consumers;

public sealed class OrderCreatedConsumer : IConsumer<OrderCreatedEvent>
{
    private readonly IHubContext<StockHub> _hubContext;
    private readonly EmailService _emailService;
    private readonly ILogger<OrderCreatedConsumer> _logger;

    public OrderCreatedConsumer(
        IHubContext<StockHub> hubContext,
        EmailService emailService,
        ILogger<OrderCreatedConsumer> logger)
    {
        _hubContext = hubContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderCreatedEvent> context)
    {
        var @event = context.Message;

        _logger.LogInformation(
            "Order created: {OrderId} for {CustomerEmail}, total {TotalPrice:F2}",
            @event.OrderId, @event.CustomerEmail, @event.TotalPrice);

        // SignalR → admins grubuna broadcast
        await _hubContext.Clients.Group("admins").SendAsync("OrderCreated", new
        {
            @event.OrderId,
            @event.CustomerEmail,
            @event.ProductId,
            @event.Quantity,
            @event.TotalPrice,
            @event.OccurredAt
        });

        // MailKit ile e-posta (Mailhog geliştirme SMTP sunucusuna)
        await _emailService.SendOrderConfirmationAsync(
            @event.CustomerEmail,
            @event.OrderId.ToString(),
            @event.ProductName,
            @event.Quantity,
            @event.TotalPrice);
    }
}

using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Hubs;

public sealed class StockHub : Hub
{
    private readonly ILogger<StockHub> _logger;

    public StockHub(ILogger<StockHub> logger)
    {
        _logger = logger;
    }

    public async Task JoinProductGroup(string productId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"product-{productId}");
        _logger.LogInformation("Connection {Id} joined product group {ProductId}", Context.ConnectionId, productId);
    }

    public async Task LeaveProductGroup(string productId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"product-{productId}");
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier ?? Context.ConnectionId;

        // userId "admin" ise admins grubuna ekle
        if (string.Equals(userId, "admin", StringComparison.OrdinalIgnoreCase))
            await Groups.AddToGroupAsync(Context.ConnectionId, "admins");

        _logger.LogInformation("SignalR client connected: {UserId} ({ConnectionId})", userId, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("SignalR client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

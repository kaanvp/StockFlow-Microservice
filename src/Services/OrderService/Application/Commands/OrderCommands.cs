using Grpc.Net.Client;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;
using OrderService.Infrastructure.Persistence;
using StockFlow.Contracts.Grpc;
using StockFlow.Shared.Common;

namespace OrderService.Application.Commands;

// ─── Create Order ───────────────────────────────────────────────

public sealed record CreateOrderCommand(
    string CustomerEmail,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
) : IRequest<Result<Guid>>;

public sealed class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly OrderDbContext _db;
    private readonly StockGrpc.StockGrpcClient _stockClient;

    public CreateOrderCommandHandler(OrderDbContext db, StockGrpc.StockGrpcClient stockClient)
    {
        _db = db;
        _stockClient = stockClient;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken ct)
    {
        // 1. gRPC CheckStock
        var checkResponse = await _stockClient.CheckStockAsync(
            new CheckStockRequest
            {
                ProductId = request.ProductId.ToString(),
                Quantity = request.Quantity
            }, cancellationToken: ct);

        if (!checkResponse.IsAvailable)
            return Result.Failure<Guid>(
                $"Insufficient stock for product '{request.ProductName}'. Available: {checkResponse.CurrentStock}, Requested: {request.Quantity}");

        // 2. gRPC ReserveStock
        var orderId = Guid.NewGuid();
        var reserveResponse = await _stockClient.ReserveStockAsync(
            new ReserveStockRequest
            {
                ProductId = request.ProductId.ToString(),
                Quantity = request.Quantity,
                OrderId = orderId.ToString()
            }, cancellationToken: ct);

        if (!reserveResponse.Success)
            return Result.Failure<Guid>($"Stock reservation failed: {reserveResponse.Message}");

        // 3. Order.Create + Confirm
        var order = Order.Create(
            request.CustomerEmail,
            request.ProductId,
            request.ProductName,
            request.Quantity,
            request.UnitPrice);

        order.Confirm();
        _db.Orders.Add(order);

        // 4. SaveChanges → OutboxMessage yazılır (OrderCreatedEvent)
        await _db.SaveChangesAsync(ct);

        return Result.Success(order.Id);
    }
}

// ─── Cancel Order ────────────────────────────────────────────────

public sealed record CancelOrderCommand(Guid OrderId, string Reason) : IRequest<Result>;

public sealed class CancelOrderCommandHandler : IRequestHandler<CancelOrderCommand, Result>
{
    private readonly OrderDbContext _db;
    private readonly StockGrpc.StockGrpcClient _stockClient;

    public CancelOrderCommandHandler(OrderDbContext db, StockGrpc.StockGrpcClient stockClient)
    {
        _db = db;
        _stockClient = stockClient;
    }

    public async Task<Result> Handle(CancelOrderCommand request, CancellationToken ct)
    {
        // 1. Order'ı bul
        var order = await _db.Orders.FindAsync([request.OrderId], ct);
        if (order is null)
            return Result.Failure($"Order '{request.OrderId}' not found.");

        // 2. Order.Cancel → domain event raise
        order.Cancel(request.Reason);

        // 3. gRPC ReleaseStock (kompanzasyon)
        await _stockClient.ReleaseStockAsync(
            new ReleaseStockRequest
            {
                ProductId = order.ProductId.ToString(),
                Quantity = order.Quantity,
                OrderId = order.Id.ToString()
            }, cancellationToken: ct);

        // 4. SaveChanges → OutboxMessage yazılır (OrderCancelledEvent)
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }
}

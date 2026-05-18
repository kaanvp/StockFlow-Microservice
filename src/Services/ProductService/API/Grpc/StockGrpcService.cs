using Grpc.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Persistence;
using StockFlow.Contracts.Grpc;

namespace ProductService.API.Grpc;

public sealed class StockGrpcService : StockGrpc.StockGrpcBase
{
    private readonly ProductDbContext _db;
    private readonly ILogger<StockGrpcService> _logger;

    public StockGrpcService(ProductDbContext db, ILogger<StockGrpcService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public override async Task<CheckStockResponse> CheckStock(
        CheckStockRequest request, ServerCallContext context)
    {
        var productId = Guid.Parse(request.ProductId);
        var product = await _db.Products.FindAsync([productId], context.CancellationToken);

        if (product is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {request.ProductId} not found."));

        return new CheckStockResponse
        {
            IsAvailable = product.Stock >= request.Quantity,
            CurrentStock = product.Stock
        };
    }

    public override async Task<ReserveStockResponse> ReserveStock(
        ReserveStockRequest request, ServerCallContext context)
    {
        var productId = Guid.Parse(request.ProductId);
        var product = await _db.Products.FindAsync([productId], context.CancellationToken);

        if (product is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {request.ProductId} not found."));

        try
        {
            product.DecreaseStock(request.Quantity);
            await _db.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation("Reserved {Qty} units of {ProductId} for order {OrderId}",
                request.Quantity, request.ProductId, request.OrderId);

            return new ReserveStockResponse { Success = true, Message = "Stock reserved successfully." };
        }
        catch (InvalidOperationException ex)
        {
            return new ReserveStockResponse { Success = false, Message = ex.Message };
        }
    }

    public override async Task<ReleaseStockResponse> ReleaseStock(
        ReleaseStockRequest request, ServerCallContext context)
    {
        var productId = Guid.Parse(request.ProductId);
        var product = await _db.Products.FindAsync([productId], context.CancellationToken);

        if (product is null)
            throw new RpcException(new Status(StatusCode.NotFound, $"Product {request.ProductId} not found."));

        product.IncreaseStock(request.Quantity);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation("Released {Qty} units of {ProductId} for order {OrderId}",
            request.Quantity, request.ProductId, request.OrderId);

        return new ReleaseStockResponse { Success = true, Message = "Stock released successfully." };
    }
}

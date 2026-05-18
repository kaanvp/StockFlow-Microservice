using MediatR;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Entities;
using ProductService.Infrastructure.Persistence;
using StockFlow.Shared.Common;

namespace ProductService.Application.Commands;

// ─── Create Product ──────────────────────────────────────────────

public sealed record CreateProductCommand(
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int InitialStock,
    int LowStockThreshold = 10
) : IRequest<Result<Guid>>;

public sealed class CreateProductCommandHandler : IRequestHandler<CreateProductCommand, Result<Guid>>
{
    private readonly ProductDbContext _db;

    public CreateProductCommandHandler(ProductDbContext db) => _db = db;

    public async Task<Result<Guid>> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var skuExists = await _db.Products.AnyAsync(p => p.Sku == request.Sku, ct);
        if (skuExists)
            return Result.Failure<Guid>($"A product with SKU '{request.Sku}' already exists.");

        var product = Product.Create(
            request.Name,
            request.Description,
            request.Sku,
            request.Price,
            request.InitialStock,
            request.LowStockThreshold);

        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);

        return Result.Success(product.Id);
    }
}

// ─── Increase Stock ──────────────────────────────────────────────

public sealed record IncreaseStockCommand(Guid ProductId, int Quantity) : IRequest<Result>;

public sealed class IncreaseStockCommandHandler : IRequestHandler<IncreaseStockCommand, Result>
{
    private readonly ProductDbContext _db;

    public IncreaseStockCommandHandler(ProductDbContext db) => _db = db;

    public async Task<Result> Handle(IncreaseStockCommand request, CancellationToken ct)
    {
        var product = await _db.Products.FindAsync([request.ProductId], ct);
        if (product is null)
            return Result.Failure($"Product '{request.ProductId}' not found.");

        product.IncreaseStock(request.Quantity);
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }
}

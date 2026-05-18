using MassTransit;
using Microsoft.EntityFrameworkCore;
using ProductService.Domain.Events;
using ProductService.Infrastructure.Persistence;
using ProductService.Infrastructure.Search;

namespace ProductService.Consumers;

public sealed class ProductIndexConsumer :
    IConsumer<ProductCreatedDomainEvent>,
    IConsumer<StockDecreasedDomainEvent>,
    IConsumer<StockIncreasedDomainEvent>
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductIndexConsumer> _logger;

    public ProductIndexConsumer(IServiceScopeFactory scopeFactory, ILogger<ProductIndexConsumer> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ProductCreatedDomainEvent> context)
    {
        await IndexProductAsync(context.Message.ProductId, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<StockDecreasedDomainEvent> context)
    {
        await IndexProductAsync(context.Message.ProductId, context.CancellationToken);
    }

    public async Task Consume(ConsumeContext<StockIncreasedDomainEvent> context)
    {
        await IndexProductAsync(context.Message.ProductId, context.CancellationToken);
    }

    private async Task IndexProductAsync(Guid productId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db      = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
        var search  = scope.ServiceProvider.GetRequiredService<IProductSearchService>();

        var product = await db.Products.FindAsync([productId], ct);
        if (product is null)
        {
            _logger.LogWarning("Product {ProductId} not found for index update, removing from index.", productId);
            await search.DeleteProductIndexAsync(productId, ct);
            return;
        }

        await search.IndexProductAsync(product, ct);
        _logger.LogDebug("Indexed product {ProductId} ({Name}) in Elasticsearch", productId, product.Name);
    }
}

using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Search;

public interface IProductSearchService
{
    Task IndexProductAsync(Product product, CancellationToken ct = default);
    Task<SearchResult> SearchProductsAsync(string query, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task DeleteProductIndexAsync(Guid productId, CancellationToken ct = default);
}

public sealed record ProductSearchHit(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int Stock,
    double Score
);

public sealed record SearchResult(IReadOnlyList<ProductSearchHit> Items, long TotalCount, int Page, int PageSize);

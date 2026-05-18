using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.QueryDsl;
using Microsoft.Extensions.Configuration;
using ProductService.Domain.Entities;

namespace ProductService.Infrastructure.Search;

public sealed class ProductSearchService : IProductSearchService
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ProductSearchService(ElasticsearchClient client, IConfiguration config)
    {
        _client = client;
        _indexName = config["Elasticsearch:IndexName"] ?? "products";
    }

    public async Task IndexProductAsync(Product product, CancellationToken ct = default)
    {
        var doc = new ProductSearchDocument
        {
            Id = product.Id,
            Name = product.Name,
            Description = product.Description,
            Sku = product.Sku,
            Price = (double)product.Price,
            Stock = product.Stock,
            LowStockThreshold = product.LowStockThreshold,
            CreatedAt = product.CreatedAt
        };

        await _client.IndexAsync(doc, idx => idx.Index(_indexName), ct);
    }

    public async Task<SearchResult> SearchProductsAsync(string query, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var from = (page - 1) * pageSize;

        var searchResponse = await _client.SearchAsync<ProductSearchDocument>(s => s
            .Indices(_indexName)
            .From(from)
            .Size(pageSize)
            .Query(q => q
                .MultiMatch(mm => mm
                    .Query(query)
                    .Fields(new[] { "name^3", "description", "sku^2" })
                    .Fuzziness(new Fuzziness("AUTO"))
                    .Operator(Operator.Or)
                )
            )
            .Sort(ss => ss
                .Field(f => f.CreatedAt, sort => sort.Order(SortOrder.Desc))
            ), ct);

        var hits = searchResponse.Hits.Select(h => new ProductSearchHit(
            h.Source!.Id,
            h.Source.Name,
            h.Source.Description,
            h.Source.Sku,
            (decimal)h.Source.Price,
            h.Source.Stock,
            h.Score ?? 0.0
        )).ToList();

        return new SearchResult(hits, searchResponse.Total, page, pageSize);
    }

    public async Task DeleteProductIndexAsync(Guid productId, CancellationToken ct = default)
    {
        await _client.DeleteAsync<ProductSearchDocument>(productId, idx => idx.Index(_indexName), ct);
    }
}

internal sealed record ProductSearchDocument
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Sku { get; init; } = string.Empty;
    public double Price { get; init; }
    public int Stock { get; init; }
    public int LowStockThreshold { get; init; }
    public DateTime CreatedAt { get; init; }
}

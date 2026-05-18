using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.IndexManagement;
using Microsoft.Extensions.Configuration;

namespace ProductService.Infrastructure.Search;

public sealed class ProductIndexMapping
{
    private readonly ElasticsearchClient _client;
    private readonly string _indexName;

    public ProductIndexMapping(ElasticsearchClient client, IConfiguration config)
    {
        _client = client;
        _indexName = config["Elasticsearch:IndexName"] ?? "products";
    }

    public async Task CreateIfNotExistsAsync(CancellationToken ct = default)
    {
        var exists = await _client.Indices.ExistsAsync(_indexName, ct);
        if (exists.Exists)
            return;

        var response = await _client.Indices.CreateAsync(_indexName, descriptor =>
        {
            descriptor.Settings(settings =>
            {
                settings.NumberOfShards(1);
                settings.NumberOfReplicas(0);
            });

            descriptor.Mappings(mappings =>
            {
                mappings.Properties<ProductSearchDocument>(props =>
                {
                    props.Text(t => t.Name, cfg => cfg.Analyzer("turkish"));
                    props.Text(t => t.Description, cfg => cfg.Analyzer("turkish"));
                    props.Keyword(t => t.Sku);
                    props.DoubleNumber(t => t.Price);
                    props.IntegerNumber(t => t.Stock);
                    props.IntegerNumber(t => t.LowStockThreshold);
                    props.Date(t => t.CreatedAt);
                });
            });
        }, ct);

        if (!response.IsValidResponse)
        {
            throw new InvalidOperationException(
                $"Failed to create Elasticsearch index '{_indexName}': {response.DebugInformation}");
        }
    }
}

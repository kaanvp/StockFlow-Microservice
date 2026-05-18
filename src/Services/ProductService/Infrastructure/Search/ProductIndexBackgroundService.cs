namespace ProductService.Infrastructure.Search;

public sealed class ProductIndexBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProductIndexBackgroundService> _logger;

    public ProductIndexBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ProductIndexBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var mapping = scope.ServiceProvider.GetRequiredService<ProductIndexMapping>();
            await mapping.CreateIfNotExistsAsync(stoppingToken);
            _logger.LogInformation("Elasticsearch index mapping verified/created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Elasticsearch index on startup. Will retry on next restart.");
        }
    }
}

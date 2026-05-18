using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;

namespace ProductService.Infrastructure.Search;

public sealed class ProductCacheService
{
    private readonly IDistributedCache _cache;
    private readonly DistributedCacheEntryOptions _defaultOptions;

    public ProductCacheService(IDistributedCache cache)
    {
        _cache = cache;
        _defaultOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : class
    {
        var data = await _cache.GetAsync(key, ct);
        return data is null ? null : JsonSerializer.Deserialize<T>(data);
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken ct = default) where T : class
    {
        var data = JsonSerializer.SerializeToUtf8Bytes(value);
        await _cache.SetAsync(key, data, _defaultOptions, ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await _cache.RemoveAsync(key, ct);
    }

    public static string ProductListKey(string? search, bool lowStockOnly, int page, int pageSize)
        => $"products:list:{search ?? ""}:{lowStockOnly}:{page}:{pageSize}";

    public static string ProductByIdKey(Guid id) => $"products:id:{id}";
}

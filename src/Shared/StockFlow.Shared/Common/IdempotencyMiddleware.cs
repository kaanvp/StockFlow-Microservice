using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;

namespace StockFlow.Shared.Common;

public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<IdempotencyMiddleware> _logger;
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

    private static readonly HashSet<string> _idempotentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post, HttpMethods.Patch, HttpMethods.Put, HttpMethods.Delete
    };

    public IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_idempotentMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var cache = context.RequestServices.GetService<IDistributedCache>();
        if (cache is null)
        {
            await _next(context);
            return;
        }

        var idempotencyKey = context.Request.Headers["X-Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await _next(context);
            return;
        }

        var cacheKey = $"idempotency:{idempotencyKey}";

        var existingResponse = await cache.GetAsync(cacheKey, context.RequestAborted);
        if (existingResponse is not null)
        {
            _logger.LogInformation("Idempotent request detected: {Key}. Returning cached response.", idempotencyKey);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $"{{\"error\":\"Duplicate request\",\"idempotencyKey\":\"{idempotencyKey}\"}}",
                Encoding.UTF8,
                context.RequestAborted);
            return;
        }

        await cache.SetAsync(cacheKey,
            Encoding.UTF8.GetBytes("processing"),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl },
            context.RequestAborted);

        var originalBody = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300)
            {
                responseBody.Seek(0, SeekOrigin.Begin);
                var responseBytes = responseBody.ToArray();
                await cache.SetAsync(cacheKey, responseBytes,
                    new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl },
                    context.RequestAborted);
            }

            responseBody.Seek(0, SeekOrigin.Begin);
            await responseBody.CopyToAsync(originalBody, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }
}

public static class IdempotencyMiddlewareExtensions
{
    public static IApplicationBuilder UseIdempotency(this IApplicationBuilder builder)
        => builder.UseMiddleware<IdempotencyMiddleware>();
}

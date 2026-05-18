using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace StockFlow.Shared.Common;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        const string headerName = "X-Correlation-Id";

        if (!context.Request.Headers.TryGetValue(headerName, out var correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
            context.Request.Headers.Append(headerName, correlationId);
        }

        // Response header'a ekle
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(headerName))
                context.Response.Headers[headerName] = correlationId;
            return Task.CompletedTask;
        });

        _logger.LogDebug("CorrelationId: {CorrelationId} for {Method} {Path}",
            correlationId, context.Request.Method, context.Request.Path);

        await _next(context);
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        => builder.UseMiddleware<CorrelationIdMiddleware>();
}

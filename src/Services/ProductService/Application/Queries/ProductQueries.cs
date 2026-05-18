using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ProductService.Application.Queries;

// ─── DTOs ────────────────────────────────────────────────────────

public sealed record ProductDto(
    Guid Id,
    string Name,
    string Description,
    string Sku,
    decimal Price,
    int Stock,
    int LowStockThreshold,
    bool IsLowStock,
    DateTime CreatedAt
);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

// ─── GetProducts ─────────────────────────────────────────────────

public sealed record GetProductsQuery(
    string? Search = null,
    bool LowStockOnly = false,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<ProductDto>>;

public sealed class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, PagedResult<ProductDto>>
{
    private readonly IConfiguration _config;

    public GetProductsQueryHandler(IConfiguration config) => _config = config;

    public async Task<PagedResult<ProductDto>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_config.GetConnectionString("SqlServer"));

        var whereClause = "WHERE 1=1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            whereClause += " AND (Name LIKE @Search OR Sku LIKE @Search)";
            parameters.Add("Search", $"%{request.Search}%");
        }

        if (request.LowStockOnly)
            whereClause += " AND Stock <= LowStockThreshold";

        parameters.Add("Offset", (request.Page - 1) * request.PageSize);
        parameters.Add("PageSize", request.PageSize);

        var sql = $"""
            SELECT Id, Name, Description, Sku, Price, Stock, LowStockThreshold,
                   CASE WHEN Stock <= LowStockThreshold THEN 1 ELSE 0 END AS IsLowStock,
                   CreatedAt
            FROM Products
            {whereClause}
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*) FROM Products {whereClause};
            """;

        using var multi = await conn.QueryMultipleAsync(sql, parameters);
        var items = (await multi.ReadAsync<ProductDto>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<ProductDto>(items, total, request.Page, request.PageSize);
    }
}

// ─── GetProductById ──────────────────────────────────────────────

public sealed record GetProductByIdQuery(Guid Id) : IRequest<ProductDto?>;

public sealed class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDto?>
{
    private readonly IConfiguration _config;

    public GetProductByIdQueryHandler(IConfiguration config) => _config = config;

    public async Task<ProductDto?> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_config.GetConnectionString("SqlServer"));

        const string sql = """
            SELECT Id, Name, Description, Sku, Price, Stock, LowStockThreshold,
                   CASE WHEN Stock <= LowStockThreshold THEN 1 ELSE 0 END AS IsLowStock,
                   CreatedAt
            FROM Products
            WHERE Id = @Id
            """;

        return await conn.QuerySingleOrDefaultAsync<ProductDto>(sql, new { request.Id });
    }
}

// ─── GetStockHistory ────────────────────────────────────────────

public sealed record StockEventDto(
    Guid Id,
    string EventType,
    int OldStock,
    int NewStock,
    int Quantity,
    string? ReferenceId,
    string? Description,
    DateTime OccurredAt
);

public sealed record GetStockHistoryQuery(Guid ProductId) : IRequest<IReadOnlyList<StockEventDto>>;

public sealed class GetStockHistoryQueryHandler : IRequestHandler<GetStockHistoryQuery, IReadOnlyList<StockEventDto>>
{
    private readonly IConfiguration _config;

    public GetStockHistoryQueryHandler(IConfiguration config) => _config = config;

    public async Task<IReadOnlyList<StockEventDto>> Handle(GetStockHistoryQuery request, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_config.GetConnectionString("SqlServer"));

        const string sql = """
            SELECT Id, EventType, OldStock, NewStock, Quantity, ReferenceId, Description, OccurredAt
            FROM StockEvents
            WHERE ProductId = @ProductId
            ORDER BY OccurredAt DESC
            """;

        var events = await conn.QueryAsync<StockEventDto>(sql, new { request.ProductId });
        return events.ToList();
    }
}

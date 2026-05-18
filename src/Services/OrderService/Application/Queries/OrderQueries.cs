using Dapper;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using OrderService.Domain.Entities;

namespace OrderService.Application.Queries;

// ─── DTOs ────────────────────────────────────────────────────────

public sealed record OrderDto(
    Guid Id,
    string CustomerEmail,
    Guid ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string Status,
    string? CancelReason,
    DateTime CreatedAt
);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

// ─── GetOrders ───────────────────────────────────────────────────

public sealed record GetOrdersQuery(
    string? Email = null,
    OrderStatus? Status = null,
    int Page = 1,
    int PageSize = 20
) : IRequest<PagedResult<OrderDto>>;

public sealed class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, PagedResult<OrderDto>>
{
    private readonly IConfiguration _config;

    public GetOrdersQueryHandler(IConfiguration config) => _config = config;

    public async Task<PagedResult<OrderDto>> Handle(GetOrdersQuery request, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_config.GetConnectionString("SqlServer"));

        var whereClause = "WHERE 1=1";
        var parameters = new DynamicParameters();

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            whereClause += " AND CustomerEmail = @Email";
            parameters.Add("Email", request.Email);
        }

        if (request.Status.HasValue)
        {
            whereClause += " AND Status = @Status";
            parameters.Add("Status", request.Status.Value.ToString());
        }

        parameters.Add("Offset", (request.Page - 1) * request.PageSize);
        parameters.Add("PageSize", request.PageSize);

        var sql = $"""
            SELECT Id, CustomerEmail, ProductId, ProductName, Quantity, UnitPrice,
                   TotalPrice, Status, CancelReason, CreatedAt
            FROM Orders
            {whereClause}
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*) FROM Orders {whereClause};
            """;

        using var multi = await conn.QueryMultipleAsync(sql, parameters);
        var items = (await multi.ReadAsync<OrderDto>()).ToList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<OrderDto>(items, total, request.Page, request.PageSize);
    }
}

// ─── GetOrderById ────────────────────────────────────────────────

public sealed record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto?>;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IConfiguration _config;

    public GetOrderByIdQueryHandler(IConfiguration config) => _config = config;

    public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        await using var conn = new SqlConnection(_config.GetConnectionString("SqlServer"));

        const string sql = """
            SELECT Id, CustomerEmail, ProductId, ProductName, Quantity, UnitPrice,
                   TotalPrice, Status, CancelReason, CreatedAt
            FROM Orders
            WHERE Id = @Id
            """;

        return await conn.QuerySingleOrDefaultAsync<OrderDto>(sql, new { request.Id });
    }
}

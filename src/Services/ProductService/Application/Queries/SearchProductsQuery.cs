using MediatR;
using ProductService.Infrastructure.Search;

namespace ProductService.Application.Queries;

public sealed record SearchProductsQuery(
    string SearchTerm,
    int Page = 1,
    int PageSize = 20
) : IRequest<SearchResult>;

public sealed class SearchProductsQueryHandler : IRequestHandler<SearchProductsQuery, SearchResult>
{
    private readonly IProductSearchService _searchService;

    public SearchProductsQueryHandler(IProductSearchService searchService) => _searchService = searchService;

    public async Task<SearchResult> Handle(SearchProductsQuery request, CancellationToken ct)
    {
        return await _searchService.SearchProductsAsync(
            request.SearchTerm, request.Page, request.PageSize, ct);
    }
}

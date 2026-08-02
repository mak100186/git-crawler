using GitCrawler.Api.Features.Repositories;

using Wolverine;

namespace GitCrawler.Api.Features.Categories.GetCategoryRepositories;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Diagnostics/Ping/PingEndpoint.cs. Category drill-down's default is Newest/Desc, same as
// Discovery Feed (F-010 D4).
public static class GetCategoryRepositoriesEndpoint
{
    public static IEndpointRouteBuilder MapGetCategoryRepositoriesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/categories/{category}/repositories", async (
                string category,
                IMessageBus bus,
                int? minStars,
                int? maxStars,
                string[]? topic,
                string[]? license,
                bool bookmarkedOnly = false,
                RepositorySortField sort = RepositorySortField.Newest,
                SortDirection direction = SortDirection.Desc,
                int page = 1,
                int pageSize = RepositoryCardQuery.DefaultPageSize) =>
            {
                var filter = new RepositoryFilterCriteria(null, minStars, maxStars, topic, license, bookmarkedOnly, sort, direction, page, pageSize);
                return await bus.InvokeAsync<PagedResult<RepositoryCardDto>>(new GetCategoryRepositoriesQuery(category, filter));
            })
            .WithName("GetCategoryRepositories");

        return app;
    }
}
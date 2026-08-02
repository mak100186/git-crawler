using Wolverine;

namespace GitCrawler.Api.Features.Repositories.GetDiscoveryFeed;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Diagnostics/Ping/PingEndpoint.cs. Discovery Feed's default is Newest/Desc (F-010 D4).
public static class GetDiscoveryFeedEndpoint
{
    public static IEndpointRouteBuilder MapGetDiscoveryFeedEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/discovery-feed", async (
                IMessageBus bus,
                string[]? language,
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
                var filter = new RepositoryFilterCriteria(language, minStars, maxStars, topic, license, bookmarkedOnly, sort, direction, page, pageSize);
                return await bus.InvokeAsync<PagedResult<RepositoryCardDto>>(new GetDiscoveryFeedQuery(filter));
            })
            .WithName("GetDiscoveryFeed");

        return app;
    }
}
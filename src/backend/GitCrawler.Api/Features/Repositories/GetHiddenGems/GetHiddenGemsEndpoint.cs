using Wolverine;

namespace GitCrawler.Api.Features.Repositories.GetHiddenGems;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Diagnostics/Ping/PingEndpoint.cs. Hidden Gems' default is Score/Desc (F-010 D4).
public static class GetHiddenGemsEndpoint
{
    public static IEndpointRouteBuilder MapGetHiddenGemsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/hidden-gems", async (
                IMessageBus bus,
                string[]? language,
                int? minStars,
                int? maxStars,
                string[]? topic,
                string[]? license,
                bool bookmarkedOnly = false,
                RepositorySortField sort = RepositorySortField.Score,
                SortDirection direction = SortDirection.Desc,
                int page = 1,
                int pageSize = RepositoryCardQuery.DefaultPageSize) =>
            {
                var filter = new RepositoryFilterCriteria(language, minStars, maxStars, topic, license, bookmarkedOnly, sort, direction, page, pageSize);
                return await bus.InvokeAsync<PagedResult<HiddenGemCardDto>>(new GetHiddenGemsQuery(filter));
            })
            .WithName("GetHiddenGems");

        return app;
    }
}
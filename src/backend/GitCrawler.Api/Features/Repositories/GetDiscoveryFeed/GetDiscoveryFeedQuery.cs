using GitCrawler.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Repositories.GetDiscoveryFeed;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// filtering/sorting/pagination itself is delegated to RepositoryCardQuery (Features/Repositories),
// a plain internal helper shared with GetHiddenGems/GetCategoryRepositories per F-010 D4, not a
// Wolverine message of its own.
public record GetDiscoveryFeedQuery(RepositoryFilterCriteria Filter);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GetDiscoveryFeedQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<PagedResult<RepositoryCardDto>> HandleAsync(GetDiscoveryFeedQuery query, CancellationToken cancellationToken)
    {
        var filter = query.Filter;

        var candidates = await RepositoryCardQuery.ApplyFilters(RepositoryCardQuery.IncludeForCards(dbContext.Repositories), filter)
            .ToListAsync(cancellationToken);

        var ranked = RepositoryCardQuery.Rank(candidates, filter.Sort, filter.Direction);
        var page = RepositoryCardQuery.Paginate(ranked, filter.Page, filter.PageSize, out var totalCount);

        return new PagedResult<RepositoryCardDto>(
            [.. page.Select(RepositoryCardQuery.ToCardDto)],
            RepositoryCardQuery.ClampPage(filter.Page),
            RepositoryCardQuery.ClampPageSize(filter.PageSize),
            totalCount);
    }
}
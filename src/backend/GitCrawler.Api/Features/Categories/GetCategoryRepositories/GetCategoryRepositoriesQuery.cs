using GitCrawler.Api.Data;
using GitCrawler.Api.Features.Repositories;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Categories.GetCategoryRepositories;

// Wolverine message + result for this slice - the Category drill-down (dashboard-handoff.md §5
// "05 Category drill-down": "the exact Discovery layout... with the topic chip pre-applied"; F-010
// D2 reads that "topic" wording as illustrative placeholder copy from the mockup tool, not a
// literal instruction to filter by GitHub topic - Category is Repository.PrimaryLanguage, same as
// GetCategories). Reuses RepositoryCardQuery's shared D4 filter/sort/paginate helper
// (Features/Repositories), same as GetDiscoveryFeed/GetHiddenGems - Category is forced onto the
// shared filter's Language facet rather than exposed as a separate parameter.
public record GetCategoryRepositoriesQuery(string Category, RepositoryFilterCriteria Filter);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GetCategoryRepositoriesQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<PagedResult<RepositoryCardDto>> HandleAsync(GetCategoryRepositoriesQuery query, CancellationToken cancellationToken)
    {
        // The category is a forced, non-removable Language facet value - any caller-supplied
        // Language filter would be redundant/contradictory for a single-category drill-down, so
        // it's overridden here rather than combined with it.
        var filter = query.Filter with { Language = [query.Category] };

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
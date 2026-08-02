using GitCrawler.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Categories.GetCategories;

// F-010 D2: Category = TrendAggregate.Category (i.e. Repository.PrimaryLanguage), not GitHub
// topics - F-009 already shipped Category as language-derived and that isn't reopened here.
public record CategoryDto(string Category, int RepositoryCount, double AverageScore, DateOnly PeriodStart, DateOnly PeriodEnd);

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder.
public record GetCategoriesQuery;

public record GetCategoriesResult(IReadOnlyList<CategoryDto> Categories);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GetCategoriesQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<GetCategoriesResult> HandleAsync(GetCategoriesQuery query, CancellationToken cancellationToken)
    {
        var trendAggregates = await dbContext.TrendAggregates.ToListAsync(cancellationToken);

        // AggregateTrendsCommandHandler upserts by (Category, PeriodStart, PeriodEnd) (F-009), so
        // multiple TrendAggregate rows can exist per category over time (one per rollup period).
        // The Categories view needs each category's *current* standing, so the latest-by-
        // CreatedAtUtc row wins per category - the same "latest wins" pattern this codebase already
        // applies to Score/Summary reads elsewhere (see those handlers' own comments).
        var categories = trendAggregates
            .GroupBy(t => t.Category)
            .Select(g => g.OrderByDescending(t => t.CreatedAtUtc).First())
            .OrderBy(t => t.Category, StringComparer.Ordinal)
            .Select(t => new CategoryDto(t.Category, t.RepositoryCount, t.AverageScore, t.PeriodStart, t.PeriodEnd))
            .ToList();

        return new GetCategoriesResult(categories);
    }
}
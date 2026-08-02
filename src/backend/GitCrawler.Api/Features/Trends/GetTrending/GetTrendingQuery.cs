using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Trends.GetTrending;

// A repo row inside an expanded trend's "contributing repos" list (dashboard-handoff.md §5 "03
// Trending") - a narrower shape than the shared RepositoryCardDto (F-010 D5 scopes that to the
// repo-card queries: Discovery Feed, Hidden Gems, Category drill-down), matching only what the
// design's contributing-repo row needs.
public record TrendingRepositoryDto(int Id, string Owner, string Name, string? PrimaryLanguage, int StarCount, string? SummaryContent, bool IsBookmarked);

public record TrendDto(
    int Id,
    string Category,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int RepositoryCount,
    double AverageScore,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<TrendingRepositoryDto> ContributingRepositories);

// Wolverine message + result for this slice. Distinct from Features/Trends/AggregateTrends, which
// is the write-side background job (F-009) that computes and persists TrendAggregate rows - this
// is the read-side query the Web API exposes over that already-computed data. No shared
// service/repository layer per ADR-015 - everything this operation needs lives in this folder.
public record GetTrendingQuery;

public record GetTrendingResult(IReadOnlyList<TrendDto> Trends);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GetTrendingQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<GetTrendingResult> HandleAsync(GetTrendingQuery query, CancellationToken cancellationToken)
    {
        // No filter/sort bar for Trending (design's §5 "03 Trending": render API order); highest
        // AverageScore first is this feature's own judgment call for what "API order" means absent
        // any spec'd ordering. Not paginated either - a TrendAggregate row per category is a small,
        // naturally bounded set, unlike the repo lists the D4 paginator exists for.
        var trends = await dbContext.TrendAggregates
            .OrderByDescending(t => t.AverageScore)
            .ToListAsync(cancellationToken);

        // F-010 D3: TrendAggregate has no FK to individual repos by design, so "contributing repos"
        // for a trend/category is computed at query time here, mirroring
        // AggregateTrendsCommandHandler's own membership criteria exactly (has both a Score and a
        // Summary, matching PrimaryLanguage, latest-by-ComputedAtUtc Score) - see that handler's
        // comment for the full rationale, which applies unchanged here.
        var candidateRepositories = await dbContext.Repositories
            .Include(r => r.Scores)
            .Include(r => r.Summaries)
            .Include(r => r.Bookmarks)
            .Where(r => r.PrimaryLanguage != null && r.Scores.Any() && r.Summaries.Any())
            .ToListAsync(cancellationToken);

        var repositoriesByCategory = candidateRepositories
            .GroupBy(r => r.PrimaryLanguage!)
            .ToDictionary(g => g.Key, g => g.ToList());

        var trendDtos = trends
            .Select(t => new TrendDto(
                t.Id,
                t.Category,
                t.PeriodStart,
                t.PeriodEnd,
                t.RepositoryCount,
                t.AverageScore,
                t.CreatedAtUtc,
                BuildContributingRepositories(repositoriesByCategory, t.Category)))
            .ToList();

        return new GetTrendingResult(trendDtos);
    }

    private static IReadOnlyList<TrendingRepositoryDto> BuildContributingRepositories(
        IReadOnlyDictionary<string, List<Repository>> repositoriesByCategory, string category)
    {
        if (!repositoriesByCategory.TryGetValue(category, out var repositories))
        {
            return [];
        }

        return [.. repositories.Select(r => new TrendingRepositoryDto(
            r.Id,
            r.Owner,
            r.Name,
            r.PrimaryLanguage,
            r.StarCount,
            r.Summaries.OrderByDescending(s => s.GeneratedAtUtc).First().Content,
            r.Bookmarks.Count > 0))];
    }
}
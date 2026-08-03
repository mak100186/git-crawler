using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Scoring.ComputeScores;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Repositories.GetHiddenGems;

// FR-005's "independently-weighted, identifiable signal inputs" requirement (F-010 D5) - each raw
// signal alongside its own fixed ScoringWeights constant, not just the collapsed TotalScore.
public record ScoreBreakdownDto(
    bool HasLicense,
    string? LicenseType,
    double LicenseWeight,
    double CommitsPerWeek,
    double CommitsPerWeekWeight,
    int ContributorCount,
    double ContributorCountWeight,
    int ForkCount,
    double ForkCountWeight,
    int StarCount,
    double StarCountWeight,
    double TotalScore);

// Extends the shared repo-card shape (D5) with the score breakdown Hidden Gems alone needs -
// record inheritance instead of duplicating RepositoryCardDto's field list.
//
// TrendGrowth (added when the standalone Trending tab was decommissioned in favor of surfacing this
// directly on the card): the repo's own category's trend growth, formatted identically to what the
// old Trending view rendered per trend card - "▲ +18% vs. last period" when at least two periods
// exist for the category, or "{avg} avg score" when only one period exists yet (no prior period to
// diff against). Null when the repo's category has no TrendAggregate row at all yet (e.g. the
// nightly Trend Aggregator hasn't run since this repo/category first appeared).
public sealed record HiddenGemCardDto(
    int Id,
    string Owner,
    string Name,
    string Url,
    string? PrimaryLanguage,
    int StarCount,
    int ForkCount,
    string? LicenseIdentifier,
    string? LicenseName,
    IReadOnlyList<string> Topics,
    DateTimeOffset FirstDiscoveredAtUtc,
    string? SummaryContent,
    bool IsBookmarked,
    ScoreBreakdownDto ScoreBreakdown,
    string? TrendGrowth)
    : RepositoryCardDto(Id, Owner, Name, Url, PrimaryLanguage, StarCount, ForkCount, LicenseIdentifier, LicenseName, Topics, FirstDiscoveredAtUtc, SummaryContent, IsBookmarked);

// Wolverine message + result for this slice. Reuses RepositoryCardQuery's shared D4 filter/sort/
// paginate helper (Features/Repositories) - GetDiscoveryFeed used to share it too before that
// standalone view was folded away, leaving this the only full user of the pipeline (see the
// changelog entry for that removal).
public record GetHiddenGemsQuery(RepositoryFilterCriteria Filter);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GetHiddenGemsQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<PagedResult<HiddenGemCardDto>> HandleAsync(GetHiddenGemsQuery query, CancellationToken cancellationToken)
    {
        var filter = query.Filter;

        // Hidden Gems can only show a score breakdown for repos that have actually been scored -
        // this Scores.Any() filter is specific to this slice, not part of the shared D4 contract.
        var scoredRepositories = dbContext.Repositories.Where(r => r.Scores.Any());

        var candidates = await RepositoryCardQuery.ApplyFilters(RepositoryCardQuery.IncludeForCards(scoredRepositories), filter)
            .ToListAsync(cancellationToken);

        var ranked = RepositoryCardQuery.Rank(candidates, filter.Sort, filter.Direction);
        var page = RepositoryCardQuery.Paginate(ranked, filter.Page, filter.PageSize, out var totalCount);

        // Ported from the frontend's old trending.ts computeGrowthLabel (removed along with the
        // standalone Trending tab) - same formula, now computed once per response instead of once
        // per card client-side. One dictionary lookup per card below rather than per-repo querying.
        var trendAggregates = await dbContext.TrendAggregates.ToListAsync(cancellationToken);
        var growthLabelsByCategory = BuildGrowthLabelsByCategory(trendAggregates);

        return new PagedResult<HiddenGemCardDto>(
            [.. page.Select(r => ToHiddenGemDto(r, growthLabelsByCategory))],
            RepositoryCardQuery.ClampPage(filter.Page),
            RepositoryCardQuery.ClampPageSize(filter.PageSize),
            totalCount);
    }

    private static HiddenGemCardDto ToHiddenGemDto(RankedRepository ranked, IReadOnlyDictionary<string, string> growthLabelsByCategory)
    {
        // Never null: the Scores.Any() filter above guarantees at least one Score row, so the
        // latest-by-ComputedAtUtc resolution in RepositoryCardQuery.Rank always finds one here.
        var score = ranked.LatestScore!;

        var trendGrowth = ranked.Repository.PrimaryLanguage is string category
            && growthLabelsByCategory.TryGetValue(category, out var label)
                ? label
                : null;

        return new HiddenGemCardDto(
            ranked.Repository.Id,
            ranked.Repository.Owner,
            ranked.Repository.Name,
            ranked.Repository.Url,
            ranked.Repository.PrimaryLanguage,
            ranked.Repository.StarCount,
            ranked.Repository.ForkCount,
            ranked.Repository.LicenseIdentifier,
            ranked.Repository.LicenseName,
            ranked.Repository.Topics,
            ranked.Repository.FirstDiscoveredAtUtc,
            ranked.LatestSummary?.Content,
            ranked.Repository.Bookmarks.Count > 0,
            new ScoreBreakdownDto(
                score.HasLicense,
                score.LicenseType,
                ScoringWeights.LicenseWeight,
                score.CommitsPerWeek,
                ScoringWeights.CommitsPerWeekWeight,
                score.ContributorCount,
                ScoringWeights.ContributorCountWeight,
                score.ForkCount,
                ScoringWeights.ForkCountWeight,
                score.StarCount,
                ScoringWeights.StarCountWeight,
                score.TotalScore),
            trendGrowth);
    }

    // One growth label per category: "current" is the latest-by-PeriodStart TrendAggregate row for
    // that category, "previous" is the second-latest (if any) - same current/previous resolution the
    // old trending.ts computeGrowthLabel used across the full /api/trending response, just scoped per
    // category instead of per individual TrendDto row (this endpoint only ever needs one label per
    // category, not one per historical period).
    private static Dictionary<string, string> BuildGrowthLabelsByCategory(IReadOnlyList<TrendAggregate> trendAggregates)
    {
        var labels = new Dictionary<string, string>();

        foreach (var group in trendAggregates.GroupBy(t => t.Category))
        {
            var periods = group.OrderBy(t => t.PeriodStart).ToList();
            var current = periods[^1];
            var previous = periods.Count > 1 ? periods[^2] : null;

            labels[group.Key] = previous is null || previous.AverageScore == 0
                ? $"{Math.Round(current.AverageScore)} avg score"
                : FormatGrowthChange(current.AverageScore, previous.AverageScore);
        }

        return labels;
    }

    private static string FormatGrowthChange(double currentAverageScore, double previousAverageScore)
    {
        var change = (currentAverageScore - previousAverageScore) / previousAverageScore * 100;
        var arrow = change >= 0 ? "▲" : "▼";
        var sign = change >= 0 ? "+" : string.Empty;
        return $"{arrow} {sign}{change:F0}% vs. last period";
    }
}
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
// TrendGrowth: this repository's OWN score trend, not its language/category's (operator: "Trend is
// currently calculated per language. I want it to be calculated per repository" - what had shipped
// here originally, when the standalone Trending tab was decommissioned, reused that view's
// per-category TrendAggregate rollup verbatim, which meant every C# repo showed the same growth
// figure regardless of its own individual standing). Computed directly from Score's own
// append-per-recrawl history (ComputeScoresCommandHandler adds a new row on every re-score rather
// than upserting) - the latest two Score rows for this repo, diffed the same way the old
// category-level computation diffed two TrendAggregate periods: "▲ +18% vs. last period" once a
// second (re-crawl-produced) Score exists, or "{score} current score" when only the first Score
// exists yet (no prior score to diff against). Never null once a repo has any Score - unlike the
// category-level version, there's no "TrendAggregate row hasn't been computed yet" gap, since this
// reads directly off the same Score row already guaranteed to exist by GetHiddenGems' own
// Scores.Any() filter.
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
    string? DetailedSummaryContent,
    bool IsBookmarked,
    ScoreBreakdownDto ScoreBreakdown,
    string? TrendGrowth)
    : RepositoryCardDto(Id, Owner, Name, Url, PrimaryLanguage, StarCount, ForkCount, LicenseIdentifier, LicenseName, Topics, FirstDiscoveredAtUtc, SummaryContent, DetailedSummaryContent, IsBookmarked);

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

        return new PagedResult<HiddenGemCardDto>(
            [.. page.Select(ToHiddenGemDto)],
            RepositoryCardQuery.ClampPage(filter.Page),
            RepositoryCardQuery.ClampPageSize(filter.PageSize),
            totalCount);
    }

    private static HiddenGemCardDto ToHiddenGemDto(RankedRepository ranked)
    {
        // Never null: the Scores.Any() filter above guarantees at least one Score row, so the
        // latest-by-ComputedAtUtc resolution in RepositoryCardQuery.Rank always finds one here.
        var score = ranked.LatestScore!;

        var trendGrowth = ComputeTrendGrowth(ranked);

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
            ranked.LatestSummary?.ShortContent,
            ranked.LatestSummary?.DetailedContent,
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

    // "Current" is ranked.LatestScore (already resolved by RepositoryCardQuery.Rank); "previous" is
    // this same repo's next-most-recent Score row, if a re-crawl has produced one - the same
    // OrderByDescending(ComputedAtUtc) convention used everywhere else in this codebase for
    // latest-Score resolution (see RepositoryCardQuery.Rank/ComputeScoresCommandHandler), just
    // Skip(1) to land on the one before latest instead of Skip(0).
    private static string ComputeTrendGrowth(RankedRepository ranked)
    {
        var previous = ranked.Repository.Scores
            .OrderByDescending(s => s.ComputedAtUtc)
            .Skip(1)
            .FirstOrDefault();

        return previous is null || previous.TotalScore == 0
            ? $"{Math.Round(ranked.LatestScore!.TotalScore)} current score"
            : FormatGrowthChange(ranked.LatestScore!.TotalScore, previous.TotalScore);
    }

    private static string FormatGrowthChange(double currentScore, double previousScore)
    {
        var change = (currentScore - previousScore) / previousScore * 100;
        var arrow = change >= 0 ? "▲" : "▼";
        var sign = change >= 0 ? "+" : string.Empty;
        return $"{arrow} {sign}{change:F0}% vs. last period";
    }
}
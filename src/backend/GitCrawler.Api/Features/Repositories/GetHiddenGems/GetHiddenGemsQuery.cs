using GitCrawler.Api.Data;
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
    ScoreBreakdownDto ScoreBreakdown)
    : RepositoryCardDto(Id, Owner, Name, Url, PrimaryLanguage, StarCount, ForkCount, LicenseIdentifier, LicenseName, Topics, FirstDiscoveredAtUtc, SummaryContent, IsBookmarked);

// Wolverine message + result for this slice. Reuses RepositoryCardQuery's shared D4 filter/sort/
// paginate helper (Features/Repositories), same as GetDiscoveryFeed/GetCategoryRepositories.
public record GetHiddenGemsQuery(RepositoryFilterCriteria Filter);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GetHiddenGemsQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<PagedResult<HiddenGemCardDto>> HandleAsync(GetHiddenGemsQuery query, CancellationToken cancellationToken)
    {
        var filter = query.Filter;

        // Hidden Gems can only show a score breakdown for repos that have actually been scored -
        // this Scores.Any() filter is specific to this slice, not part of the shared D4 contract
        // (Discovery Feed/Category drill-down show unscored repos too).
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
                score.TotalScore));
    }
}
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
//
// F-017/NFR-004: filtering, sorting, and pagination execute entirely server-side on the production
// Npgsql/PostgreSQL provider. The pre-F-017 shape (IncludeForCards → ToListAsync → Rank →
// Paginate) materialized the ENTIRE scored match set with all navigation collections into process
// memory before sorting and paginating client-side - at 100k repos × ~10 Score rows each, every
// page request loaded ~1M rows. This rewrite pushes ORDER BY + LIMIT/OFFSET into SQL via
// RepositoryCardQuery.ApplySort's correlated-subquery sort key (same "latest by ComputedAtUtc,
// never highest-ever" convention from F-007). Per-request work is bounded by page size (≤
// MaxPageSize), not total match count.
//
// Provider-aware: the xUnit suite's SQLite provider rejects DateTimeOffset in ORDER BY (the
// Newest sort's FirstDiscoveredAtUtc key) and cannot translate any DateTimeOffset member. When
// SQLite is detected at runtime, the handler falls back to the client-side Rank/Paginate
// pipeline (IncludeForCards → materialize filtered set → Rank → Paginate) — same response
// contract and all semantics preserved, just client-side sort on the filtered candidates. The
// Score history detail query also sorts client-side after fetch for the same portability reason.
// Detail data (latest Score for the breakdown, second-latest for TrendGrowth, Summary, Bookmark
// flag) is fetched in narrow queries scoped to the page's repository IDs, not the full match set.
public class GetHiddenGemsQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<PagedResult<HiddenGemCardDto>> HandleAsync(GetHiddenGemsQuery query, CancellationToken cancellationToken)
    {
        var filter = query.Filter;
        var page = RepositoryCardQuery.ClampPage(filter.Page);
        var pageSize = RepositoryCardQuery.ClampPageSize(filter.PageSize);

        // Hidden Gems can only show a score breakdown for repos that have actually been scored -
        // this Scores.Any() filter is specific to this slice, not part of the shared D4 contract.
        // Applied BEFORE filters so TotalCount reflects the same eligible set.
        var filtered = RepositoryCardQuery.ApplyFilters(
            dbContext.Repositories.Where(r => r.Scores.Any()), filter);

        // TotalCount: full match count across all pages (not just the current page), so the
        // dashboard's paginator shows the correct total. CountAsync translates to SELECT COUNT(*).
        var totalCount = await filtered.CountAsync(cancellationToken);

        // Provider-aware sort/pagination: the production Npgsql/PostgreSQL provider translates
        // ApplySort (including its DateTimeOffset FirstDiscoveredAtUtc key for the "Newest" sort)
        // to SQL ORDER BY + LIMIT/OFFSET without issue. The EF Core SQLite provider (used by the
        // xUnit test suite) rejects DateTimeOffset in ORDER BY with NotSupportedException and
        // cannot translate any DateTimeOffset member (.DateTime, .Ticks) either, so the
        // server-side path is unreachable on SQLite. Fall back to the client-side Rank/Paginate
        // pipeline (IncludeForCards → materialize → Rank → Paginate), which already works
        // correctly on both providers via LINQ-to-Objects.
        var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";

        List<int> pageRepositoryIds;
        if (isSqlite)
        {
            var candidates = await RepositoryCardQuery.IncludeForCards(filtered)
                .ToListAsync(cancellationToken);
            var ranked = RepositoryCardQuery.Rank(candidates, filter.Sort, filter.Direction);
            var paginated = RepositoryCardQuery.Paginate(ranked, page, pageSize, out _);
            pageRepositoryIds = paginated.Select(r => r.Repository.Id).ToList();
        }
        else
        {
            // Sort + paginate server-side. ApplySort translates to ORDER BY with a correlated
            // subquery for Score/Commits sort keys; ThenBy(r.Id) is the deterministic tie-break
            // (F-010). Skip/Take translate to LIMIT/OFFSET, bounding per-request work to pageSize.
            pageRepositoryIds = await RepositoryCardQuery.ApplySort(filtered, filter.Sort, filter.Direction)
                .ThenBy(r => r.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);
        }

        // Beyond-last-page: the match set has results (totalCount > 0) but Skip past the end
        // yields no IDs. Return an empty slice with the accurate totalCount - not an error (F-010).
        if (pageRepositoryIds.Count == 0)
        {
            return new PagedResult<HiddenGemCardDto>([], page, pageSize, totalCount);
        }

        // Detail fetch: three narrow queries scoped to the page's repository IDs (≤ MaxPageSize),
        // not the full match set. This replaces the pre-F-017 IncludeForCards pattern that loaded
        // Scores/Summaries/Bookmarks for every matched repository before pagination.

        // Repository entities for the page's IDs - needed for Owner/Name/Url/etc. on the DTO.
        var pageRepositories = await dbContext.Repositories
            .Where(r => pageRepositoryIds.Contains(r.Id))
            .ToListAsync(cancellationToken);

        // Score history for the page's repos - needed for the score breakdown (latest row) and
        // TrendGrowth (latest two rows). ~10 rows per repo × ≤100 repos = ~1000 rows max.
        // Sort is done client-side after fetch because the SQLite provider (xUnit suite) rejects
        // DateTimeOffset in ORDER BY; the production Npgsql provider handles it server-side, but
        // the result is identical either way (LINQ-to-Objects on a bounded page-scoped set).
        var pageScores = (await dbContext.Scores
            .Where(s => pageRepositoryIds.Contains(s.RepositoryId))
            .ToListAsync(cancellationToken))
            .OrderByDescending(s => s.ComputedAtUtc)
            .ToList();

        // Summary (if any) for each page repo - at most one per repo (unique index, F-016).
        var pageSummaries = await dbContext.Summaries
            .Where(s => pageRepositoryIds.Contains(s.RepositoryId))
            .ToListAsync(cancellationToken);

        // Bookmark existence for each page repo - using Any() instead of loading the entity, since
        // the DTO only needs the boolean, not the bookmark's own fields.
        var bookmarkedRepositoryIds = await dbContext.Bookmarks
            .Where(b => pageRepositoryIds.Contains(b.RepositoryId))
            .Select(b => b.RepositoryId)
            .ToHashSetAsync(cancellationToken);

        // Assemble DTOs from the page-scoped detail data, preserving the server-side sort order
        // (pageRepositoryIds is already in the correct order from the SQL ORDER BY).
        var scoreLookup = pageScores.GroupBy(s => s.RepositoryId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var summaryLookup = pageSummaries.ToDictionary(s => s.RepositoryId);

        var items = pageRepositoryIds.Select(id =>
        {
            var repository = pageRepositories.First(r => r.Id == id);
            var scores = scoreLookup.GetValueOrDefault(id) ?? [];
            var summary = summaryLookup.GetValueOrDefault(id);

            // Never null: the Scores.Any() filter above guarantees at least one Score row, and
            // scores are ordered by ComputedAtUtc DESC from the query above.
            var latestScore = scores[0];
            var previousScore = scores.Count > 1 ? scores[1] : null;

            return ToHiddenGemDto(repository, latestScore, previousScore, summary, bookmarkedRepositoryIds.Contains(id));
        }).ToList();

        return new PagedResult<HiddenGemCardDto>(items, page, pageSize, totalCount);
    }

    private static HiddenGemCardDto ToHiddenGemDto(
        Repository repository, Score latestScore, Score? previousScore, Summary? summary, bool isBookmarked)
    {
        var trendGrowth = ComputeTrendGrowth(latestScore, previousScore);

        return new HiddenGemCardDto(
            repository.Id,
            repository.Owner,
            repository.Name,
            repository.Url,
            repository.PrimaryLanguage,
            repository.StarCount,
            repository.ForkCount,
            repository.LicenseIdentifier,
            repository.LicenseName,
            repository.Topics,
            repository.FirstDiscoveredAtUtc,
            summary?.ShortContent,
            summary?.DetailedContent,
            isBookmarked,
            new ScoreBreakdownDto(
                latestScore.HasLicense,
                latestScore.LicenseType,
                ScoringWeights.LicenseWeight,
                latestScore.CommitsPerWeek,
                ScoringWeights.CommitsPerWeekWeight,
                latestScore.ContributorCount,
                ScoringWeights.ContributorCountWeight,
                latestScore.ForkCount,
                ScoringWeights.ForkCountWeight,
                latestScore.StarCount,
                ScoringWeights.StarCountWeight,
                latestScore.TotalScore),
            trendGrowth);
    }

    // "Current" is latestScore (already resolved as the first row of the page-scoped Score query,
    // ordered by ComputedAtUtc DESC); "previous" is this same repo's next-most-recent Score row
    // (the second row), if a re-crawl has produced one. Same OrderByDescending(ComputedAtUtc)
    // convention used everywhere else in this codebase for latest-Score resolution (see
    // RepositoryCardQuery.Rank/ComputeScoresCommandHandler).
    private static string ComputeTrendGrowth(Score latestScore, Score? previousScore) =>
        previousScore is null || previousScore.TotalScore == 0
            ? $"{Math.Round(latestScore.TotalScore)} current score"
            : FormatGrowthChange(latestScore.TotalScore, previousScore.TotalScore);

    private static string FormatGrowthChange(double currentScore, double previousScore)
    {
        var change = (currentScore - previousScore) / previousScore * 100;
        var arrow = change >= 0 ? "▲" : "▼";
        var sign = change >= 0 ? "+" : string.Empty;
        return $"{arrow} {sign}{change:F0}% vs. last period";
    }
}
using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Repositories;

// F-010 D4: the filter/sort/pagination contract, used today solely by GetHiddenGems. Originally
// shared by Discovery Feed, Category drill-down, and ListBookmarks too; all three were later
// decommissioned as distinct views/endpoints (see the changelog entries for those removals), leaving
// this helper with a single caller but the same shape - kept as its own class rather than folded
// into GetHiddenGemsQueryHandler directly, since a future second consumer is exactly the situation
// that already happened three times before. This is a plain internal helper, not a Wolverine message
// or a shared service layer - ADR-015's "one slice per operation" governs the message/handler
// boundary, not whether slices may share ordinary code.
public enum RepositorySortField
{
    Newest,
    Score,
    Stars,
    Commits,
}

public enum SortDirection
{
    Asc,
    Desc,
}

public sealed record RepositoryFilterCriteria(
    IReadOnlyList<string>? Language = null,
    int? MinStars = null,
    int? MaxStars = null,
    IReadOnlyList<string>? Topic = null,
    IReadOnlyList<string>? License = null,
    bool BookmarkedOnly = false,
    RepositorySortField Sort = RepositorySortField.Newest,
    SortDirection Direction = SortDirection.Desc,
    int Page = 1,
    int PageSize = RepositoryCardQuery.DefaultPageSize);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// D5: shared repo-card shape, extended by HiddenGemCardDto (record inheritance) for GetHiddenGems -
// the base record itself is never returned bare anymore now that ListBookmarks is gone (it used to be
// this shape's other consumer; see the changelog entry for that removal), but stays as the base type
// rather than folding its fields directly into HiddenGemCardDto, since a future second consumer of the
// bare shape is plausible (same reasoning as this file's own header comment). SummaryContent and
// DetailedSummaryContent (Summary.ShortContent/DetailedContent - operator direction: "two kinds of
// summaries: short that show on the repo card and then the detailed one") are both null exactly when
// no Summary row exists yet ("summary pending" client-side per the approved design), and both
// populated together otherwise - the migration that added DetailedContent deleted every pre-existing
// Summary row rather than leaving a partially-populated one to special-case (operator-confirmed).
// DetailedSummaryContent rides on this same DTO rather than a separate one fetched on demand, since
// the detail dialog only ever opens from a card that already has this data in hand client-side.
public record RepositoryCardDto(
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
    bool IsBookmarked);

// A repository paired with its latest Score/Summary (or null if none exist yet) - the unit the
// shared Rank/Paginate pipeline below operates on.
public sealed record RankedRepository(Repository Repository, Score? LatestScore, Summary? LatestSummary);

public static class RepositoryCardQuery
{
    // Design's approved paginator uses 24/page (dashboard-handoff.md §5, "01 Discovery Feed").
    public const int DefaultPageSize = 24;

    // F-010 D4's own instruction to "cap pageSize at a sane max, your choice, document it" - 100 is
    // generous for manual browsing while still bounding a single response's size.
    public const int MaxPageSize = 100;

    // F-017: eagerly loads navigation collections for client-side Rank/Paginate. Superseded by
    // server-side sort/pagination in GetHiddenGemsQueryHandler (the only remaining caller of the
    // Rank/Paginate path) which fetches only page-scoped details instead. Kept rather than removed
    // in case a future caller needs the full-materialization path again.
    public static IQueryable<Repository> IncludeForCards(IQueryable<Repository> query) =>
        query.Include(r => r.Scores).Include(r => r.Summaries).Include(r => r.Bookmarks);

    // F-017: server-side sort that translates to SQL ORDER BY on the production Npgsql/PostgreSQL
    // provider, replacing the client-side Rank method below (which materialized the entire match
    // set before sorting). The Score/Commits sort keys use a correlated scalar subquery —
    // `r.Scores.OrderByDescending(s => s.ComputedAtUtc).Select(s => s.TotalScore).FirstOrDefault()`
    // — to resolve each repository's latest score inside the ORDER BY clause itself (same "latest
    // by ComputedAtUtc, never highest-ever" convention from F-007). The Newest sort key uses
    // `FirstDiscoveredAtUtc` (DateTimeOffset) directly, which Npgsql handles without issue.
    // A repo with no Score rows yields NULL/default, which sorts to the end (0.0 for
    // Score/Commits), matching Rank's own fallback.
    //
    // Portability caveat: the xUnit suite's SQLite provider rejects DateTimeOffset in ORDER BY
    // (NotSupportedException) and cannot translate any DateTimeOffset member (.DateTime, .Ticks)
    // either, so this method is unreachable on SQLite for the Newest sort. GetHiddenGemsQueryHandler
    // detects SQLite at runtime and falls back to the client-side Rank/Paginate pipeline instead —
    // same response contract, same semantics. The caller must add a Repository.Id tie-break
    // (ThenBy r.Id) after this for deterministic pagination (F-010's explicit callout).
    public static IOrderedQueryable<Repository> ApplySort(
        IQueryable<Repository> query, RepositorySortField sort, SortDirection direction)
    {
        IOrderedQueryable<Repository> ordered = sort switch
        {
            RepositorySortField.Score => direction == SortDirection.Asc
                ? query.OrderBy(r => r.Scores.OrderByDescending(s => s.ComputedAtUtc)
                    .Select(s => s.TotalScore).FirstOrDefault())
                : query.OrderByDescending(r => r.Scores.OrderByDescending(s => s.ComputedAtUtc)
                    .Select(s => s.TotalScore).FirstOrDefault()),
            RepositorySortField.Stars => direction == SortDirection.Asc
                ? query.OrderBy(r => r.StarCount)
                : query.OrderByDescending(r => r.StarCount),
            RepositorySortField.Commits => direction == SortDirection.Asc
                ? query.OrderBy(r => r.Scores.OrderByDescending(s => s.ComputedAtUtc)
                    .Select(s => s.CommitsPerWeek).FirstOrDefault())
                : query.OrderByDescending(r => r.Scores.OrderByDescending(s => s.ComputedAtUtc)
                    .Select(s => s.CommitsPerWeek).FirstOrDefault()),
            _ => direction == SortDirection.Asc
                ? query.OrderBy(r => r.FirstDiscoveredAtUtc)
                : query.OrderByDescending(r => r.FirstDiscoveredAtUtc),
        };

        return ordered;
    }

    // Every facet is optional and AND-composed with the others (D4); each facet's own list of
    // values is OR-composed internally. The Topic facet uses the r.Topics.Any(t => topics.Contains
    // (t)) shape specifically because that's the pattern Npgsql's NpgsqlArrayMethodTranslator
    // translates to the `&&` array-overlap operator (verified against the installed
    // Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 package) rather than evaluating client-side.
    public static IQueryable<Repository> ApplyFilters(IQueryable<Repository> query, RepositoryFilterCriteria filter)
    {
        if (filter.Language is { Count: > 0 } languages)
        {
            query = query.Where(r => r.PrimaryLanguage != null && languages.Contains(r.PrimaryLanguage));
        }

        if (filter.MinStars is int minStars)
        {
            query = query.Where(r => r.StarCount >= minStars);
        }

        if (filter.MaxStars is int maxStars)
        {
            query = query.Where(r => r.StarCount <= maxStars);
        }

        if (filter.Topic is { Count: > 0 } topics)
        {
            query = query.Where(r => r.Topics.Any(t => topics.Contains(t)));
        }

        if (filter.License is { Count: > 0 } licenses)
        {
            query = query.Where(r => r.LicenseIdentifier != null && licenses.Contains(r.LicenseIdentifier));
        }

        if (filter.BookmarkedOnly)
        {
            query = query.Where(r => r.Bookmarks.Any());
        }

        return query;
    }

    // F-017: superseded by ApplySort (server-side ORDER BY) for GetHiddenGems' main path, which
    // avoids materializing the entire match set before sorting. Kept rather than removed: it's the
    // only fully-portable (SQLite + Npgsql) reference implementation of the "latest by
    // ComputedAtUtc, never highest-ever" ranking convention in client-side LINQ-to-Objects, and a
    // future caller may need it for small, already-materialized candidate sets where the
    // server-side path isn't applicable.
    //
    // Resolves each candidate's latest Score/Summary and sorts - mirroring the exact
    // OrderByDescending(ComputedAtUtc).First()-style convention already established by
    // GenerateSummariesCommandHandler/AggregateTrendsCommandHandler (not Max() - see
    // docs/handoff.md's "Important context" for why Max() is wrong here). Done client-side, after
    // the candidates are already materialized by ApplyFilters' caller, for the same portability
    // reason those handlers give: this must behave identically on the xUnit suite's SQLite provider
    // and the real Npgsql/Postgres provider. A repo with no Score yet sorts as if Score/Commits were
    // 0 rather than being dropped from the list - Hidden Gems layers its own Scores.Any() filter on
    // top before ranking (its own slice-specific requirement), so this fallback only ever matters for
    // other, unscored candidates should a future caller need them.
    //
    // Ties are broken by a stable secondary sort on Repository.Id ascending (F-010 Task Packet's
    // explicit callout that tie-breaking is this feature's judgment call) - deterministic paging
    // instead of depending on LINQ's unspecified order for equal keys.
    public static IReadOnlyList<RankedRepository> Rank(IReadOnlyList<Repository> candidates, RepositorySortField sort, SortDirection direction)
    {
        var projected = candidates.Select(r => new RankedRepository(
            r,
            r.Scores.OrderByDescending(s => s.ComputedAtUtc).FirstOrDefault(),
            r.Summaries.OrderByDescending(s => s.GeneratedAtUtc).FirstOrDefault()));

        IOrderedEnumerable<RankedRepository> ordered = sort switch
        {
            RepositorySortField.Score => OrderBy(projected, x => x.LatestScore?.TotalScore ?? 0.0, direction),
            RepositorySortField.Stars => OrderBy(projected, x => x.Repository.StarCount, direction),
            RepositorySortField.Commits => OrderBy(projected, x => x.LatestScore?.CommitsPerWeek ?? 0.0, direction),
            _ => OrderBy(projected, x => x.Repository.FirstDiscoveredAtUtc, direction),
        };

        return [.. ordered.ThenBy(x => x.Repository.Id)];
    }

    public static IReadOnlyList<RankedRepository> Paginate(IReadOnlyList<RankedRepository> ranked, int page, int pageSize, out int totalCount)
    {
        totalCount = ranked.Count;

        // A page beyond the last page returns an empty slice, not an error (F-010 Test
        // Expectations) - Skip past the end of a list is a no-op in LINQ, so this falls out
        // naturally rather than needing an explicit bounds check.
        return [.. ranked.Skip((ClampPage(page) - 1) * ClampPageSize(pageSize)).Take(ClampPageSize(pageSize))];
    }

    public static int ClampPage(int page) => Math.Max(page, 1);

    public static int ClampPageSize(int pageSize) => Math.Clamp(pageSize, 1, MaxPageSize);

    private static IOrderedEnumerable<T> OrderBy<T, TKey>(IEnumerable<T> source, Func<T, TKey> keySelector, SortDirection direction) =>
        direction == SortDirection.Asc ? source.OrderBy(keySelector) : source.OrderByDescending(keySelector);
}
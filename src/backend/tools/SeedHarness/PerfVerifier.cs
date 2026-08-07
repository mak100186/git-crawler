using System.Diagnostics;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Repositories;

using Microsoft.EntityFrameworkCore;

using Npgsql;

// F-017: performance verifier that runs against the seeded scratch database to capture EXPLAIN
// ANALYZE evidence and timed page-request numbers. Uses raw SQL for EXPLAIN ANALYZE (to avoid
// parameter-inlining issues with EF Core's ToQueryString) and the same LINQ query shape as
// GetHiddenGemsQueryHandler for timed page requests (via EF Core's own translation).

internal static class PerfVerifier
{
    public static async Task VerifyAsync(string connectionString)
    {
        await using var dbContext = new GitCrawlerDbContext(
            new DbContextOptionsBuilder<GitCrawlerDbContext>().UseNpgsql(connectionString).Options);

        // Verify scale first.
        var repoCount = await dbContext.Repositories.CountAsync();
        var scoreCount = await dbContext.Scores.CountAsync();
        var summaryCount = await dbContext.Summaries.CountAsync();
        var bookmarkCount = await dbContext.Bookmarks.CountAsync();
        Console.WriteLine($"Scale: {repoCount:N0} repos, {scoreCount:N0} scores, {summaryCount:N0} summaries, {bookmarkCount:N0} bookmarks");
        Console.WriteLine();

        // EXPLAIN ANALYZE for representative query shapes using raw SQL (avoids ToQueryString's
        // parameter inlining issues with correlated subqueries).
        Console.WriteLine("--- EXPLAIN ANALYZE (representative queries) ---");

        await RawExplainAnalyze(connectionString, "Score DESC (default sort)",
            @"SELECT r.""Id"" FROM ""Repositories"" r
              WHERE EXISTS (SELECT 1 FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"")
              ORDER BY (SELECT s.""TotalScore"" FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"" ORDER BY s.""ComputedAtUtc"" DESC LIMIT 1) DESC, r.""Id""
              LIMIT 24 OFFSET 0");

        await RawExplainAnalyze(connectionString, "Newest DESC + Language=C#",
            @"SELECT r.""Id"" FROM ""Repositories"" r
              WHERE EXISTS (SELECT 1 FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"")
                AND r.""PrimaryLanguage"" = 'C#'
              ORDER BY r.""FirstDiscoveredAtUtc"" DESC, r.""Id""
              LIMIT 24 OFFSET 0");

        await RawExplainAnalyze(connectionString, "Stars ASC + MinStars=100",
            @"SELECT r.""Id"" FROM ""Repositories"" r
              WHERE EXISTS (SELECT 1 FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"")
                AND r.""StarCount"" >= 100
              ORDER BY r.""StarCount"", r.""Id""
              LIMIT 24 OFFSET 0");

        await RawExplainAnalyze(connectionString, "Score DESC + Language=Python + Stars 50-500",
            @"SELECT r.""Id"" FROM ""Repositories"" r
              WHERE EXISTS (SELECT 1 FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"")
                AND r.""PrimaryLanguage"" = 'Python'
                AND r.""StarCount"" >= 50 AND r.""StarCount"" <= 500
              ORDER BY (SELECT s.""TotalScore"" FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"" ORDER BY s.""ComputedAtUtc"" DESC LIMIT 1) DESC, r.""Id""
              LIMIT 24 OFFSET 0");

        await RawExplainAnalyze(connectionString, "Commits DESC",
            @"SELECT r.""Id"" FROM ""Repositories"" r
              WHERE EXISTS (SELECT 1 FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"")
              ORDER BY (SELECT s.""CommitsPerWeek"" FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"" ORDER BY s.""ComputedAtUtc"" DESC LIMIT 1) DESC, r.""Id""
              LIMIT 24 OFFSET 0");

        await RawExplainAnalyze(connectionString, "DISTINCT PrimaryLanguage (GetCategories)",
            @"SELECT DISTINCT r.""PrimaryLanguage"" FROM ""Repositories"" r
              WHERE EXISTS (SELECT 1 FROM ""Scores"" s WHERE s.""RepositoryId"" = r.""Id"")
                AND r.""PrimaryLanguage"" IS NOT NULL");

        Console.WriteLine();
        Console.WriteLine("--- Timed page requests (sort x direction x facets) ---");

        // Full matrix: all four sort fields x both directions, with representative facet combos.
        var facets = new (string Name, RepositoryFilterCriteria Filter)[]
        {
            ("No filters", new RepositoryFilterCriteria()),
            ("Language=C#", new RepositoryFilterCriteria(Language: ["C#"])),
            ("Stars 50-500", new RepositoryFilterCriteria(MinStars: 50, MaxStars: 500)),
            ("Lang=Python+MinStars=10", new RepositoryFilterCriteria(Language: ["Python"], MinStars: 10)),
        };

        var sortFields = Enum.GetValues<RepositorySortField>();
        var directions = Enum.GetValues<SortDirection>();

        Console.WriteLine($"{"Sort",-10} {"Dir",-5} {"Facet",-25} {"ms",8} {"Total",8}");
        Console.WriteLine(new string('-', 60));

        foreach (var (facetName, facet) in facets)
        {
            foreach (var sortField in sortFields)
            {
                foreach (var dir in directions)
                {
                    var filter = facet with { Sort = sortField, Direction = dir, Page = 1, PageSize = 24 };
                    var (elapsed, count) = await TimePageRequest(dbContext, filter);
                    Console.WriteLine($"{sortField,-10} {dir,-5} {facetName,-25} {elapsed.TotalMilliseconds,8:F1} {count,8}");
                }
            }
        }

        // Boundary cases.
        Console.WriteLine();
        Console.WriteLine("--- Boundary cases ---");
        var (beyondLast, _) = await TimePageRequest(dbContext, new RepositoryFilterCriteria(Page: 99999, PageSize: 24));
        Console.WriteLine($"Beyond-last-page: {beyondLast.TotalMilliseconds:F1}ms");

        var (zeroMatch, zc) = await TimePageRequest(dbContext, new RepositoryFilterCriteria(Language: ["NonExistentLanguage"]));
        Console.WriteLine($"Zero-match:       {zeroMatch.TotalMilliseconds:F1}ms (count={zc})");

        var (singleMatch, sc) = await TimePageRequest(dbContext, new RepositoryFilterCriteria(Language: ["Haskell"], MinStars: 49000));
        Console.WriteLine($"Single-match:     {singleMatch.TotalMilliseconds:F1}ms (count={sc})");

        var (lastPage, lc) = await TimePageRequest(dbContext, new RepositoryFilterCriteria(Page: 4167, PageSize: 24));
        Console.WriteLine($"Last page:        {lastPage.TotalMilliseconds:F1}ms (count={lc})");
    }

    private static async Task RawExplainAnalyze(string connectionString, string label, string query)
    {
        await using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand($"EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) {query}", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        var planLines = new List<string>();
        while (await reader.ReadAsync())
        {
            planLines.Add(reader.GetString(0));
        }

        Console.WriteLine();
        Console.WriteLine($"[{label}]");
        foreach (var line in planLines.Take(20))
        {
            Console.WriteLine($"  {line}");
        }

        if (planLines.Count > 20)
        {
            Console.WriteLine($"  ... ({planLines.Count - 20} more lines)");
        }
    }

    private static async Task<(TimeSpan Elapsed, int TotalCount)> TimePageRequest(
        GitCrawlerDbContext dbContext, RepositoryFilterCriteria filter)
    {
        var sw = Stopwatch.StartNew();

        var filtered = RepositoryCardQuery.ApplyFilters(
            dbContext.Repositories.Where(r => r.Scores.Any()), filter);
        var totalCount = await filtered.CountAsync();
        var pageIds = await RepositoryCardQuery.ApplySort(filtered, filter.Sort, filter.Direction)
            .ThenBy(r => r.Id)
            .Skip((RepositoryCardQuery.ClampPage(filter.Page) - 1) * RepositoryCardQuery.ClampPageSize(filter.PageSize))
            .Take(RepositoryCardQuery.ClampPageSize(filter.PageSize))
            .Select(r => r.Id)
            .ToListAsync();

        sw.Stop();
        return (sw.Elapsed, totalCount);
    }
}
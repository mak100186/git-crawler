using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Scoring.ComputeScores;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder. Triggered by F-006's Job Scheduler via
// ComputeScoresJob.RunAsync's IMessageBus.InvokeAsync call, chained after DiscoverRepositoriesJob
// finishes (see that class) - so, like DiscoverRepositoriesCommand, this is a plain command with no
// HTTP endpoint.
public record ComputeScoresCommand;

public record ComputeScoresResult(int ScoredCount, int SkippedCount, int PrunedScoreCount);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
//
// Pure computation, no external calls (Architecture §3): this handler only reads/writes
// GitCrawlerDbContext. All of the actual scoring math lives in ScoringWeights, kept separate so it
// can be unit-tested without a DbContext at all - this class is purely data-access orchestration.
public class ComputeScoresCommandHandler(GitCrawlerDbContext dbContext, IConfiguration configuration, TimeProvider timeProvider)
{
    // How many Score rows to keep per repository (review finding M-8). Score history is append-only
    // by design - GetHiddenGemsQueryHandler reads the latest two rows to compute TrendGrowth - but
    // nothing used to delete the rest, so a daily crawl accrued 365 rows per repository per year of
    // which two were ever read. 10 is a deliberate margin over the two that are used, leaving room
    // to look back over a crawl week when diagnosing a scoring change without keeping a year.
    private readonly int _historyRetentionCount = configuration.GetValue("Scoring:ScoreHistoryRetentionCount", 10);

    public async Task<ComputeScoresResult> HandleAsync(ComputeScoresCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // "Needs scoring" = no Score row yet, or the most recent one predates the repo's last
        // crawl (i.e. new raw data has landed since this repo was last scored) - re-score on
        // re-crawl rather than only ever scoring once. Score's existing schema (F-004) allows
        // multiple rows per repo specifically to support this history rather than an upsert.
        //
        // Loaded into memory before filtering: EF Core can't translate "each repo's *latest* Score"
        // into a single SQL predicate as cleanly as a correlated subquery would need, and this
        // pipeline has no pagination anywhere else at this single-operator-v1 scale - revisit if
        // the Repositories table grows large enough for that to matter. The "latest" itself is also
        // resolved client-side (Enumerable.Max over the loaded Scores, not an ORDER BY translated to
        // SQL) rather than as a correlated-subquery projection: relational providers vary in whether
        // they can translate ORDER BY/FirstOrDefault over a DateTimeOffset column into SQL (Npgsql
        // against Postgres can; the xUnit suite's SQLite provider cannot), and since every row is
        // already being pulled into memory here anyway, there's no cost to deferring the max to LINQ
        // to Objects and making this portable across both.
        var repositoriesWithScores = await dbContext.Repositories
            .Include(r => r.Scores)
            .ToListAsync(cancellationToken);

        var scoredCount = 0;
        var skippedCount = 0;

        foreach (var repository in repositoriesWithScores)
        {
            var latestScoreAtUtc = repository.Scores.Count == 0
                ? (DateTimeOffset?)null
                : repository.Scores.Max(s => s.ComputedAtUtc);

            var needsScoring = latestScoreAtUtc is null
                || (repository.LastCrawledAtUtc is not null && latestScoreAtUtc < repository.LastCrawledAtUtc);

            if (!needsScoring)
            {
                skippedCount++;
                continue;
            }

            dbContext.Scores.Add(BuildScore(repository, now));
            scoredCount++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var prunedScoreCount = await PruneScoreHistoryAsync(cancellationToken);

        return new ComputeScoresResult(scoredCount, skippedCount, prunedScoreCount);
    }

    // Retention for M-8, run here rather than as its own recurring job: this is the one place that
    // adds Score rows, so it is also the only place history can grow. A separate Hangfire job would
    // need its own schedule, its own concurrency guard (F-016), and would spend most of its runs
    // finding nothing to do.
    //
    // One statement, one round trip. A window function is used because "keep the N most recent rows
    // per repository" is not expressible in LINQ - EF Core has no ROW_NUMBER() translation - and the
    // portable alternatives are either a query per repository or loading every row into memory,
    // which is the problem this is meant to solve. The SQL is deliberately provider-neutral:
    // double-quoted identifiers, ROW_NUMBER() OVER (PARTITION BY ...) and a subquery are all
    // supported by both Npgsql/PostgreSQL and the SQLite provider the test suite runs against
    // (window functions since SQLite 3.25).
    private async Task<int> PruneScoreHistoryAsync(CancellationToken cancellationToken)
    {
        // A retention count below 2 would delete the row TrendGrowth compares against, silently
        // flattening every growth pill on the dashboard. Treated as a misconfiguration to clamp, not
        // to honour.
        var keep = Math.Max(2, _historyRetentionCount);

        return await dbContext.Database.ExecuteSqlAsync(
            $"""
            DELETE FROM "Scores"
            WHERE "Id" IN (
                SELECT "Id" FROM (
                    SELECT "Id", ROW_NUMBER() OVER (
                        PARTITION BY "RepositoryId" ORDER BY "ComputedAtUtc" DESC, "Id" DESC
                    ) AS "RowNumber"
                    FROM "Scores"
                ) AS "Ranked"
                WHERE "Ranked"."RowNumber" > {keep}
            )
            """,
            cancellationToken);
    }

    private static Score BuildScore(Repository repository, DateTimeOffset now)
    {
        // No license is a real, valid crawl result (F-005's ApplyDiscoveredFields comment) - stored
        // as HasLicense = false with a null LicenseType here, not defaulted to a placeholder that
        // would silently read as "has some license".
        var hasLicense = !string.IsNullOrEmpty(repository.LicenseIdentifier) || !string.IsNullOrEmpty(repository.LicenseName);
        var licenseType = repository.LicenseIdentifier ?? repository.LicenseName;
        var commitsPerWeek = ScoringWeights.ComputeCommitsPerWeek(repository.CommitCount, repository.CreatedAtUtc, now);
        // Null ContributorCount alongside a non-null ContributorCountFetchedAtUtc means the crawler
        // did ask and GitHub refused - "too large to list contributors" (see
        // DiscoverRepositoriesCommandHandler's catch of GitHubContributorListUnavailableException,
        // which stamps the timestamp precisely so this state is distinguishable). ScoringWeights
        // reads that null as full marks rather than zero. Null with no timestamp is a repo whose
        // contributor count has genuinely never been fetched, which still scores as zero.
        var contributorCountForScore = repository.ContributorCount is null && repository.ContributorCountFetchedAtUtc is not null
            ? (int?)null
            : repository.ContributorCount ?? 0;
        var forkCount = repository.ForkCount;
        var starCount = repository.StarCount;

        var totalScore = ScoringWeights.ComputeTotalScore(hasLicense, commitsPerWeek, contributorCountForScore, forkCount, starCount);

        return new Score
        {
            RepositoryId = repository.Id,
            HasLicense = hasLicense,
            LicenseType = licenseType,
            CommitsPerWeek = commitsPerWeek,
            ContributorCount = repository.ContributorCount ?? 0,
            ForkCount = forkCount,
            StarCount = starCount,
            TotalScore = totalScore,
            ComputedAtUtc = now,
        };
    }
}
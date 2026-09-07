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

    // How many repositories are scored per round trip / SaveChanges. Trades round trips against
    // peak memory and changes no observable outcome, so the default suits every deployment this
    // ships to - it is configurable mainly so the batch-boundary behaviour is testable, which
    // matters because the loop below pages by keyset and that is where an off-by-one would hide.
    private readonly int _batchSize = Math.Max(1, configuration.GetValue("Scoring:BatchSize", 500));

    public async Task<ComputeScoresResult> HandleAsync(ComputeScoresCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // "Needs scoring" = no Score row yet, or the most recent one predates the repo's last
        // crawl (i.e. new raw data has landed since this repo was last scored) - re-score on
        // re-crawl rather than only ever scoring once. Score's existing schema (F-004) allows
        // multiple rows per repo specifically to support this history rather than an upsert.
        //
        // Evaluated in SQL and processed in batches (review finding H-3). This used to load the
        // entire Repositories table joined to the entire Scores table and decide in
        // LINQ-to-Objects. With no WHERE clause at all and Score being append-only, that is roughly
        // 36M rows in one query at the 100k-repository scale F-017 measured against.
        //
        // The predicate is deliberately not the one H-3's recommendation suggested
        // (`!r.Scores.Any(s => s.ComputedAtUtc >= r.LastCrawledAtUtc)`). That form changes
        // behaviour for a repository that has a Score but a null LastCrawledAtUtc: the SQL
        // comparison against NULL is never true, so Any() is false, so the repository would be
        // re-scored on every single run. The original skipped it, and so does this. The remaining
        // clause restates "max(ComputedAtUtc) < LastCrawledAtUtc" as "no Score at or after
        // LastCrawledAtUtc", which is an index probe on (RepositoryId, ComputedAtUtc) rather than
        // an aggregate over each repository's history.
        var needsScoring = dbContext.Repositories.Where(r =>
            !r.Scores.Any()
            || (r.LastCrawledAtUtc != null && !r.Scores.Any(s => s.ComputedAtUtc >= r.LastCrawledAtUtc)));

        // SkippedCount is "everything that did not need re-scoring", which was previously counted
        // in the loop over every repository. With the loop now seeing only the ones that do need
        // it, the total has to be asked for separately.
        var totalRepositoryCount = await dbContext.Repositories.CountAsync(cancellationToken);

        var scoredCount = 0;
        var lastId = 0;

        while (true)
        {
            // Keyset pagination on Id, not Skip/Take. Adding a Score row removes that repository
            // from the predicate above, so the result set shrinks as this loop runs and a numeric
            // offset would step straight over unscored repositories.
            //
            // AsNoTracking because BuildScore only reads scalar columns off the Repository - the
            // Include of r.Scores that used to be here fed a client-side Max() that no longer
            // exists, and nothing writes back to these entities.
            var batch = await needsScoring
                .Where(r => r.Id > lastId)
                .OrderBy(r => r.Id)
                .Take(_batchSize)
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            foreach (var repository in batch)
            {
                dbContext.Scores.Add(BuildScore(repository, now));
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            // Without this the change tracker holds every Score added across the whole run, which
            // would move the memory problem this method is fixing rather than solve it.
            dbContext.ChangeTracker.Clear();

            scoredCount += batch.Count;
            lastId = batch[^1].Id;
        }

        var skippedCount = totalRepositoryCount - scoredCount;

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
    // alternatives are either a query per repository or loading every row into memory, which is the
    // problem this is meant to solve. Raw SQL here is PostgreSQL, the only database this ships on
    // and now the only one the test suite runs against (review finding H-4).
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
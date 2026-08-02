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

public record ComputeScoresResult(int ScoredCount, int SkippedCount);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
//
// Pure computation, no external calls (Architecture §3): this handler only reads/writes
// GitCrawlerDbContext. All of the actual scoring math lives in ScoringWeights, kept separate so it
// can be unit-tested without a DbContext at all - this class is purely data-access orchestration.
public class ComputeScoresCommandHandler(GitCrawlerDbContext dbContext, TimeProvider timeProvider)
{
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

        return new ComputeScoresResult(scoredCount, skippedCount);
    }

    private static Score BuildScore(Repository repository, DateTimeOffset now)
    {
        // No license is a real, valid crawl result (F-005's ApplyDiscoveredFields comment) - stored
        // as HasLicense = false with a null LicenseType here, not defaulted to a placeholder that
        // would silently read as "has some license".
        var hasLicense = !string.IsNullOrEmpty(repository.LicenseIdentifier) || !string.IsNullOrEmpty(repository.LicenseName);
        var licenseType = repository.LicenseIdentifier ?? repository.LicenseName;
        var commitsPerWeek = ScoringWeights.ComputeCommitsPerWeek(repository.CommitCount, repository.CreatedAtUtc, now);
        var contributorCount = repository.ContributorCount ?? 0;
        var forkCount = repository.ForkCount;
        var starCount = repository.StarCount;

        var totalScore = ScoringWeights.ComputeTotalScore(hasLicense, commitsPerWeek, contributorCount, forkCount, starCount);

        return new Score
        {
            RepositoryId = repository.Id,
            HasLicense = hasLicense,
            LicenseType = licenseType,
            CommitsPerWeek = commitsPerWeek,
            ContributorCount = contributorCount,
            ForkCount = forkCount,
            StarCount = starCount,
            TotalScore = totalScore,
            ComputedAtUtc = now,
        };
    }
}
using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Scoring.ComputeScores;
using GitCrawler.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GitCrawler.Api.Tests.Features.Scoring.ComputeScores;

// Runs against the shared PostgreSQL container (see PostgresFixture) so the FK/relationship
// navigation this handler relies on (Repository.Scores) behaves as it does in production - and so
// PruneScoreHistoryAsync's raw ROW_NUMBER() retention SQL is executed by the database that actually
// runs it.
public class ComputeScoresCommandHandlerTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

    private ComputeScoresCommandHandler CreateHandler(IConfiguration? configuration = null) =>
        new(DbContext, configuration ?? new ConfigurationBuilder().Build(), _timeProvider);

    private static IConfiguration ConfigurationWithRetention(int retentionCount) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scoring:ScoreHistoryRetentionCount"] = retentionCount.ToString(),
            })
            .Build();

    private Repository NewRepository(
        long gitHubId,
        string? licenseIdentifier = "MIT",
        string? licenseName = "MIT License",
        int? commitCount = 100,
        int? contributorCount = 5,
        int forkCount = 3,
        int starCount = 8,
        DateTimeOffset? createdAtUtc = null,
        DateTimeOffset? lastCrawledAtUtc = null) => new()
        {
            GitHubId = gitHubId,
            Owner = "octocat",
            Name = $"repo-{gitHubId}",
            Url = $"https://github.com/octocat/repo-{gitHubId}",
            DefaultBranch = "main",
            LicenseIdentifier = licenseIdentifier,
            LicenseName = licenseName,
            CommitCount = commitCount,
            ContributorCount = contributorCount,
            ForkCount = forkCount,
            StarCount = starCount,
            CreatedAtUtc = createdAtUtc ?? _timeProvider.GetUtcNow().AddYears(-1),
            LastCrawledAtUtc = lastCrawledAtUtc ?? _timeProvider.GetUtcNow(),
        };

    [Fact]
    public async Task Handle_RepositoryWithNoScore_WritesNewScoreRow_WithSignalsFromRepositoryFields()
    {
        var repository = NewRepository(gitHubId: 1, licenseIdentifier: "Apache-2.0", commitCount: 52, contributorCount: 7, forkCount: 4, starCount: 15);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(1, result.ScoredCount);
        Assert.Equal(0, result.SkippedCount);

        var score = await DbContext.Scores.SingleAsync(s => s.RepositoryId == repository.Id);
        Assert.True(score.HasLicense);
        Assert.Equal("Apache-2.0", score.LicenseType);
        Assert.Equal(7, score.ContributorCount);
        Assert.Equal(4, score.ForkCount);
        Assert.Equal(15, score.StarCount);
        // Signals are not swapped/mismatched: ContributorCount (7), ForkCount (4), and StarCount
        // (15) must each land in their own column, not each other's.
        Assert.NotEqual(score.ContributorCount, score.ForkCount);
        Assert.NotEqual(score.StarCount, score.ForkCount);
        Assert.NotEqual(score.StarCount, score.ContributorCount);
        Assert.True(score.CommitsPerWeek > 0);
        Assert.True(score.TotalScore > 0);
        Assert.Equal(_timeProvider.GetUtcNow(), score.ComputedAtUtc);
    }

    [Fact]
    public async Task Handle_RepositoryAlreadyScoredAfterItsLastCrawl_IsSkipped()
    {
        var repository = NewRepository(gitHubId: 2, lastCrawledAtUtc: _timeProvider.GetUtcNow().AddDays(-2));
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        DbContext.Scores.Add(new Score
        {
            RepositoryId = repository.Id,
            HasLicense = true,
            LicenseType = "MIT",
            CommitsPerWeek = 1,
            ContributorCount = 1,
            ForkCount = 1,
            TotalScore = 10,
            // Newer than the repo's last crawl - no new raw data has landed since this score was
            // computed, so it should not be re-scored.
            ComputedAtUtc = _timeProvider.GetUtcNow().AddDays(-1),
        });
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(0, result.ScoredCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, await DbContext.Scores.CountAsync(s => s.RepositoryId == repository.Id));
    }

    [Fact]
    public async Task Handle_RepositoryRecrawledSinceLastScore_IsReScored_AddsNewScoreRow()
    {
        var repository = NewRepository(gitHubId: 3, lastCrawledAtUtc: _timeProvider.GetUtcNow());
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        DbContext.Scores.Add(new Score
        {
            RepositoryId = repository.Id,
            HasLicense = true,
            LicenseType = "MIT",
            CommitsPerWeek = 1,
            ContributorCount = 1,
            ForkCount = 1,
            TotalScore = 10,
            // Older than the repo's LastCrawledAtUtc set above - new crawl data has landed since.
            ComputedAtUtc = _timeProvider.GetUtcNow().AddDays(-1),
        });
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(1, result.ScoredCount);
        Assert.Equal(0, result.SkippedCount);
        // Score's schema supports multiple rows per repo (a history) by design (F-004) - re-scoring
        // adds a new row rather than overwriting the old one.
        Assert.Equal(2, await DbContext.Scores.CountAsync(s => s.RepositoryId == repository.Id));
    }

    [Fact]
    public async Task Handle_RepositoryWithNoLicense_ComputesHasLicenseFalse_NotAPlaceholder()
    {
        var repository = NewRepository(gitHubId: 4, licenseIdentifier: null, licenseName: null);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        var score = await DbContext.Scores.SingleAsync(s => s.RepositoryId == repository.Id);
        Assert.False(score.HasLicense);
        Assert.Null(score.LicenseType);
        Assert.False(double.IsNaN(score.TotalScore));
        Assert.False(double.IsInfinity(score.TotalScore));
    }

    [Fact]
    public async Task Handle_ZeroContributorsForksAndStars_DoesNotCrashOrProduceNaNOrInfinity()
    {
        var repository = NewRepository(gitHubId: 5, contributorCount: 0, forkCount: 0, commitCount: 0, starCount: 0);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        var score = await DbContext.Scores.SingleAsync(s => s.RepositoryId == repository.Id);
        Assert.Equal(0, score.ContributorCount);
        Assert.Equal(0, score.ForkCount);
        Assert.Equal(0, score.StarCount);
        Assert.Equal(0.0, score.CommitsPerWeek);
        Assert.False(double.IsNaN(score.TotalScore));
        Assert.False(double.IsInfinity(score.TotalScore));
    }

    [Fact]
    public async Task Handle_RepositoryWithNullContributorCount_TreatsAsZero_DoesNotCrash()
    {
        // ContributorCount is nullable on Repository (F-005: never fetched yet) - the handler must
        // coalesce this rather than throwing/propagating null into the Score's non-nullable int
        // column.
        var repository = NewRepository(gitHubId: 6, contributorCount: null);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        var score = await DbContext.Scores.SingleAsync(s => s.RepositoryId == repository.Id);
        Assert.Equal(0, score.ContributorCount);
    }

    [Fact]
    public async Task Handle_NoRepositories_CompletesWithoutError()
    {
        var result = await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(0, result.ScoredCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.PrunedScoreCount);
    }

    [Fact]
    public async Task Handle_ScoreHistoryExceedsRetentionCount_DeletesTheOldestRowsOnly()
    {
        var repository = NewRepository(gitHubId: 10);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        // Eight days of history, oldest first. The repository's LastCrawledAtUtc is newer than all
        // of them, so this run also appends a ninth, current row before retention runs.
        await SeedScoreHistoryAsync(repository.Id, days: 8);

        var result = await CreateHandler(ConfigurationWithRetention(3))
            .HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        var remaining = await DbContext.Scores
            .Where(s => s.RepositoryId == repository.Id)
            .Select(s => s.ComputedAtUtc)
            .ToListAsync();

        // Nine rows in, three kept: the six oldest go, and what survives is the newest three.
        Assert.Equal(6, result.PrunedScoreCount);
        Assert.Equal(3, remaining.Count);
        Assert.All(remaining, computedAt => Assert.True(computedAt >= HistoryDay(6)));
    }

    [Fact]
    public async Task Handle_ScoreHistoryWithinRetentionCount_DeletesNothing()
    {
        var repository = NewRepository(gitHubId: 11);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        await SeedScoreHistoryAsync(repository.Id, days: 4);

        var result = await CreateHandler(ConfigurationWithRetention(10))
            .HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        // Four seeded rows plus the one this run appends, all under the limit of ten.
        Assert.Equal(0, result.PrunedScoreCount);
        Assert.Equal(5, await DbContext.Scores.CountAsync(s => s.RepositoryId == repository.Id));
    }

    [Fact]
    public async Task Handle_RetentionConfiguredBelowTwo_StillKeepsTheRowTrendGrowthComparesAgainst()
    {
        // TrendGrowth needs the latest two rows. A retention count of 1 (or 0) would silently
        // flatten every growth pill on the dashboard, so the handler clamps rather than obeying.
        var repository = NewRepository(gitHubId: 12);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        await SeedScoreHistoryAsync(repository.Id, days: 5);

        await CreateHandler(ConfigurationWithRetention(1)).HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(2, await DbContext.Scores.CountAsync(s => s.RepositoryId == repository.Id));
    }

    [Fact]
    public async Task Handle_MultipleRepositories_PrunesEachIndependently()
    {
        var kept = NewRepository(gitHubId: 13);
        var pruned = NewRepository(gitHubId: 14);
        DbContext.Repositories.AddRange(kept, pruned);
        await DbContext.SaveChangesAsync();

        await SeedScoreHistoryAsync(kept.Id, days: 2);
        await SeedScoreHistoryAsync(pruned.Id, days: 7);

        await CreateHandler(ConfigurationWithRetention(4)).HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        // Retention partitions by repository. Both gain a row from this run: the first ends at
        // three, under the limit and untouched; the second is cut from eight back to four.
        Assert.Equal(3, await DbContext.Scores.CountAsync(s => s.RepositoryId == kept.Id));
        Assert.Equal(4, await DbContext.Scores.CountAsync(s => s.RepositoryId == pruned.Id));
    }

    private static DateTimeOffset HistoryDay(int dayIndex) =>
        new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero).AddDays(dayIndex);

    // Rows dated before the fixture clock so the handler treats these repositories as already
    // scored and the only thing under test is retention.
    private async Task SeedScoreHistoryAsync(int repositoryId, int days)
    {
        for (var day = 0; day < days; day++)
        {
            DbContext.Scores.Add(new Score
            {
                RepositoryId = repositoryId,
                HasLicense = true,
                LicenseType = "MIT",
                CommitsPerWeek = 1,
                ContributorCount = 1,
                ForkCount = 1,
                StarCount = 1,
                TotalScore = 50,
                ComputedAtUtc = HistoryDay(day),
            });
        }

        await DbContext.SaveChangesAsync();
    }
}
using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Scoring.ComputeScores;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Scoring.ComputeScores;

// Same SQLite-backed DbContext approach as DiscoverRepositoriesCommandHandlerTests / GitCrawlerDbContextTests
// (not the EF Core InMemory provider) so the FK/relationship navigation this handler relies on
// (Repository.Scores) behaves like the real relational provider.
public class ComputeScoresCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

    public ComputeScoresCommandHandlerTests()
    {
        // The in-memory SQLite database is destroyed the moment its last connection closes, so
        // this connection must stay open for the test's lifetime rather than being opened per call.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GitCrawlerDbContext>().UseSqlite(_connection).Options;
        _dbContext = new GitCrawlerDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private ComputeScoresCommandHandler CreateHandler() => new(_dbContext, _timeProvider);

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
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(1, result.ScoredCount);
        Assert.Equal(0, result.SkippedCount);

        var score = await _dbContext.Scores.SingleAsync(s => s.RepositoryId == repository.Id);
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
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(new Score
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
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(0, result.ScoredCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, await _dbContext.Scores.CountAsync(s => s.RepositoryId == repository.Id));
    }

    [Fact]
    public async Task Handle_RepositoryRecrawledSinceLastScore_IsReScored_AddsNewScoreRow()
    {
        var repository = NewRepository(gitHubId: 3, lastCrawledAtUtc: _timeProvider.GetUtcNow());
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(new Score
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
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(1, result.ScoredCount);
        Assert.Equal(0, result.SkippedCount);
        // Score's schema supports multiple rows per repo (a history) by design (F-004) - re-scoring
        // adds a new row rather than overwriting the old one.
        Assert.Equal(2, await _dbContext.Scores.CountAsync(s => s.RepositoryId == repository.Id));
    }

    [Fact]
    public async Task Handle_RepositoryWithNoLicense_ComputesHasLicenseFalse_NotAPlaceholder()
    {
        var repository = NewRepository(gitHubId: 4, licenseIdentifier: null, licenseName: null);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        var score = await _dbContext.Scores.SingleAsync(s => s.RepositoryId == repository.Id);
        Assert.False(score.HasLicense);
        Assert.Null(score.LicenseType);
        Assert.False(double.IsNaN(score.TotalScore));
        Assert.False(double.IsInfinity(score.TotalScore));
    }

    [Fact]
    public async Task Handle_ZeroContributorsForksAndStars_DoesNotCrashOrProduceNaNOrInfinity()
    {
        var repository = NewRepository(gitHubId: 5, contributorCount: 0, forkCount: 0, commitCount: 0, starCount: 0);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        var score = await _dbContext.Scores.SingleAsync(s => s.RepositoryId == repository.Id);
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
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        var score = await _dbContext.Scores.SingleAsync(s => s.RepositoryId == repository.Id);
        Assert.Equal(0, score.ContributorCount);
    }

    [Fact]
    public async Task Handle_NoRepositories_CompletesWithoutError()
    {
        var result = await CreateHandler().HandleAsync(new ComputeScoresCommand(), CancellationToken.None);

        Assert.Equal(0, result.ScoredCount);
        Assert.Equal(0, result.SkippedCount);
    }
}
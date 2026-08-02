using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Trends.AggregateTrends;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GitCrawler.Api.Tests.Features.Trends.AggregateTrends;

// Same SQLite-backed DbContext approach as ComputeScoresCommandHandlerTests /
// GenerateSummariesCommandHandlerTests (not the EF Core InMemory provider) so the
// Repository/Score/Summary navigation this handler relies on behaves like the real relational
// provider.
public class AggregateTrendsCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

    public AggregateTrendsCommandHandlerTests()
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

    private AggregateTrendsCommandHandler CreateHandler(IConfiguration? configuration = null) =>
        new(_dbContext, configuration ?? new ConfigurationBuilder().Build(), _timeProvider);

    private static IConfiguration ConfigWith(int periodDays) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Trends:PeriodDays", periodDays.ToString())])
            .Build();

    private Repository NewRepository(long gitHubId, string? primaryLanguage) => new()
    {
        GitHubId = gitHubId,
        Owner = "octocat",
        Name = $"repo-{gitHubId}",
        Url = $"https://github.com/octocat/repo-{gitHubId}",
        DefaultBranch = "main",
        PrimaryLanguage = primaryLanguage,
        CreatedAtUtc = _timeProvider.GetUtcNow().AddYears(-1),
    };

    private static Score NewScore(int repositoryId, double totalScore, DateTimeOffset computedAtUtc) => new()
    {
        RepositoryId = repositoryId,
        TotalScore = totalScore,
        ComputedAtUtc = computedAtUtc,
    };

    private Summary NewSummary(int repositoryId) => new()
    {
        RepositoryId = repositoryId,
        Content = "a summary",
        GeneratedAtUtc = _timeProvider.GetUtcNow(),
    };

    [Fact]
    public async Task Handle_RepositoryWithScoreAndSummary_IsCountedInItsCategory()
    {
        var repository = NewRepository(gitHubId: 1, primaryLanguage: "C#");
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(NewScore(repository.Id, 50, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repository.Id));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        Assert.Equal(1, result.TrendCount);
        Assert.Equal(1, result.RepositoryCount);

        var trend = await _dbContext.TrendAggregates.SingleAsync();
        Assert.Equal("C#", trend.Category);
        Assert.Equal(1, trend.RepositoryCount);
        Assert.Equal(50, trend.AverageScore);

        // Default period (Trends:PeriodDays not configured, so the 1-day default applies): a daily
        // snapshot taken on this run's date, not a rolling window.
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        Assert.Equal(today, trend.PeriodStart);
        Assert.Equal(today, trend.PeriodEnd);
    }

    [Fact]
    public async Task Handle_RepositoryWithOnlyScore_NoSummaryYet_IsExcluded()
    {
        var repository = NewRepository(gitHubId: 2, primaryLanguage: "C#");
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(NewScore(repository.Id, 50, _timeProvider.GetUtcNow()));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        Assert.Equal(0, result.TrendCount);
        Assert.Equal(0, result.RepositoryCount);
        Assert.Empty(await _dbContext.TrendAggregates.ToListAsync());
    }

    [Fact]
    public async Task Handle_RepositoryWithNullPrimaryLanguage_IsExcludedFromAllTrends()
    {
        var repository = NewRepository(gitHubId: 3, primaryLanguage: null);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(NewScore(repository.Id, 50, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repository.Id));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        Assert.Equal(0, result.TrendCount);
        Assert.Equal(0, result.RepositoryCount);
        Assert.Empty(await _dbContext.TrendAggregates.ToListAsync());
    }

    [Fact]
    public async Task Handle_MultipleRepositoriesInSameCategory_ComputesRepositoryCountAndAverageScore()
    {
        var repositoryA = NewRepository(gitHubId: 4, primaryLanguage: "C#");
        var repositoryB = NewRepository(gitHubId: 5, primaryLanguage: "C#");
        _dbContext.Repositories.AddRange(repositoryA, repositoryB);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(NewScore(repositoryA.Id, 40, _timeProvider.GetUtcNow()));
        _dbContext.Scores.Add(NewScore(repositoryB.Id, 60, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repositoryA.Id));
        _dbContext.Summaries.Add(NewSummary(repositoryB.Id));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        Assert.Equal(1, result.TrendCount);
        Assert.Equal(2, result.RepositoryCount);

        var trend = await _dbContext.TrendAggregates.SingleAsync(t => t.Category == "C#");
        Assert.Equal(2, trend.RepositoryCount);
        Assert.Equal(50, trend.AverageScore);
    }

    [Fact]
    public async Task Handle_RepositoryWithMultipleScoreRows_UsesLatestByComputedAtUtc_NotHighestEverValue()
    {
        var repository = NewRepository(gitHubId: 6, primaryLanguage: "C#");
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        // Older row has the higher TotalScore (90); newer row has a lower one (30). The rollup must
        // reflect the repo's current standing (30), not its historical peak (90) - the same
        // distinction ComputeScoresCommandHandler/GenerateSummariesCommandHandler each draw for
        // their own Score reads.
        _dbContext.Scores.Add(NewScore(repository.Id, 90, _timeProvider.GetUtcNow().AddDays(-2)));
        _dbContext.Scores.Add(NewScore(repository.Id, 30, _timeProvider.GetUtcNow().AddDays(-1)));
        _dbContext.Summaries.Add(NewSummary(repository.Id));
        await _dbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        var trend = await _dbContext.TrendAggregates.SingleAsync();
        Assert.Equal(30, trend.AverageScore);
    }

    [Fact]
    public async Task Handle_RerunForSamePeriod_UpsertsExistingTrendAggregateRow_DoesNotCreateDuplicate()
    {
        var repositoryA = NewRepository(gitHubId: 7, primaryLanguage: "C#");
        _dbContext.Repositories.Add(repositoryA);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(NewScore(repositoryA.Id, 40, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repositoryA.Id));
        await _dbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        Assert.Equal(1, await _dbContext.TrendAggregates.CountAsync());
        var firstRun = await _dbContext.TrendAggregates.SingleAsync();
        Assert.Equal(1, firstRun.RepositoryCount);
        Assert.Equal(40, firstRun.AverageScore);

        // A second repo becomes eligible before the re-run (still the same period/day).
        var repositoryB = NewRepository(gitHubId: 8, primaryLanguage: "C#");
        _dbContext.Repositories.Add(repositoryB);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repositoryB.Id, 60, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repositoryB.Id));
        await _dbContext.SaveChangesAsync();

        await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        // Still exactly one row for (Category, PeriodStart, PeriodEnd) - updated in place, not
        // duplicated (NFR-003 idempotency).
        Assert.Equal(1, await _dbContext.TrendAggregates.CountAsync());
        var secondRun = await _dbContext.TrendAggregates.SingleAsync();
        Assert.Equal(firstRun.Id, secondRun.Id);
        Assert.Equal(2, secondRun.RepositoryCount);
        Assert.Equal(50, secondRun.AverageScore);
    }

    [Fact]
    public async Task Handle_TwoDifferentCategoriesInSamePeriod_EachGetsOwnTrendAggregateRow()
    {
        var repositoryA = NewRepository(gitHubId: 9, primaryLanguage: "C#");
        var repositoryB = NewRepository(gitHubId: 10, primaryLanguage: "Python");
        _dbContext.Repositories.AddRange(repositoryA, repositoryB);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(NewScore(repositoryA.Id, 40, _timeProvider.GetUtcNow()));
        _dbContext.Scores.Add(NewScore(repositoryB.Id, 70, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repositoryA.Id));
        _dbContext.Summaries.Add(NewSummary(repositoryB.Id));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        Assert.Equal(2, result.TrendCount);
        Assert.Equal(2, result.RepositoryCount);

        var csharpTrend = await _dbContext.TrendAggregates.SingleAsync(t => t.Category == "C#");
        Assert.Equal(1, csharpTrend.RepositoryCount);
        Assert.Equal(40, csharpTrend.AverageScore);

        var pythonTrend = await _dbContext.TrendAggregates.SingleAsync(t => t.Category == "Python");
        Assert.Equal(1, pythonTrend.RepositoryCount);
        Assert.Equal(70, pythonTrend.AverageScore);
    }

    [Fact]
    public async Task Handle_NoQualifyingRepositories_IsNoOp_WritesNoTrendAggregateRows()
    {
        var result = await CreateHandler().HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        Assert.Equal(0, result.TrendCount);
        Assert.Equal(0, result.RepositoryCount);
        Assert.Empty(await _dbContext.TrendAggregates.ToListAsync());
    }

    [Fact]
    public async Task Handle_ConfiguredMultiDayPeriod_ComputesPeriodStartAsPeriodEndMinusPeriodDaysMinusOne()
    {
        var repository = NewRepository(gitHubId: 11, primaryLanguage: "C#");
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        _dbContext.Scores.Add(NewScore(repository.Id, 50, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repository.Id));
        await _dbContext.SaveChangesAsync();

        await CreateHandler(ConfigWith(periodDays: 3)).HandleAsync(new AggregateTrendsCommand(), CancellationToken.None);

        var trend = await _dbContext.TrendAggregates.SingleAsync();
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        Assert.Equal(today, trend.PeriodEnd);
        Assert.Equal(today.AddDays(-2), trend.PeriodStart);
    }
}
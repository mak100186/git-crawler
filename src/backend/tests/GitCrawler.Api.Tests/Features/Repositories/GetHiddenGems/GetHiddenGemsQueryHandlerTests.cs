using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Repositories;
using GitCrawler.Api.Features.Repositories.GetHiddenGems;
using GitCrawler.Api.Features.Scoring.ComputeScores;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Repositories.GetHiddenGems;

public class GetHiddenGemsQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;

    public GetHiddenGemsQueryHandlerTests()
    {
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

    private GetHiddenGemsQueryHandler CreateHandler() => new(_dbContext);

    private async Task<Repository> AddRepositoryAsync(long gitHubId, string name = "repo", string? language = "C#", int stars = 10)
    {
        var repository = new Repository
        {
            GitHubId = gitHubId,
            Owner = "octocat",
            Name = name,
            Url = $"https://github.com/octocat/{name}",
            DefaultBranch = "main",
            PrimaryLanguage = language,
            StarCount = stars,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FirstDiscoveredAtUtc = DateTimeOffset.UtcNow,
        };
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    private async Task<Score> AddScoreAsync(
        int repositoryId,
        bool hasLicense = true,
        string? licenseType = "MIT",
        double commitsPerWeek = 3.0,
        int contributorCount = 4,
        int forkCount = 5,
        int starCount = 10,
        double totalScore = 42.0,
        DateTimeOffset? computedAt = null)
    {
        var score = new Score
        {
            RepositoryId = repositoryId,
            HasLicense = hasLicense,
            LicenseType = licenseType,
            CommitsPerWeek = commitsPerWeek,
            ContributorCount = contributorCount,
            ForkCount = forkCount,
            StarCount = starCount,
            TotalScore = totalScore,
            ComputedAtUtc = computedAt ?? DateTimeOffset.UtcNow,
        };
        _dbContext.Scores.Add(score);
        await _dbContext.SaveChangesAsync();
        return score;
    }

    [Fact]
    public async Task Handle_RepositoryWithNoScore_IsExcluded()
    {
        await AddRepositoryAsync(1);
        var scored = await AddRepositoryAsync(2);
        await AddScoreAsync(scored.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal([scored.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_ScoreBreakdown_MatchesScoringWeightsConstantsExactly()
    {
        var repository = await AddRepositoryAsync(1);
        await AddScoreAsync(
            repository.Id, hasLicense: true, licenseType: "MIT", commitsPerWeek: 3.5, contributorCount: 4, forkCount: 5, starCount: 10, totalScore: 55.5);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        var breakdown = result.Items.Single().ScoreBreakdown;
        Assert.True(breakdown.HasLicense);
        Assert.Equal("MIT", breakdown.LicenseType);
        Assert.Equal(ScoringWeights.LicenseWeight, breakdown.LicenseWeight);
        Assert.Equal(3.5, breakdown.CommitsPerWeek);
        Assert.Equal(ScoringWeights.CommitsPerWeekWeight, breakdown.CommitsPerWeekWeight);
        Assert.Equal(4, breakdown.ContributorCount);
        Assert.Equal(ScoringWeights.ContributorCountWeight, breakdown.ContributorCountWeight);
        Assert.Equal(5, breakdown.ForkCount);
        Assert.Equal(ScoringWeights.ForkCountWeight, breakdown.ForkCountWeight);
        Assert.Equal(10, breakdown.StarCount);
        Assert.Equal(ScoringWeights.StarCountWeight, breakdown.StarCountWeight);
        Assert.Equal(55.5, breakdown.TotalScore);
    }

    [Fact]
    public async Task Handle_MultipleScores_UsesLatestByComputedAtUtc_NotHighestEverTotalScore()
    {
        var repository = await AddRepositoryAsync(1);
        // Highest-ever TotalScore is 90 (older), latest is 20 (newer) - regression test for the
        // same class of bug F-008 already caught (see docs/handoff.md): the breakdown must reflect
        // current standing, not a historical peak.
        await AddScoreAsync(repository.Id, totalScore: 90, computedAt: DateTimeOffset.UtcNow.AddDays(-2));
        await AddScoreAsync(repository.Id, totalScore: 20, computedAt: DateTimeOffset.UtcNow.AddDays(-1));

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal(20, result.Items.Single().ScoreBreakdown.TotalScore);
    }

    [Fact]
    public async Task Handle_NoFilters_DefaultsToScoreDesc()
    {
        var repositoryA = await AddRepositoryAsync(1);
        await AddScoreAsync(repositoryA.Id, totalScore: 10);
        var repositoryB = await AddRepositoryAsync(2);
        await AddScoreAsync(repositoryB.Id, totalScore: 90);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal([repositoryB.Id, repositoryA.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_LanguageAndStarsFilters_CombineWithAnd()
    {
        var repositoryA = await AddRepositoryAsync(1, language: "C#", stars: 10);
        await AddScoreAsync(repositoryA.Id);
        var repositoryB = await AddRepositoryAsync(2, language: "Go", stars: 10);
        await AddScoreAsync(repositoryB.Id);
        var repositoryC = await AddRepositoryAsync(3, language: "C#", stars: 1);
        await AddScoreAsync(repositoryC.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Language: ["C#"], MinStars: 5)), CancellationToken.None);

        Assert.Equal([repositoryA.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_EmptyFilterResult_ReturnsEmptyNotError()
    {
        var repository = await AddRepositoryAsync(1, language: "C#");
        await AddScoreAsync(repository.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Language: ["Rust"])), CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_PageBeyondLast_ReturnsEmptyNotError()
    {
        var repository = await AddRepositoryAsync(1);
        await AddScoreAsync(repository.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Page: 99, PageSize: 24)), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_NoTrendAggregateForCategory_TrendGrowthIsNull()
    {
        var repository = await AddRepositoryAsync(1, language: "C#");
        await AddScoreAsync(repository.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Null(result.Items.Single().TrendGrowth);
    }

    [Fact]
    public async Task Handle_SinglePeriodTrendAggregate_TrendGrowthFallsBackToAverageScore()
    {
        var repository = await AddRepositoryAsync(1, language: "C#");
        await AddScoreAsync(repository.Id);
        await AddTrendAggregateAsync("C#", periodStart: DateOnly.FromDateTime(DateTime.UtcNow), averageScore: 72.4);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal("72 avg score", result.Items.Single().TrendGrowth);
    }

    [Fact]
    public async Task Handle_TwoPeriodTrendAggregate_TrendGrowthIsPercentChangeVsPreviousPeriod()
    {
        var repository = await AddRepositoryAsync(1, language: "C#");
        await AddScoreAsync(repository.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Previous period 50, current period 60 - a +20% increase (regression-shaped: the repo's own
        // category, not some other category, must be the one matched).
        await AddTrendAggregateAsync("C#", periodStart: today.AddDays(-1), averageScore: 50.0);
        await AddTrendAggregateAsync("C#", periodStart: today, averageScore: 60.0);
        await AddTrendAggregateAsync("Go", periodStart: today, averageScore: 10.0);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal("▲ +20% vs. last period", result.Items.Single().TrendGrowth);
    }

    [Fact]
    public async Task Handle_TwoPeriodTrendAggregate_DecliningScore_ShowsDownArrow()
    {
        var repository = await AddRepositoryAsync(1, language: "C#");
        await AddScoreAsync(repository.Id);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await AddTrendAggregateAsync("C#", periodStart: today.AddDays(-1), averageScore: 80.0);
        await AddTrendAggregateAsync("C#", periodStart: today, averageScore: 60.0);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal("▼ -25% vs. last period", result.Items.Single().TrendGrowth);
    }

    private async Task AddTrendAggregateAsync(string category, DateOnly periodStart, double averageScore, int repositoryCount = 1)
    {
        _dbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = category,
            PeriodStart = periodStart,
            PeriodEnd = periodStart,
            RepositoryCount = repositoryCount,
            AverageScore = averageScore,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
    }
}
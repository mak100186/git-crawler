using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Trends.GetTrending;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Trends.GetTrending;

public class GetTrendingQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;

    public GetTrendingQueryHandlerTests()
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

    private GetTrendingQueryHandler CreateHandler() => new(_dbContext);

    private async Task AddTrendAggregateAsync(string category, int repositoryCount, double averageScore)
    {
        _dbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = category,
            PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow),
            PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow),
            RepositoryCount = repositoryCount,
            AverageScore = averageScore,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task<Repository> AddRepositoryAsync(long gitHubId, string? language, string name = "repo")
    {
        var repository = new Repository
        {
            GitHubId = gitHubId,
            Owner = "octocat",
            Name = name,
            Url = $"https://github.com/octocat/{name}",
            DefaultBranch = "main",
            PrimaryLanguage = language,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FirstDiscoveredAtUtc = DateTimeOffset.UtcNow,
        };
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    private async Task AddScoreAsync(int repositoryId, double totalScore = 50, DateTimeOffset? computedAt = null)
    {
        _dbContext.Scores.Add(new Score { RepositoryId = repositoryId, TotalScore = totalScore, ComputedAtUtc = computedAt ?? DateTimeOffset.UtcNow });
        await _dbContext.SaveChangesAsync();
    }

    private async Task AddSummaryAsync(int repositoryId, string content = "summary", DateTimeOffset? generatedAt = null)
    {
        _dbContext.Summaries.Add(new Summary { RepositoryId = repositoryId, Content = content, GeneratedAtUtc = generatedAt ?? DateTimeOffset.UtcNow });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_NoTrendAggregates_ReturnsEmptyList()
    {
        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetTrendingQuery(), CancellationToken.None);

        Assert.Empty(result.Trends);
    }

    [Fact]
    public async Task Handle_ReturnsTrendFields()
    {
        await AddTrendAggregateAsync("C#", repositoryCount: 3, averageScore: 61.5);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetTrendingQuery(), CancellationToken.None);

        var trend = result.Trends.Single();
        Assert.Equal("C#", trend.Category);
        Assert.Equal(3, trend.RepositoryCount);
        Assert.Equal(61.5, trend.AverageScore);
    }

    [Fact]
    public async Task Handle_ContributingRepos_MatchesAggregateTrendsMembershipCriteria_HasScoreAndSummary()
    {
        await AddTrendAggregateAsync("C#", repositoryCount: 1, averageScore: 50);

        // Qualifies: scored and summarized, matching category.
        var qualifies = await AddRepositoryAsync(1, "C#");
        await AddScoreAsync(qualifies.Id);
        await AddSummaryAsync(qualifies.Id);

        // Does not qualify: scored but never summarized.
        var scoredOnly = await AddRepositoryAsync(2, "C#");
        await AddScoreAsync(scoredOnly.Id);

        // Does not qualify: summarized but never scored.
        var summarizedOnly = await AddRepositoryAsync(3, "C#");
        await AddSummaryAsync(summarizedOnly.Id);

        // Does not qualify: wrong category.
        var wrongCategory = await AddRepositoryAsync(4, "Go");
        await AddScoreAsync(wrongCategory.Id);
        await AddSummaryAsync(wrongCategory.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetTrendingQuery(), CancellationToken.None);

        var trend = result.Trends.Single(t => t.Category == "C#");
        Assert.Equal([qualifies.Id], trend.ContributingRepositories.Select(r => r.Id));
    }

    [Fact]
    public async Task Handle_ContributingRepoSummary_UsesLatestByGeneratedAtUtc()
    {
        await AddTrendAggregateAsync("C#", repositoryCount: 1, averageScore: 50);

        var repository = await AddRepositoryAsync(1, "C#");
        await AddScoreAsync(repository.Id);
        await AddSummaryAsync(repository.Id, "old summary", generatedAt: DateTimeOffset.UtcNow.AddDays(-1));
        await AddSummaryAsync(repository.Id, "new summary", generatedAt: DateTimeOffset.UtcNow);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetTrendingQuery(), CancellationToken.None);

        Assert.Equal("new summary", result.Trends.Single().ContributingRepositories.Single().SummaryContent);
    }

    [Fact]
    public async Task Handle_TrendWithNoContributingRepos_ReturnsEmptyContributingList()
    {
        await AddTrendAggregateAsync("Rust", repositoryCount: 0, averageScore: 0);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetTrendingQuery(), CancellationToken.None);

        Assert.Empty(result.Trends.Single().ContributingRepositories);
    }

    [Fact]
    public async Task Handle_MultipleTrends_OrderedByAverageScoreDescending()
    {
        await AddTrendAggregateAsync("Low", repositoryCount: 1, averageScore: 10);
        await AddTrendAggregateAsync("High", repositoryCount: 1, averageScore: 90);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetTrendingQuery(), CancellationToken.None);

        Assert.Equal(["High", "Low"], result.Trends.Select(t => t.Category));
    }
}
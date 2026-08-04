using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Categories.GetCategories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Categories.GetCategories;

public class GetCategoriesQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;
    private long _nextGitHubId = 1;

    public GetCategoriesQueryHandlerTests()
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

    private GetCategoriesQueryHandler CreateHandler() => new(_dbContext);

    private async Task<Repository> AddRepositoryAsync(string? primaryLanguage, bool scored)
    {
        var repository = new Repository
        {
            GitHubId = _nextGitHubId++,
            Owner = "octocat",
            Name = $"repo-{_nextGitHubId}",
            Url = $"https://github.com/octocat/repo-{_nextGitHubId}",
            DefaultBranch = "main",
            PrimaryLanguage = primaryLanguage,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FirstDiscoveredAtUtc = DateTimeOffset.UtcNow,
        };
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        if (scored)
        {
            _dbContext.Scores.Add(new Score
            {
                RepositoryId = repository.Id,
                TotalScore = 50,
                ComputedAtUtc = DateTimeOffset.UtcNow,
            });
            await _dbContext.SaveChangesAsync();
        }

        return repository;
    }

    [Fact]
    public async Task Handle_NoScoredRepositories_ReturnsEmptyList()
    {
        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task Handle_ReturnsOneEntryPerDistinctLanguage_SortedOrdinally()
    {
        await AddRepositoryAsync("Go", scored: true);
        await AddRepositoryAsync("C#", scored: true);
        // A second C# repo proves distinctness, not just "one row per repository".
        await AddRepositoryAsync("C#", scored: true);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        Assert.Equal(["C#", "Go"], result.Categories.Select(c => c.Category));
    }

    [Fact]
    public async Task Handle_UnscoredRepository_Excluded()
    {
        // Matches GetHiddenGemsQueryHandler's own Scores.Any() eligibility - a language with no
        // scored repo behind it can never actually return a Hidden Gems result, so it must not
        // appear as a selectable filter option.
        await AddRepositoryAsync("Rust", scored: false);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task Handle_NullPrimaryLanguage_Excluded()
    {
        await AddRepositoryAsync(null, scored: true);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        Assert.Empty(result.Categories);
    }
}
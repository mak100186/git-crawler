using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Categories.GetCategories;
using GitCrawler.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Categories.GetCategories;

public class GetCategoriesQueryHandlerTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private long _nextGitHubId = 1;

    private GetCategoriesQueryHandler CreateHandler() => new(DbContext);

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
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        if (scored)
        {
            DbContext.Scores.Add(new Score
            {
                RepositoryId = repository.Id,
                TotalScore = 50,
                ComputedAtUtc = DateTimeOffset.UtcNow,
            });
            await DbContext.SaveChangesAsync();
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
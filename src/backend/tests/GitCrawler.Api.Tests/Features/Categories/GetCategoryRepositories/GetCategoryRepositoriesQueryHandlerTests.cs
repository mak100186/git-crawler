using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Categories.GetCategoryRepositories;
using GitCrawler.Api.Features.Repositories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Categories.GetCategoryRepositories;

public class GetCategoryRepositoriesQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;

    public GetCategoryRepositoriesQueryHandlerTests()
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

    private GetCategoryRepositoriesQueryHandler CreateHandler() => new(_dbContext);

    private async Task<Repository> AddRepositoryAsync(
        long gitHubId, string? language, int stars = 10, string? license = "MIT", List<string>? topics = null, string name = "repo")
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
            LicenseIdentifier = license,
            LicenseName = license,
            Topics = topics ?? [],
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FirstDiscoveredAtUtc = DateTimeOffset.UtcNow,
        };
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    [Fact]
    public async Task Handle_ScopesToTheGivenCategoryOnly()
    {
        var matching = await AddRepositoryAsync(1, "C#");
        await AddRepositoryAsync(2, "Go");

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoryRepositoriesQuery("C#", new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal([matching.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_IgnoresCallerSuppliedLanguageFilter_UsesRouteCategoryInstead()
    {
        var repository = await AddRepositoryAsync(1, "C#");

        var handler = CreateHandler();
        // Even if a caller somehow supplies a conflicting Language facet, the route category wins -
        // this drill-down is scoped to exactly one category (F-010 D2).
        var result = await handler.HandleAsync(
            new GetCategoryRepositoriesQuery("C#", new RepositoryFilterCriteria(Language: ["Go"])), CancellationToken.None);

        Assert.Equal([repository.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_AppliesD4FilterContractWithinTheCategory()
    {
        var matches = await AddRepositoryAsync(1, "C#", stars: 20, license: "MIT", topics: ["cli"]);
        await AddRepositoryAsync(2, "C#", stars: 1, license: "MIT", topics: ["cli"]); // wrong stars
        await AddRepositoryAsync(3, "C#", stars: 20, license: "Apache-2.0", topics: ["cli"]); // wrong license

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetCategoryRepositoriesQuery("C#", new RepositoryFilterCriteria(MinStars: 10, Topic: ["cli"], License: ["MIT"])),
            CancellationToken.None);

        Assert.Equal([matches.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_NoFilters_DefaultsToNewestDesc()
    {
        var older = await AddRepositoryAsync(1, "C#");
        older.FirstDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-2);
        var newer = await AddRepositoryAsync(2, "C#");
        newer.FirstDiscoveredAtUtc = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoryRepositoriesQuery("C#", new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_EmptyFilterResult_ReturnsEmptyNotError()
    {
        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoryRepositoriesQuery("Rust", new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Empty(result.Items);
    }
}
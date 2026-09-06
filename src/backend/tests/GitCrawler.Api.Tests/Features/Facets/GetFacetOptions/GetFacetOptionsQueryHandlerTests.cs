using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Facets.GetFacetOptions;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Facets.GetFacetOptions;

// Same SQLite-backed DbContext approach as GetCategoriesQueryHandlerTests: this handler's whole
// point is that Distinct() runs server-side, so a provider that actually translates the query is
// the only thing that proves it.
public class GetFacetOptionsQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;
    private long _nextGitHubId = 1;

    public GetFacetOptionsQueryHandlerTests()
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

    private GetFacetOptionsQueryHandler CreateHandler() => new(_dbContext);

    private async Task AddRepositoryAsync(string? licenseIdentifier, string[] topics, bool scored)
    {
        var repository = new Repository
        {
            GitHubId = _nextGitHubId++,
            Owner = "octocat",
            Name = $"repo-{_nextGitHubId}",
            Url = $"https://github.com/octocat/repo-{_nextGitHubId}",
            DefaultBranch = "main",
            LicenseIdentifier = licenseIdentifier,
            Topics = [.. topics],
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
    }

    [Fact]
    public async Task Handle_NoScoredRepositories_ReturnsEmptyLists()
    {
        var result = await CreateHandler().HandleAsync(new GetFacetOptionsQuery(), CancellationToken.None);

        Assert.Empty(result.Licenses);
        Assert.Empty(result.Topics);
    }

    [Fact]
    public async Task Handle_ReturnsOneEntryPerDistinctLicense_SortedOrdinally()
    {
        await AddRepositoryAsync("MIT", [], scored: true);
        await AddRepositoryAsync("Apache-2.0", [], scored: true);
        // A second MIT repo proves distinctness, not just "one row per repository".
        await AddRepositoryAsync("MIT", [], scored: true);

        var result = await CreateHandler().HandleAsync(new GetFacetOptionsQuery(), CancellationToken.None);

        Assert.Equal(["Apache-2.0", "MIT"], result.Licenses);
    }

    [Fact]
    public async Task Handle_FlattensTopicsAcrossRepositories_DistinctAndSortedOrdinally()
    {
        await AddRepositoryAsync("MIT", ["rust", "cli"], scored: true);
        await AddRepositoryAsync("MIT", ["cli", "async"], scored: true);

        var result = await CreateHandler().HandleAsync(new GetFacetOptionsQuery(), CancellationToken.None);

        // "cli" appears on both repositories and must appear once. This also pins the SelectMany
        // translation over the Topics primitive collection - the one part of this handler that
        // depends on provider behaviour rather than plain LINQ.
        Assert.Equal(["async", "cli", "rust"], result.Topics);
    }

    [Fact]
    public async Task Handle_UnscoredRepository_ContributesNeitherLicenseNorTopics()
    {
        // Matches GetHiddenGems' own eligibility filter: an option that could never return a result
        // does not belong in the filter list.
        await AddRepositoryAsync("MIT", ["visible"], scored: true);
        await AddRepositoryAsync("GPL-3.0", ["hidden"], scored: false);

        var result = await CreateHandler().HandleAsync(new GetFacetOptionsQuery(), CancellationToken.None);

        Assert.Equal(["MIT"], result.Licenses);
        Assert.Equal(["visible"], result.Topics);
    }

    [Fact]
    public async Task Handle_RepositoryWithNoLicenseOrTopics_IsSkippedWithoutNulls()
    {
        await AddRepositoryAsync(licenseIdentifier: null, topics: [], scored: true);
        await AddRepositoryAsync("MIT", ["cli"], scored: true);

        var result = await CreateHandler().HandleAsync(new GetFacetOptionsQuery(), CancellationToken.None);

        Assert.Equal(["MIT"], result.Licenses);
        Assert.Equal(["cli"], result.Topics);
    }
}
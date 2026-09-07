using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Facets.GetFacetOptions;
using GitCrawler.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Facets.GetFacetOptions;

// Runs against the shared PostgreSQL container (see PostgresFixture). This handler's whole point is
// that Distinct() runs server-side, and its topic flattening needs unnest() - which the suite's
// previous SQLite provider could not translate at all, so this is the only setup that proves it.
public class GetFacetOptionsQueryHandlerTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private long _nextGitHubId = 1;

    private GetFacetOptionsQueryHandler CreateHandler() => new(DbContext);

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
using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Crawling.DiscoverRepositories;
using GitCrawler.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitCrawler.Api.Tests.Features.Crawling.DiscoverRepositories;

// Runs against the shared PostgreSQL container (see PostgresFixture) so the upsert tests exercise
// the real unique-index-on-GitHubId constraint, not an in-memory approximation of it.
public class DiscoverRepositoriesCommandHandlerTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly FakeGitHubDiscoveryClient _discoveryClient = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

    private DiscoverRepositoriesCommandHandler CreateHandler() =>
        new(DbContext, _discoveryClient, NullLogger<DiscoverRepositoriesCommandHandler>.Instance, _timeProvider);

    private static DiscoveredRepository NewDiscoveredRepo(
        long gitHubId,
        string name = "hello-world",
        string? licenseId = "MIT",
        string? licenseName = "MIT License",
        IReadOnlyList<string>? topics = null) =>
        new(
            gitHubId,
            "octocat",
            name,
            $"https://github.com/octocat/{name}",
            "main",
            "C#",
            10,
            2,
            licenseId,
            licenseName,
            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            42,
            topics ?? []);

    [Fact]
    public async Task Handle_NewGitHubId_InsertsNewRepository()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(123)]));
        _discoveryClient.EnqueueContributorCount(4);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(1, await DbContext.Repositories.CountAsync());
        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 123);
        Assert.Equal("octocat", stored.Owner);
        Assert.Equal("hello-world", stored.Name);
        Assert.Equal(4, stored.ContributorCount);
        Assert.Equal(42, stored.CommitCount);
        Assert.Equal(1, result.DiscoveredCount);
        Assert.Equal(1, result.UpsertedCount);
    }

    [Fact]
    public async Task Handle_NewGitHubId_SetsFirstDiscoveredAtUtcToNow()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(321)]));
        _discoveryClient.EnqueueContributorCount(1);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 321);
        Assert.Equal(_timeProvider.GetUtcNow(), stored.FirstDiscoveredAtUtc);
    }

    [Fact]
    public async Task Handle_ExistingGitHubId_NeverOverwritesFirstDiscoveredAtUtc()
    {
        var originalFirstDiscovered = _timeProvider.GetUtcNow().AddDays(-30);
        DbContext.Repositories.Add(new Repository
        {
            GitHubId = 42,
            Owner = "octocat",
            Name = "old-name",
            Url = "https://github.com/octocat/old-name",
            DefaultBranch = "main",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddYears(-2),
            FirstDiscoveredAtUtc = originalFirstDiscovered,
            ContributorCountFetchedAtUtc = _timeProvider.GetUtcNow().AddDays(-1),
        });
        await DbContext.SaveChangesAsync();

        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(42)]));

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        // A re-crawl must not touch FirstDiscoveredAtUtc (F-010 D1) - otherwise a frequently
        // re-crawled old repo would look newer than a genuinely new discovery under "Newest" sort.
        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 42);
        Assert.Equal(originalFirstDiscovered, stored.FirstDiscoveredAtUtc);
    }

    [Fact]
    public async Task Handle_DiscoveredRepositoryWithTopics_CapturesTopicsOnInsertAndRefreshesOnRecrawl()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(500, topics: ["cli", "dotnet"])]));
        _discoveryClient.EnqueueContributorCount(1);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 500);
        Assert.Equal(["cli", "dotnet"], stored.Topics);

        // Topics is refreshed every crawl (unlike FirstDiscoveredAtUtc) - a repo's topic list can
        // legitimately change over time.
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(500, topics: ["cli"])]));
        _discoveryClient.EnqueueContributorCount(1);
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 500);
        Assert.Equal(["cli"], stored.Topics);
    }

    [Fact]
    public async Task Handle_DiscoveredRepositoryWithNoTopics_StoresEmptyList()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(600)]));
        _discoveryClient.EnqueueContributorCount(1);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 600);
        Assert.Empty(stored.Topics);
    }

    [Fact]
    public async Task Handle_ExistingGitHubId_UpdatesExistingRow_DoesNotDuplicate()
    {
        DbContext.Repositories.Add(new Repository
        {
            GitHubId = 42,
            Owner = "octocat",
            Name = "old-name",
            Url = "https://github.com/octocat/old-name",
            DefaultBranch = "main",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddYears(-2),
            StarCount = 1,
            // Stale on purpose so this test also implicitly exercises the "refetch when stale" path.
            ContributorCountFetchedAtUtc = _timeProvider.GetUtcNow().AddDays(-30),
        });
        await DbContext.SaveChangesAsync();

        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(42, name: "new-name")]));
        _discoveryClient.EnqueueContributorCount(9);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        // Re-crawling an already-known GitHubId must update, not duplicate (F-001 spike finding;
        // GitCrawlerDbContext enforces this at the schema level via a unique index on GitHubId).
        Assert.Equal(1, await DbContext.Repositories.CountAsync(r => r.GitHubId == 42));
        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 42);
        Assert.Equal("new-name", stored.Name);
        Assert.Equal(10, stored.StarCount);
        Assert.Equal(9, stored.ContributorCount);
    }

    [Fact]
    public async Task Handle_EmptyDiscoveryResult_CompletesWithoutError()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, []));

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(0, result.DiscoveredCount);
        Assert.Equal(0, result.UpsertedCount);
        Assert.Equal(0, await DbContext.Repositories.CountAsync());
        Assert.Equal(0, _discoveryClient.GetContributorCountCallCount);
    }

    [Fact]
    public async Task Handle_RepositoryWithNoLicense_StoresNullNotPlaceholder()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(7, licenseId: null, licenseName: null)]));
        _discoveryClient.EnqueueContributorCount(1);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 7);
        Assert.Null(stored.LicenseIdentifier);
        Assert.Null(stored.LicenseName);
    }

    [Fact]
    public async Task Handle_GraphQlRateLimited_WaitsUntilResetThenRetries()
    {
        var resetAt = _timeProvider.GetUtcNow().AddMinutes(10);
        _discoveryClient.EnqueueDiscoveryFailure(new GitHubGraphQlRateLimitExceededException(resetAt));
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, []));

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        // The exception did not propagate uncaught (Task Packet's explicit requirement), the
        // handler retried, and the wait it requested matches the resetAt the fake surfaced -
        // proving it actually reads the reset-time signal rather than a hardcoded/generic wait.
        Assert.Equal(0, result.DiscoveredCount);
        Assert.Equal(2, _discoveryClient.DiscoverRepositoriesCallCount);
        Assert.Single(_timeProvider.RequestedDelays);
        Assert.Equal(TimeSpan.FromMinutes(10), _timeProvider.RequestedDelays[0]);
    }

    [Fact]
    public async Task Handle_RestRateLimited_WaitsUntilResetThenRetries()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(55)]));
        var resetAt = _timeProvider.GetUtcNow().AddMinutes(5);
        _discoveryClient.EnqueueContributorFailure(new GitHubRestRateLimitExceededException(resetAt));
        _discoveryClient.EnqueueContributorCount(3);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(2, _discoveryClient.GetContributorCountCallCount);
        Assert.Contains(TimeSpan.FromMinutes(5), _timeProvider.RequestedDelays);

        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 55);
        Assert.Equal(3, stored.ContributorCount);
    }

    [Fact]
    public async Task Handle_TransientFailureWithoutRateLimitSignal_RetriesTwiceWithFlatOneMinuteGap()
    {
        // No rate-limit signal at all (F-001 spike §6's "if absent" case) - ADR-018 recovers via a
        // flat 1-minute gap for up to 2 retries, not propagating the exception.
        _discoveryClient.EnqueueDiscoveryFailure(new HttpRequestException("network blip"));
        _discoveryClient.EnqueueDiscoveryFailure(new HttpRequestException("network blip"));
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, []));

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(3, _discoveryClient.DiscoverRepositoriesCallCount);
        Assert.Equal(2, _timeProvider.RequestedDelays.Count);
        Assert.Equal(TimeSpan.FromMinutes(1), _timeProvider.RequestedDelays[0]);
        Assert.Equal(TimeSpan.FromMinutes(1), _timeProvider.RequestedDelays[1]);
    }

    [Fact]
    public async Task Handle_TransientFailureExceedsMaxRetries_AbortsRun()
    {
        // Three consecutive failures with no rate-limit signal - one more than ADR-018's 2-retry
        // cap - must abort the run rather than retry forever.
        for (var i = 0; i < 3; i++)
        {
            _discoveryClient.EnqueueDiscoveryFailure(new HttpRequestException("persistent failure"));
        }

        var handler = CreateHandler();

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ContributorListUnavailable_SkipsWithoutRetry_StampsFetchedAtSoItDoesNotRetryForever()
    {
        // GitHub's permanent "history/contributor list too large" 403 (ADR-018, observed live for
        // torvalds/linux) must not go through either retry pathway - one call, one failure, move on.
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(200)]));
        _discoveryClient.EnqueueContributorFailure(new GitHubContributorListUnavailableException("octocat", "hello-world"));

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(1, _discoveryClient.GetContributorCountCallCount);
        Assert.Empty(_timeProvider.RequestedDelays);
        Assert.Equal(1, result.ContributorCountSkipped);
        Assert.Equal(0, result.ContributorCountFetches);

        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 200);
        Assert.Null(stored.ContributorCount);
        Assert.Equal(_timeProvider.GetUtcNow(), stored.ContributorCountFetchedAtUtc);
    }

    [Fact]
    public async Task Handle_ContributorCountFetchedRecently_SkipsRestCall()
    {
        DbContext.Repositories.Add(new Repository
        {
            GitHubId = 88,
            Owner = "octocat",
            Name = "hello-world",
            Url = "https://github.com/octocat/hello-world",
            DefaultBranch = "main",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ContributorCount = 12,
            // Well within the 7-day freshness window (F-001 spike §7 caching cadence).
            ContributorCountFetchedAtUtc = _timeProvider.GetUtcNow().AddDays(-1),
        });
        await DbContext.SaveChangesAsync();

        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(88)]));

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(0, _discoveryClient.GetContributorCountCallCount);
        Assert.Equal(1, result.ContributorCountSkipped);
        Assert.Equal(0, result.ContributorCountFetches);
        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 88);
        Assert.Equal(12, stored.ContributorCount);
    }

    [Fact]
    public async Task Handle_ContributorCountNeverFetched_FetchesViaRest()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(99)]));
        _discoveryClient.EnqueueContributorCount(6);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(1, _discoveryClient.GetContributorCountCallCount);
        Assert.Equal(1, result.ContributorCountFetches);
        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 99);
        Assert.Equal(6, stored.ContributorCount);
    }

    [Fact]
    public async Task Handle_ContributorCountStale_RefetchesViaRest()
    {
        DbContext.Repositories.Add(new Repository
        {
            GitHubId = 100,
            Owner = "octocat",
            Name = "hello-world",
            Url = "https://github.com/octocat/hello-world",
            DefaultBranch = "main",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ContributorCount = 1,
            // Older than the 7-day freshness window.
            ContributorCountFetchedAtUtc = _timeProvider.GetUtcNow().AddDays(-8),
        });
        await DbContext.SaveChangesAsync();

        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(100)]));
        _discoveryClient.EnqueueContributorCount(50);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(1, _discoveryClient.GetContributorCountCallCount);
        var stored = await DbContext.Repositories.SingleAsync(r => r.GitHubId == 100);
        Assert.Equal(50, stored.ContributorCount);
    }
}
using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Crawling.DiscoverRepositories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitCrawler.Api.Tests.Features.Crawling.DiscoverRepositories;

// Same SQLite-backed DbContext approach as GitCrawlerDbContextTests (not the EF Core InMemory
// provider) so the upsert tests exercise the real unique-index-on-GitHubId constraint, not a
// weaker in-memory approximation of it.
public class DiscoverRepositoriesCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;
    private readonly FakeGitHubDiscoveryClient _discoveryClient = new();
    private readonly FakeRetryDelay _retryDelay = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

    public DiscoverRepositoriesCommandHandlerTests()
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

    private DiscoverRepositoriesCommandHandler CreateHandler() =>
        new(_dbContext, _discoveryClient, NullLogger<DiscoverRepositoriesCommandHandler>.Instance, _timeProvider, _retryDelay);

    private static DiscoveredRepository NewDiscoveredRepo(
        long gitHubId,
        string name = "hello-world",
        string? licenseId = "MIT",
        string? licenseName = "MIT License") =>
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
            42);

    [Fact]
    public async Task Handle_NewGitHubId_InsertsNewRepository()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(123)]));
        _discoveryClient.EnqueueContributorCount(4);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(1, await _dbContext.Repositories.CountAsync());
        var stored = await _dbContext.Repositories.SingleAsync(r => r.GitHubId == 123);
        Assert.Equal("octocat", stored.Owner);
        Assert.Equal("hello-world", stored.Name);
        Assert.Equal(4, stored.ContributorCount);
        Assert.Equal(42, stored.CommitCount);
        Assert.Equal(1, result.DiscoveredCount);
        Assert.Equal(1, result.UpsertedCount);
    }

    [Fact]
    public async Task Handle_ExistingGitHubId_UpdatesExistingRow_DoesNotDuplicate()
    {
        _dbContext.Repositories.Add(new Repository
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
        await _dbContext.SaveChangesAsync();

        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(42, name: "new-name")]));
        _discoveryClient.EnqueueContributorCount(9);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        // Re-crawling an already-known GitHubId must update, not duplicate (F-001 spike finding;
        // GitCrawlerDbContext enforces this at the schema level via a unique index on GitHubId).
        Assert.Equal(1, await _dbContext.Repositories.CountAsync(r => r.GitHubId == 42));
        var stored = await _dbContext.Repositories.SingleAsync(r => r.GitHubId == 42);
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
        Assert.Equal(0, await _dbContext.Repositories.CountAsync());
        Assert.Equal(0, _discoveryClient.GetContributorCountCallCount);
    }

    [Fact]
    public async Task Handle_RepositoryWithNoLicense_StoresNullNotPlaceholder()
    {
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(7, licenseId: null, licenseName: null)]));
        _discoveryClient.EnqueueContributorCount(1);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        var stored = await _dbContext.Repositories.SingleAsync(r => r.GitHubId == 7);
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
        Assert.Single(_retryDelay.RequestedDelays);
        Assert.Equal(TimeSpan.FromMinutes(10), _retryDelay.RequestedDelays[0]);
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
        Assert.Contains(TimeSpan.FromMinutes(5), _retryDelay.RequestedDelays);

        var stored = await _dbContext.Repositories.SingleAsync(r => r.GitHubId == 55);
        Assert.Equal(3, stored.ContributorCount);
    }

    [Fact]
    public async Task Handle_TransientFailureWithoutRateLimitSignal_AppliesExponentialBackoff()
    {
        // No rate-limit signal at all (F-001 spike §6's "if absent" case) - the handler must still
        // recover via exponential backoff (60s, then 120s), not propagate the exception.
        _discoveryClient.EnqueueDiscoveryFailure(new HttpRequestException("network blip"));
        _discoveryClient.EnqueueDiscoveryFailure(new HttpRequestException("network blip"));
        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, []));

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(3, _discoveryClient.DiscoverRepositoriesCallCount);
        Assert.Equal(2, _retryDelay.RequestedDelays.Count);
        Assert.Equal(TimeSpan.FromSeconds(60), _retryDelay.RequestedDelays[0]);
        Assert.Equal(TimeSpan.FromSeconds(120), _retryDelay.RequestedDelays[1]);
    }

    [Fact]
    public async Task Handle_TransientFailureExceedsMaxRetries_AbortsRun()
    {
        // Six consecutive failures with no rate-limit signal - one more than the 5-retry cap
        // (F-001 spike §6.5) - must abort the run rather than retry forever.
        for (var i = 0; i < 6; i++)
        {
            _discoveryClient.EnqueueDiscoveryFailure(new HttpRequestException("persistent failure"));
        }

        var handler = CreateHandler();

        await Assert.ThrowsAsync<HttpRequestException>(() => handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_ContributorCountFetchedRecently_SkipsRestCall()
    {
        _dbContext.Repositories.Add(new Repository
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
        await _dbContext.SaveChangesAsync();

        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(88)]));

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(0, _discoveryClient.GetContributorCountCallCount);
        Assert.Equal(1, result.ContributorCountSkipped);
        Assert.Equal(0, result.ContributorCountFetches);
        var stored = await _dbContext.Repositories.SingleAsync(r => r.GitHubId == 88);
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
        var stored = await _dbContext.Repositories.SingleAsync(r => r.GitHubId == 99);
        Assert.Equal(6, stored.ContributorCount);
    }

    [Fact]
    public async Task Handle_ContributorCountStale_RefetchesViaRest()
    {
        _dbContext.Repositories.Add(new Repository
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
        await _dbContext.SaveChangesAsync();

        _discoveryClient.EnqueueDiscoveryPage(new DiscoveryPage(false, null, [NewDiscoveredRepo(100)]));
        _discoveryClient.EnqueueContributorCount(50);

        var handler = CreateHandler();
        await handler.HandleAsync(new DiscoverRepositoriesCommand(), CancellationToken.None);

        Assert.Equal(1, _discoveryClient.GetContributorCountCallCount);
        var stored = await _dbContext.Repositories.SingleAsync(r => r.GitHubId == 100);
        Assert.Equal(50, stored.ContributorCount);
    }
}
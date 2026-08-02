using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Crawling.DiscoverRepositories;
using GitCrawler.Api.Features.Summarization.GenerateSummaries;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitCrawler.Api.Tests.Features.Summarization.GenerateSummaries;

// Same SQLite-backed DbContext approach as ComputeScoresCommandHandlerTests /
// DiscoverRepositoriesCommandHandlerTests (not the EF Core InMemory provider) so the
// Repository/Score/Summary navigation this handler relies on behaves like the real relational
// provider. The GitHub README fetch is faked at the HttpClientFactory/HttpMessageHandler level
// (see Fakes.cs) rather than behind a bespoke interface, since GenerateSummariesCommandHandler
// itself calls IHttpClientFactory directly (Task Packet's explicit "reuse the existing named
// HttpClient" instruction, not a new abstraction).
public class GenerateSummariesCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;
    private readonly FakeRepositorySummarizer _summarizer = new();
    private readonly FakeHttpClientFactory _httpClientFactory = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

    public GenerateSummariesCommandHandlerTests()
    {
        // The in-memory SQLite database is destroyed the moment its last connection closes, so
        // this connection must stay open for the test's lifetime rather than being opened per call.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GitCrawlerDbContext>().UseSqlite(_connection).Options;
        _dbContext = new GitCrawlerDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Default: every README fetch 404s ("no README" is the common, legitimate case per the
        // Task Packet) unless a test overrides this via UseReadmeResponder.
        UseReadmeResponder(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private void UseReadmeResponder(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new FakeHttpMessageHandler(respond);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.github.com/") };
        _httpClientFactory.RegisterClient(GitHubDiscoveryClient.GitHubRestClientName, client);
    }

    // Matches GitHub REST's actual GET /repos/{owner}/{repo}/readme shape closely enough for
    // GenerateSummariesCommandHandler's base64 decode path - only the "content" field is read.
    private static HttpResponseMessage ReadmeFoundResponse(string rawContent)
    {
        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawContent));
        var json = JsonSerializer.Serialize(new { content = base64, encoding = "base64" });
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private GenerateSummariesCommandHandler CreateHandler(IConfiguration? configuration = null) =>
        new(_dbContext, _summarizer, _httpClientFactory, configuration ?? new ConfigurationBuilder().Build(), NullLogger<GenerateSummariesCommandHandler>.Instance, _timeProvider);

    private static IConfiguration ConfigWith(double? minimumScore = null, int? batchSize = null)
    {
        var data = new Dictionary<string, string?>();
        if (minimumScore is not null)
        {
            data["Summarization:MinimumScore"] = minimumScore.Value.ToString(CultureInfo.InvariantCulture);
        }

        if (batchSize is not null)
        {
            data["Summarization:BatchSize"] = batchSize.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private Repository NewRepository(long gitHubId, string owner = "octocat") => new()
    {
        GitHubId = gitHubId,
        Owner = owner,
        Name = $"repo-{gitHubId}",
        Url = $"https://github.com/{owner}/repo-{gitHubId}",
        DefaultBranch = "main",
        PrimaryLanguage = "C#",
        LicenseName = "MIT License",
        CreatedAtUtc = _timeProvider.GetUtcNow().AddYears(-1),
    };

    private Score NewScore(int repositoryId, double totalScore, DateTimeOffset? computedAtUtc = null) => new()
    {
        RepositoryId = repositoryId,
        HasLicense = true,
        LicenseType = "MIT",
        CommitsPerWeek = 1,
        ContributorCount = 1,
        ForkCount = 1,
        StarCount = 1,
        TotalScore = totalScore,
        ComputedAtUtc = computedAtUtc ?? _timeProvider.GetUtcNow(),
    };

    [Fact]
    public async Task Handle_TopScoredRepositoryWithoutSummary_GeneratesAndPersistsSummary()
    {
        var repository = NewRepository(1);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repository.Id, 75));
        await _dbContext.SaveChangesAsync();

        _summarizer.EnqueueSummary("A concise summary.");

        var result = await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(1, result.SummarizedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);

        var stored = await _dbContext.Summaries.SingleAsync(s => s.RepositoryId == repository.Id);
        Assert.Equal("A concise summary.", stored.Content);
        Assert.Equal(_timeProvider.GetUtcNow(), stored.GeneratedAtUtc);

        var request = Assert.Single(_summarizer.Requests);
        Assert.Equal(repository.Owner, request.Owner);
        Assert.Equal(repository.Name, request.Name);
    }

    [Fact]
    public async Task Handle_RepositoryBelowMinimumScore_IsExcluded_NotSummarized()
    {
        var repository = NewRepository(2);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        // Default Summarization:MinimumScore is 40 (see GenerateSummariesCommandHandler) - well below it.
        _dbContext.Scores.Add(NewScore(repository.Id, 10));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(0, result.SummarizedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Empty(_summarizer.Requests);
        Assert.Equal(0, await _dbContext.Summaries.CountAsync());
    }

    [Fact]
    public async Task Handle_RepositoryAlreadyHasSummary_IsExcluded_NeverReSummarized()
    {
        var repository = NewRepository(3);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repository.Id, 90)); // well above threshold
        _dbContext.Summaries.Add(new Summary
        {
            RepositoryId = repository.Id,
            Content = "Existing summary.",
            GeneratedAtUtc = _timeProvider.GetUtcNow().AddDays(-1),
        });
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(0, result.SummarizedCount);
        Assert.Empty(_summarizer.Requests);
        // Unlike Score, a Summary is never regenerated once it exists (Task Packet's explicit
        // divergence from the Scoring Engine's re-scoring-on-recrawl behavior) - still exactly one
        // row for this repo.
        Assert.Equal(1, await _dbContext.Summaries.CountAsync(s => s.RepositoryId == repository.Id));
    }

    [Fact]
    public async Task Handle_ReadmeMissing404_SummarizesUsingAvailableMetadata_NotFatal()
    {
        var repository = NewRepository(4);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repository.Id, 50));
        await _dbContext.SaveChangesAsync();

        // Default responder (set up in the constructor) already 404s every README fetch.
        _summarizer.EnqueueSummary("Summary without a README.");

        var result = await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(1, result.SummarizedCount);
        Assert.Equal(0, result.FailedCount);
        var request = Assert.Single(_summarizer.Requests);
        Assert.Null(request.ReadmeContent);
        Assert.Equal(repository.PrimaryLanguage, request.PrimaryLanguage);
        Assert.Equal(repository.LicenseName, request.LicenseName);
    }

    [Fact]
    public async Task Handle_ReadmeFound_DecodesBase64Content_PassesToSummarizer()
    {
        var repository = NewRepository(5);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repository.Id, 50));
        await _dbContext.SaveChangesAsync();

        UseReadmeResponder(_ => ReadmeFoundResponse("# Hello World\nThis is a test repo."));
        _summarizer.EnqueueSummary("Summary with a README.");

        await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        var request = Assert.Single(_summarizer.Requests);
        Assert.Equal("# Hello World\nThis is a test repo.", request.ReadmeContent);
    }

    [Fact]
    public async Task Handle_SummarizerFailsForOneRepo_DoesNotAbortBatch_ContinuesWithRest()
    {
        var failingRepo = NewRepository(6);
        var succeedingRepo = NewRepository(7);
        _dbContext.Repositories.AddRange(failingRepo, succeedingRepo);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(failingRepo.Id, 90));
        _dbContext.Scores.Add(NewScore(succeedingRepo.Id, 80));
        await _dbContext.SaveChangesAsync();

        // Processed in descending-score order, so failingRepo (90) is attempted first.
        _summarizer.EnqueueFailure(new HttpRequestException("LM Studio unreachable"));
        _summarizer.EnqueueSummary("Second repo's summary.");

        var result = await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(1, result.SummarizedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(2, _summarizer.Requests.Count);

        // The failing repo still has no Summary row - it's picked up again on the next run rather
        // than aborting the whole batch or being permanently skipped.
        Assert.Equal(0, await _dbContext.Summaries.CountAsync(s => s.RepositoryId == failingRepo.Id));
        Assert.Equal(1, await _dbContext.Summaries.CountAsync(s => s.RepositoryId == succeedingRepo.Id));
    }

    [Fact]
    public async Task Handle_MultipleScoreRowsExist_UsesLatestScore_NotAnOlderOne()
    {
        var repository = NewRepository(8);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        // Older score is below threshold; the latest score (by ComputedAtUtc) is above it - only
        // the latest should determine eligibility, same "latest Score wins" rule
        // ComputeScoresCommandHandler applies for re-scoring.
        _dbContext.Scores.Add(NewScore(repository.Id, 10, _timeProvider.GetUtcNow().AddDays(-5)));
        _dbContext.Scores.Add(NewScore(repository.Id, 60, _timeProvider.GetUtcNow()));
        await _dbContext.SaveChangesAsync();

        _summarizer.EnqueueSummary("Summary.");

        var result = await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(1, result.SummarizedCount);
        Assert.Single(_summarizer.Requests);
    }

    [Fact]
    public async Task Handle_LatestScoreIsLowerThanAnEarlierHigherScore_UsesLatestByTime_NotHighestByValue()
    {
        var repository = NewRepository(9);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();

        // Earlier score is high (well above MinimumScore); the chronologically latest score is low
        // (below MinimumScore) - e.g. the repo went stale on a later re-crawl. "Latest" must mean
        // latest-by-ComputedAtUtc, not highest-by-TotalScore, or a repo that's no longer top-scored
        // would still get permanently summarized off its historical peak (Summary is create-once).
        _dbContext.Scores.Add(NewScore(repository.Id, 85, _timeProvider.GetUtcNow().AddDays(-5)));
        _dbContext.Scores.Add(NewScore(repository.Id, 25, _timeProvider.GetUtcNow()));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(0, result.SummarizedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Empty(_summarizer.Requests);
        Assert.Equal(0, await _dbContext.Summaries.CountAsync());
    }

    [Fact]
    public async Task Handle_NoRepositoriesNeedSummarization_CompletesWithoutError_NoOp()
    {
        var result = await CreateHandler().HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(0, result.SummarizedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(_summarizer.Requests);
    }

    [Fact]
    public async Task Handle_MoreEligibleRepositoriesThanBatchSize_CapsAtBatchSize()
    {
        for (var i = 0; i < 3; i++)
        {
            var repository = NewRepository(100 + i);
            _dbContext.Repositories.Add(repository);
            await _dbContext.SaveChangesAsync();
            _dbContext.Scores.Add(NewScore(repository.Id, 50 + i));
            await _dbContext.SaveChangesAsync();
        }

        _summarizer.EnqueueSummary("Summary A.");
        _summarizer.EnqueueSummary("Summary B.");

        var result = await CreateHandler(ConfigWith(batchSize: 2)).HandleAsync(new GenerateSummariesCommand(), CancellationToken.None);

        Assert.Equal(2, result.SummarizedCount);
        Assert.Equal(1, result.SkippedCount);
    }
}
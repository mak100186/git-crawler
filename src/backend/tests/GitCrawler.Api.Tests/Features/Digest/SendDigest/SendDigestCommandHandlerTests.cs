using System.Globalization;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Digest.SendDigest;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitCrawler.Api.Tests.Features.Digest.SendDigest;

// Same SQLite-backed DbContext approach as AggregateTrendsCommandHandlerTests/
// GenerateSummariesCommandHandlerTests (not the EF Core InMemory provider) so the
// Repository/Score/Summary/TrendAggregate navigation this handler relies on (via
// RepositoryCardQuery, shared with GetHiddenGems) behaves like the real relational provider.
public class SendDigestCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero));

    public SendDigestCommandHandlerTests()
    {
        // The in-memory SQLite database is destroyed the moment its last connection closes, so this
        // connection must stay open for the test's lifetime rather than being opened per call.
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

    private SendDigestCommandHandler CreateHandler(IConfiguration? configuration = null) =>
        new(_dbContext, _emailSender, configuration ?? ConfigWith(recipientEmail: "ops@example.com"), NullLogger<SendDigestCommandHandler>.Instance, _timeProvider);

    private static IConfiguration ConfigWith(string? recipientEmail = "ops@example.com", int? topN = null)
    {
        var data = new Dictionary<string, string?>
        {
            ["Digest:RecipientEmail"] = recipientEmail,
        };

        if (topN is not null)
        {
            data["Digest:TopN"] = topN.Value.ToString(CultureInfo.InvariantCulture);
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private Repository NewRepository(long gitHubId, string? primaryLanguage = "C#") => new()
    {
        GitHubId = gitHubId,
        Owner = "octocat",
        Name = $"repo-{gitHubId}",
        Url = $"https://github.com/octocat/repo-{gitHubId}",
        DefaultBranch = "main",
        PrimaryLanguage = primaryLanguage,
        CreatedAtUtc = _timeProvider.GetUtcNow().AddYears(-1),
        FirstDiscoveredAtUtc = _timeProvider.GetUtcNow().AddYears(-1),
    };

    private static Score NewScore(int repositoryId, double totalScore, DateTimeOffset computedAtUtc) => new()
    {
        RepositoryId = repositoryId,
        TotalScore = totalScore,
        ComputedAtUtc = computedAtUtc,
    };

    private Summary NewSummary(int repositoryId, string shortContent = "a short summary") => new()
    {
        RepositoryId = repositoryId,
        ShortContent = shortContent,
        DetailedContent = "a detailed summary",
        GeneratedAtUtc = _timeProvider.GetUtcNow(),
    };

    [Fact]
    public async Task Handle_EligibleRepositoriesAndTrends_ComposesDigest_AndSendsIt()
    {
        var repository = NewRepository(gitHubId: 1);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repository.Id, 82, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repository.Id, "A concise summary of octocat/repo-1."));
        _dbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = "C#",
            PeriodStart = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime),
            PeriodEnd = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime),
            RepositoryCount = 5,
            AverageScore = 61,
            CreatedAtUtc = _timeProvider.GetUtcNow(),
        });
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Equal(1, result.RepositoryCount);
        Assert.Equal(1, result.TrendCount);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Equal("ops@example.com", sent.To);
        Assert.Contains("octocat/repo-1", sent.Body);
        Assert.Contains("A concise summary of octocat/repo-1.", sent.Body);
        Assert.Contains("C#", sent.Body);
        Assert.Contains("5 repositories", sent.Body);
    }

    [Fact]
    public async Task Handle_RepositoryWhoseOnlyHighScoreIsHistorical_IsRankedByLatestScore_NotHistoricalPeak()
    {
        var staleRepo = NewRepository(gitHubId: 2);
        var freshRepo = NewRepository(gitHubId: 3);
        _dbContext.Repositories.AddRange(staleRepo, freshRepo);
        await _dbContext.SaveChangesAsync();

        // staleRepo's historical peak (90) is higher than freshRepo's only score (50), but
        // staleRepo's *latest* (chronologically most recent) score has since dropped to 20 - the
        // same distinction AggregateTrendsCommandHandlerTests/GenerateSummariesCommandHandlerTests
        // each draw for their own Score reads. With TopN capped at 1, only the repo with the higher
        // *latest* score should appear.
        _dbContext.Scores.Add(NewScore(staleRepo.Id, 90, _timeProvider.GetUtcNow().AddDays(-5)));
        _dbContext.Scores.Add(NewScore(staleRepo.Id, 20, _timeProvider.GetUtcNow()));
        _dbContext.Scores.Add(NewScore(freshRepo.Id, 50, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(staleRepo.Id, "Stale repo summary."));
        _dbContext.Summaries.Add(NewSummary(freshRepo.Id, "Fresh repo summary."));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler(ConfigWith(topN: 1)).HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.Equal(1, result.RepositoryCount);
        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("octocat/repo-3", sent.Body);
        Assert.DoesNotContain("octocat/repo-2", sent.Body);
    }

    [Fact]
    public async Task Handle_RepositoryScoredButNotYetSummarized_IsExcluded()
    {
        var repository = NewRepository(gitHubId: 4);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repository.Id, 90, _timeProvider.GetUtcNow()));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.Equal(0, result.RepositoryCount);
        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.DoesNotContain("octocat/repo-4", sent.Body);
    }

    [Fact]
    public async Task Handle_MoreEligibleRepositoriesThanTopN_CapsAtTopN()
    {
        for (var i = 0; i < 3; i++)
        {
            var repository = NewRepository(gitHubId: 10 + i);
            _dbContext.Repositories.Add(repository);
            await _dbContext.SaveChangesAsync();
            _dbContext.Scores.Add(NewScore(repository.Id, 50 + i, _timeProvider.GetUtcNow()));
            _dbContext.Summaries.Add(NewSummary(repository.Id));
            await _dbContext.SaveChangesAsync();
        }

        var result = await CreateHandler(ConfigWith(topN: 2)).HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.Equal(2, result.RepositoryCount);
    }

    [Fact]
    public async Task Handle_EmailSenderThrows_LogsFailure_ReturnsNotSent_DoesNotPropagate()
    {
        var repository = NewRepository(gitHubId: 5);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repository.Id, 70, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repository.Id));
        await _dbContext.SaveChangesAsync();

        _emailSender.FailNextSendWith(new InvalidOperationException("SMTP host unreachable"));

        // Must not throw - FR-006's "must not crash the host" constraint. The exception is caught
        // and logged inside the handler, not left to propagate out of HandleAsync.
        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(1, result.RepositoryCount);
        Assert.Empty(_emailSender.SentMessages);
    }

    [Fact]
    public async Task Handle_NoEligibleRepositoriesAndNoTrendData_StillSendsWellFormedEmptyDigest()
    {
        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Equal(0, result.RepositoryCount);
        Assert.Equal(0, result.TrendCount);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("No hidden gems to report today.", sent.Body);
        Assert.Contains("No trend data available for the current period.", sent.Body);
    }

    [Fact]
    public async Task Handle_TrendAggregateFromAnEarlierPeriod_IsExcluded_OnlyCurrentPeriodCounted()
    {
        _dbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = "C#",
            PeriodStart = DateOnly.FromDateTime(_timeProvider.GetUtcNow().AddDays(-2).UtcDateTime),
            PeriodEnd = DateOnly.FromDateTime(_timeProvider.GetUtcNow().AddDays(-2).UtcDateTime),
            RepositoryCount = 3,
            AverageScore = 40,
            CreatedAtUtc = _timeProvider.GetUtcNow().AddDays(-2),
        });
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.Equal(0, result.TrendCount);
        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("No trend data available for the current period.", sent.Body);
    }

    [Fact]
    public async Task Handle_RecipientEmailNotConfigured_SkipsSend_NoOp()
    {
        var repository = NewRepository(gitHubId: 6);
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        _dbContext.Scores.Add(NewScore(repository.Id, 90, _timeProvider.GetUtcNow()));
        _dbContext.Summaries.Add(NewSummary(repository.Id));
        await _dbContext.SaveChangesAsync();

        var result = await CreateHandler(ConfigWith(recipientEmail: string.Empty)).HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(0, result.RepositoryCount);
        Assert.Equal(0, result.TrendCount);
        Assert.Empty(_emailSender.SentMessages);
    }
}
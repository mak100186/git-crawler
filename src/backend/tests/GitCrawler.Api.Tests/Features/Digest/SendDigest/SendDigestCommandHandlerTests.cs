using System.Globalization;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Digest.SendDigest;
using GitCrawler.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GitCrawler.Api.Tests.Features.Digest.SendDigest;

// Runs against the shared PostgreSQL container (see PostgresFixture) so the
// Repository/Score/Summary/TrendAggregate navigation this handler relies on (via
// RepositoryCardQuery, shared with GetHiddenGems) behaves exactly as it does in production.
//
// Note for anyone adding tests here: this handler writes a DigestSendLog marker on a successful
// send and skips when one already exists for the day. PostgresTestBase's reset covers that table
// (it builds its TRUNCATE from the EF model), so each test starts with no marker.
public class SendDigestCommandHandlerTests : PostgresTestBase
{
    private readonly FakeEmailSender _emailSender = new();
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 4, 0, 0, 0, TimeSpan.Zero));

    public SendDigestCommandHandlerTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private SendDigestCommandHandler CreateHandler(IConfiguration? configuration = null) =>
        new(DbContext, _emailSender, configuration ?? ConfigWith(recipientEmail: "ops@example.com"), NullLogger<SendDigestCommandHandler>.Instance, _timeProvider);

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
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();
        DbContext.Scores.Add(NewScore(repository.Id, 82, _timeProvider.GetUtcNow()));
        DbContext.Summaries.Add(NewSummary(repository.Id, "A concise summary of octocat/repo-1."));
        DbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = "C#",
            PeriodStart = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime),
            PeriodEnd = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime),
            RepositoryCount = 5,
            AverageScore = 61,
            CreatedAtUtc = _timeProvider.GetUtcNow(),
        });
        await DbContext.SaveChangesAsync();

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
        DbContext.Repositories.AddRange(staleRepo, freshRepo);
        await DbContext.SaveChangesAsync();

        // staleRepo's historical peak (90) is higher than freshRepo's only score (50), but
        // staleRepo's *latest* (chronologically most recent) score has since dropped to 20 - the
        // same distinction AggregateTrendsCommandHandlerTests/GenerateSummariesCommandHandlerTests
        // each draw for their own Score reads. With TopN capped at 1, only the repo with the higher
        // *latest* score should appear.
        DbContext.Scores.Add(NewScore(staleRepo.Id, 90, _timeProvider.GetUtcNow().AddDays(-5)));
        DbContext.Scores.Add(NewScore(staleRepo.Id, 20, _timeProvider.GetUtcNow()));
        DbContext.Scores.Add(NewScore(freshRepo.Id, 50, _timeProvider.GetUtcNow()));
        DbContext.Summaries.Add(NewSummary(staleRepo.Id, "Stale repo summary."));
        DbContext.Summaries.Add(NewSummary(freshRepo.Id, "Fresh repo summary."));
        await DbContext.SaveChangesAsync();

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
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();
        DbContext.Scores.Add(NewScore(repository.Id, 90, _timeProvider.GetUtcNow()));
        await DbContext.SaveChangesAsync();

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
            DbContext.Repositories.Add(repository);
            await DbContext.SaveChangesAsync();
            DbContext.Scores.Add(NewScore(repository.Id, 50 + i, _timeProvider.GetUtcNow()));
            DbContext.Summaries.Add(NewSummary(repository.Id));
            await DbContext.SaveChangesAsync();
        }

        var result = await CreateHandler(ConfigWith(topN: 2)).HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.Equal(2, result.RepositoryCount);
    }

    [Fact]
    public async Task Handle_EmailSenderThrows_LogsFailure_ReportsSendFailure_DoesNotPropagate()
    {
        var repository = NewRepository(gitHubId: 5);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();
        DbContext.Scores.Add(NewScore(repository.Id, 70, _timeProvider.GetUtcNow()));
        DbContext.Summaries.Add(NewSummary(repository.Id));
        await DbContext.SaveChangesAsync();

        _emailSender.FailNextSendWith(new InvalidOperationException("SMTP host unreachable"));

        // Must not throw - FR-006's "must not crash the host" constraint. The exception is caught
        // and logged inside the handler, not left to propagate out of HandleAsync.
        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(1, result.RepositoryCount);
        Assert.Empty(_emailSender.SentMessages);

        // SendDigestJob rethrows on this, so the Hangfire job is marked Failed instead of the
        // Succeeded it used to report for a digest that never left the process.
        Assert.Equal("SMTP host unreachable", result.SendFailure);
    }

    [Fact]
    public async Task Handle_SkippedRatherThanFailed_ReportsNoSendFailure()
    {
        // A skip is a correct outcome, not a failure: SendFailure must stay null so SendDigestJob
        // leaves the Hangfire job Succeeded.
        var first = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);
        var second = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.True(first.Sent);
        Assert.Null(first.SendFailure);
        Assert.False(second.Sent);
        Assert.Null(second.SendFailure);
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
        DbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = "C#",
            PeriodStart = DateOnly.FromDateTime(_timeProvider.GetUtcNow().AddDays(-2).UtcDateTime),
            PeriodEnd = DateOnly.FromDateTime(_timeProvider.GetUtcNow().AddDays(-2).UtcDateTime),
            RepositoryCount = 3,
            AverageScore = 40,
            CreatedAtUtc = _timeProvider.GetUtcNow().AddDays(-2),
        });
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.Equal(0, result.TrendCount);
        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("No trend data available for the current period.", sent.Body);
    }

    [Fact]
    public async Task Handle_InvokedTwiceForSameDay_SendsOnlyOnce_SecondInvocationReportsNotSent()
    {
        // F-016/NFR-003: sequential-retry dedupe - simulates Hangfire re-invoking this command for
        // the same "today" after an earlier attempt already sent successfully (e.g. the process
        // died right after SendAsync but before the job was marked Succeeded). The
        // [DisableConcurrentExecution] guard (part A) does not cover this - it only prevents two
        // *simultaneous* executions - so this is exercised as two sequential HandleAsync calls
        // against the same handler/DbContext, same as a real sequential retry would be.
        var repository = NewRepository(gitHubId: 20);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();
        DbContext.Scores.Add(NewScore(repository.Id, 80, _timeProvider.GetUtcNow()));
        DbContext.Summaries.Add(NewSummary(repository.Id));
        await DbContext.SaveChangesAsync();

        var handler = CreateHandler();

        var firstResult = await handler.HandleAsync(new SendDigestCommand(), CancellationToken.None);
        Assert.True(firstResult.Sent);

        var secondResult = await handler.HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.False(secondResult.Sent);
        Assert.Equal(0, secondResult.RepositoryCount);
        Assert.Equal(0, secondResult.TrendCount);

        // Exactly one send across both invocations - the second call never reaches composition/
        // sending at all, it short-circuits on the persisted DigestSendLog marker instead.
        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("octocat/repo-20", sent.Body);

        Assert.Equal(1, await DbContext.DigestSendLogs.CountAsync());
    }

    [Fact]
    public async Task Handle_PriorDaySentMarker_DoesNotBlockTodaysSend()
    {
        // Edge case (Task Packet's explicit callout): yesterday's "sent" marker must not block
        // today's send - the dedupe check is scoped to the exact calendar day, not "has ever sent".
        DbContext.DigestSendLogs.Add(new DigestSendLog
        {
            SentForDate = DateOnly.FromDateTime(_timeProvider.GetUtcNow().AddDays(-1).UtcDateTime),
            SentAtUtc = _timeProvider.GetUtcNow().AddDays(-1),
        });
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.True(result.Sent);
        Assert.Single(_emailSender.SentMessages);
    }

    [Fact]
    public async Task Handle_RecipientEmailNotConfigured_SkipsSend_NoOp()
    {
        var repository = NewRepository(gitHubId: 6);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();
        DbContext.Scores.Add(NewScore(repository.Id, 90, _timeProvider.GetUtcNow()));
        DbContext.Summaries.Add(NewSummary(repository.Id));
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler(ConfigWith(recipientEmail: string.Empty)).HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.False(result.Sent);
        Assert.Equal(0, result.RepositoryCount);
        Assert.Equal(0, result.TrendCount);
        Assert.Empty(_emailSender.SentMessages);
    }

    private TrendAggregate NewTrendAggregate(string category, int repositoryCount, DateTimeOffset periodEnd) => new()
    {
        Category = category,
        PeriodStart = DateOnly.FromDateTime(periodEnd.UtcDateTime),
        PeriodEnd = DateOnly.FromDateTime(periodEnd.UtcDateTime),
        RepositoryCount = repositoryCount,
        AverageScore = 50,
        CreatedAtUtc = periodEnd,
    };

    [Fact]
    public async Task Handle_TrendGrewSincePreviousWeek_RendersUpwardGrowthPill()
    {
        // 4 repos a week ago, 6 today: (6-4)/4*100 = +50%.
        DbContext.TrendAggregates.Add(NewTrendAggregate("C#", 4, _timeProvider.GetUtcNow().AddDays(-7)));
        DbContext.TrendAggregates.Add(NewTrendAggregate("C#", 6, _timeProvider.GetUtcNow()));
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        Assert.Equal(1, result.TrendCount);
        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("▲ +50% this week", sent.Body);
    }

    [Fact]
    public async Task Handle_TrendShrankSincePreviousWeek_RendersDownwardGrowthPill()
    {
        // 10 repos a week ago, 6 today: (6-10)/10*100 = -40%.
        DbContext.TrendAggregates.Add(NewTrendAggregate("Go", 10, _timeProvider.GetUtcNow().AddDays(-7)));
        DbContext.TrendAggregates.Add(NewTrendAggregate("Go", 6, _timeProvider.GetUtcNow()));
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("▼ -40% this week", sent.Body);
    }

    [Fact]
    public async Task Handle_TrendHasNoPreviousBaselineAtAll_RendersNoChangePill_NotAMisleadingInfinitePercent()
    {
        // No TrendAggregate row at all before today for this category - must not divide by zero or
        // claim a fabricated growth figure for a category with no prior baseline to compare against.
        // A pill must still render (never a blank gap) - just a neutral "No change" one.
        DbContext.TrendAggregates.Add(NewTrendAggregate("Rust", 5, _timeProvider.GetUtcNow()));
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("Rust", sent.Body);
        Assert.Contains("No change", sent.Body);
        Assert.DoesNotContain("this week", sent.Body);
    }

    [Fact]
    public async Task Handle_TrendUnchangedSincePreviousWeek_RendersNoChangePill_FlatIsNotUpOrDown()
    {
        DbContext.TrendAggregates.Add(NewTrendAggregate("Python", 8, _timeProvider.GetUtcNow().AddDays(-7)));
        DbContext.TrendAggregates.Add(NewTrendAggregate("Python", 8, _timeProvider.GetUtcNow()));
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("No change", sent.Body);
        Assert.DoesNotContain("this week", sent.Body);
    }

    [Fact]
    public async Task Handle_BaselineYoungerThanAWeek_RendersNoChangePill_NotANoisyShortWindowPercent()
    {
        // Only 2 days of history so far (a brand-new pipeline/category) - a 4->6 swing over 2 days
        // is not meaningful "+50%" week-over-week growth, so this must render the same neutral
        // "No change" pill as having no baseline at all, not a computed percentage from too thin a
        // window.
        DbContext.TrendAggregates.Add(NewTrendAggregate("Zig", 4, _timeProvider.GetUtcNow().AddDays(-2)));
        DbContext.TrendAggregates.Add(NewTrendAggregate("Zig", 6, _timeProvider.GetUtcNow()));
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("No change", sent.Body);
        Assert.DoesNotContain("+50%", sent.Body);
        Assert.DoesNotContain("in 2d", sent.Body);
    }

    [Fact]
    public async Task Handle_NoExactSevenDayBaseline_FallsBackToClosestPriorRowOlderThanAWeek_LabeledWithRealElapsedDays()
    {
        // No row from exactly 7 days ago, but there is one from 10 days back - old enough to still
        // be a meaningful comparison, so unlike the under-a-week case this should render a real
        // computed percentage, just labeled with its actual elapsed days instead of "this week".
        // (4 -> 6 repos over 10 days = +50%.)
        DbContext.TrendAggregates.Add(NewTrendAggregate("Kotlin", 4, _timeProvider.GetUtcNow().AddDays(-10)));
        DbContext.TrendAggregates.Add(NewTrendAggregate("Kotlin", 6, _timeProvider.GetUtcNow()));
        await DbContext.SaveChangesAsync();

        var result = await CreateHandler().HandleAsync(new SendDigestCommand(), CancellationToken.None);

        var sent = Assert.Single(_emailSender.SentMessages);
        Assert.Contains("▲ +50% in 10d", sent.Body);
        Assert.DoesNotContain("this week", sent.Body);
    }

}
using System.Reflection;

using GitCrawler.Api.Features.Digest.SendDigest;

using Hangfire;

namespace GitCrawler.Api.Tests.Features.Digest.SendDigest;

// Covers the Hangfire-to-Wolverine glue class SendDigestJob. Unlike DiscoverRepositoriesJob/
// ComputeScoresJob/GenerateSummariesJob, SendDigestJob attaches no further continuation of its own
// (it's the terminal pipeline stage, deliberately scheduled independently rather than chained onto
// AggregateTrendsJob - see that class's own header comment for why), so there's no
// *ContinuationLink-shaped seam to fake here, mirroring AggregateTrendsJobTests' own equally simple
// coverage of AggregateTrendsJob.
public class SendDigestJobTests
{
    [Fact]
    public void RunAsync_IsDecoratedWithDisableConcurrentExecution()
    {
        // F-016/NFR-003: same reflection-based attribute-presence check as
        // DiscoverRepositoriesJobTests (see that file's own comment for why this is a static check
        // rather than exercising Hangfire's real distributed lock). The actual "never send the same
        // day's digest twice" guarantee comes from SendDigestCommandHandler's own DigestSendLog
        // marker (see SendDigestCommandHandlerTests' dedupe coverage) - this attribute only closes
        // the narrower simultaneous-overlap window, same as every other stage.
        var attribute = typeof(SendDigestJob)
            .GetMethod(nameof(SendDigestJob.RunAsync))!
            .GetCustomAttribute<DisableConcurrentExecutionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(60, attribute!.TimeoutSec);
    }

    [Fact]
    public async Task RunAsync_InvokesSendDigestCommandOnMessageBus()
    {
        var messageBus = new FakeMessageBus();
        var job = new SendDigestJob(messageBus);

        await job.RunAsync();

        var invoked = Assert.Single(messageBus.InvokedMessages);
        Assert.IsType<SendDigestCommand>(invoked);
    }

    [Fact]
    public async Task RunAsync_SendFailureReported_Throws_SoHangfireMarksTheJobFailed()
    {
        // The handler swallows SMTP exceptions by design (FR-006), which used to leave Hangfire
        // reporting Succeeded for a digest that never left the process. The job rethrows instead,
        // carrying the real SMTP message into the dashboard.
        var messageBus = new FakeMessageBus
        {
            NextResult = new SendDigestResult(Sent: false, RepositoryCount: 3, TrendCount: 1, SendFailure: "SMTP host unreachable"),
        };
        var job = new SendDigestJob(messageBus);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(job.RunAsync);

        Assert.Contains("SMTP host unreachable", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_SkippedSend_DoesNotThrow()
    {
        // A skip (no recipient configured, or already sent today) is a correct outcome, not a
        // failure - the job must stay Succeeded.
        var messageBus = new FakeMessageBus
        {
            NextResult = new SendDigestResult(Sent: false, RepositoryCount: 0, TrendCount: 0),
        };

        await new SendDigestJob(messageBus).RunAsync();
    }
}
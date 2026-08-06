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
}
using GitCrawler.Api.Features.Digest.SendDigest;

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
    public async Task RunAsync_InvokesSendDigestCommandOnMessageBus()
    {
        var messageBus = new FakeMessageBus();
        var job = new SendDigestJob(messageBus);

        await job.RunAsync();

        var invoked = Assert.Single(messageBus.InvokedMessages);
        Assert.IsType<SendDigestCommand>(invoked);
    }
}
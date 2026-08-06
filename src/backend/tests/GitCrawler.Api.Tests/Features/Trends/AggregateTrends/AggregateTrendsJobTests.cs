using System.Reflection;

using GitCrawler.Api.Features.Trends.AggregateTrends;

using Hangfire;

namespace GitCrawler.Api.Tests.Features.Trends.AggregateTrends;

// Covers the Hangfire-to-Wolverine glue class GenerateSummariesJob's own continuation link targets
// (see that class's header comment - Features/Summarization/GenerateSummaries/GenerateSummariesJob.cs).
// Like GenerateSummariesJob before F-009, this is now the last link in the chain with a built next
// stage (Crawler -> Scoring Engine -> Summarizer -> Trend Aggregator), so RunAsync takes no
// PerformContext and attaches no further continuation of its own - there's nothing to chain into
// yet, hence no ITrendsContinuationLink-shaped seam here. This test only proves the Wolverine
// invocation; the F-008->F-009 chain attachment itself (GenerateSummariesJob requesting this
// continuation) is covered by GenerateSummariesJobTests.cs
// (Features/Summarization/GenerateSummaries/GenerateSummariesJobTests.cs), since that's the class
// that owns it.
public class AggregateTrendsJobTests
{
    [Fact]
    public void RunAsync_IsDecoratedWithDisableConcurrentExecution()
    {
        // F-016/NFR-003: same reflection-based attribute-presence check as
        // DiscoverRepositoriesJobTests (see that file's own comment for why this is a static check
        // rather than exercising Hangfire's real distributed lock).
        var attribute = typeof(AggregateTrendsJob)
            .GetMethod(nameof(AggregateTrendsJob.RunAsync))!
            .GetCustomAttribute<DisableConcurrentExecutionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(30, attribute!.TimeoutSec);
    }

    [Fact]
    public async Task RunAsync_InvokesAggregateTrendsCommandOnMessageBus()
    {
        var messageBus = new FakeMessageBus();
        var job = new AggregateTrendsJob(messageBus);

        await job.RunAsync();

        var invoked = Assert.Single(messageBus.InvokedMessages);
        Assert.IsType<AggregateTrendsCommand>(invoked);
    }
}
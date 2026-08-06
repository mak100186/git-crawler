using System.Reflection;

using GitCrawler.Api.Features.Summarization.GenerateSummaries;
using GitCrawler.Api.Features.Trends.AggregateTrends;

using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;

namespace GitCrawler.Api.Tests.Features.Summarization.GenerateSummaries;

// Covers the Hangfire-to-Wolverine glue class ComputeScoresJob's own continuation link targets (see
// that class's header comment - Features/Scoring/ComputeScores/ComputeScoresJob.cs). This is the
// same shape as ComputeScoresJobTests.cs's own coverage of the F-007->F-008 attachment (see that
// file, Features/Scoring/ComputeScores/ComputeScoresJobTests.cs) - mirrored here for the F-008->F-009
// link, now that GenerateSummariesJob's RunAsync takes a PerformContext and attaches an
// ITrendsContinuationLink of its own (F-009). ITrendsContinuationLink is faked rather than exercising
// the real BackgroundJob.ContinueJobWith static call, which requires a live JobStorage.Current
// unavailable in a unit test (Task Packet's explicit testing constraint - see
// ITrendsContinuationLink's own comment); real end-to-end verification of the chaining behavior is
// left to the Integration Agent's live Postgres+Hangfire check.
public class GenerateSummariesJobTests
{
    [Fact]
    public void RunAsync_IsDecoratedWithDisableConcurrentExecution()
    {
        // F-016/NFR-003: same reflection-based attribute-presence check as
        // DiscoverRepositoriesJobTests (see that file's own comment for why this is a static check
        // rather than exercising Hangfire's real distributed lock). This is the one stage with two
        // real concurrent trigger paths (its own hourly RecurringJob and the chained continuation
        // from ComputeScoresJob), hence the longer documented timeout.
        var attribute = typeof(GenerateSummariesJob)
            .GetMethod(nameof(GenerateSummariesJob.RunAsync))!
            .GetCustomAttribute<DisableConcurrentExecutionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(30 * 60, attribute!.TimeoutSec);
    }

    [Fact]
    public async Task RunAsync_InvokesGenerateSummariesCommandOnMessageBus()
    {
        var messageBus = new FakeMessageBus();
        var job = new GenerateSummariesJob(messageBus, new AggregateTrendsJob(new FakeMessageBus()), new FakeTrendsContinuationLink());

        await job.RunAsync(CreatePerformContext("job-1"));

        var invoked = Assert.Single(messageBus.InvokedMessages);
        Assert.IsType<GenerateSummariesCommand>(invoked);
    }

    [Fact]
    public async Task RunAsync_AttachesTrendsContinuation_ForTheCompletedJobId()
    {
        var trendsJob = new AggregateTrendsJob(new FakeMessageBus());
        var continuationLink = new FakeTrendsContinuationLink();
        var job = new GenerateSummariesJob(new FakeMessageBus(), trendsJob, continuationLink);

        await job.RunAsync(CreatePerformContext("job-42"));

        // Proves the chain attaches to the specific job instance that just finished summarizing (not
        // a hardcoded id) and targets the actual AggregateTrendsJob this job was constructed with
        // (not some other instance).
        Assert.Equal("job-42", continuationLink.AttachedParentJobId);
        Assert.Same(trendsJob, continuationLink.AttachedTrendsJob);
    }

    // Same PerformContext-construction approach as DiscoverRepositoriesJobTests.cs/
    // ComputeScoresJobTests.cs's own helper - kept local rather than shared, mirroring the
    // vertical-slice convention applied to tests. See those files' own comments for why each fake
    // member below is shaped the way it is.
    private static PerformContext CreatePerformContext(string jobId) =>
        new(new FakeJobStorage(), new FakeStorageConnection(), new BackgroundJob(jobId, null!, DateTime.UtcNow), new NoOpJobCancellationToken());

    private sealed class NoOpJobCancellationToken : IJobCancellationToken
    {
        public CancellationToken ShutdownToken => CancellationToken.None;

        public void ThrowIfCancellationRequested()
        {
        }
    }

    private sealed class FakeJobStorage : JobStorage
    {
        public override IStorageConnection GetConnection() => throw new NotSupportedException();

        public override IMonitoringApi GetMonitoringApi() => throw new NotSupportedException();
    }

    private sealed class FakeStorageConnection : IStorageConnection
    {
        public IWriteOnlyTransaction CreateWriteTransaction() => throw new NotSupportedException();

        public IDisposable AcquireDistributedLock(string resource, TimeSpan timeout) => throw new NotSupportedException();

        public string CreateExpiredJob(Hangfire.Common.Job job, IDictionary<string, string> parameters, DateTime createdAt, TimeSpan expireIn) =>
            throw new NotSupportedException();

        public IFetchedJob FetchNextJob(string[] queues, CancellationToken cancellationToken) => throw new NotSupportedException();

        public void SetJobParameter(string id, string name, string value) => throw new NotSupportedException();

        public string GetJobParameter(string id, string name) => throw new NotSupportedException();

        public JobData GetJobData(string jobId) => throw new NotSupportedException();

        public StateData GetStateData(string jobId) => throw new NotSupportedException();

        public void AnnounceServer(string serverId, Hangfire.Server.ServerContext context) => throw new NotSupportedException();

        public void RemoveServer(string serverId) => throw new NotSupportedException();

        public void Heartbeat(string serverId) => throw new NotSupportedException();

        public int RemoveTimedOutServers(TimeSpan timeOut) => throw new NotSupportedException();

        public HashSet<string> GetAllItemsFromSet(string key) => throw new NotSupportedException();

        public string GetFirstByLowestScoreFromSet(string key, double fromScore, double toScore) => throw new NotSupportedException();

        public void SetRangeInHash(string key, IEnumerable<KeyValuePair<string, string>> keyValuePairs) => throw new NotSupportedException();

        public Dictionary<string, string> GetAllEntriesFromHash(string key) => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
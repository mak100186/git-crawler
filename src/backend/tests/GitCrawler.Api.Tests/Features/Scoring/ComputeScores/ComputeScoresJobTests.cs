using GitCrawler.Api.Features.Scoring.ComputeScores;
using GitCrawler.Api.Features.Summarization.GenerateSummaries;
using GitCrawler.Api.Features.Trends.AggregateTrends;

using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;

namespace GitCrawler.Api.Tests.Features.Scoring.ComputeScores;

// Covers the Hangfire-to-Wolverine glue class DiscoverRepositoriesJob's own continuation link
// targets - both that RunAsync invokes the Wolverine command (the actual scoring computation
// belongs to and is already covered by ComputeScoresCommandHandlerTests) and, as of F-008, that it
// requests the summarization continuation for the right job/target afterward. This is the same
// shape as DiscoverRepositoriesJobTests.cs's own coverage of the F-006->F-007 attachment (see that
// file, Features/Crawling/DiscoverRepositories/DiscoverRepositoriesJobTests.cs) - mirrored here for
// the F-007->F-008 link. ISummarizationContinuationLink is faked rather than exercising the real
// BackgroundJob.ContinueJobWith static call, which requires a live JobStorage.Current unavailable in
// a unit test (Task Packet's explicit testing constraint - see ISummarizationContinuationLink's own
// comment); real end-to-end verification of the chaining behavior is left to the Integration Agent's
// live Postgres+Hangfire check.
public class ComputeScoresJobTests
{
    [Fact]
    public async Task RunAsync_InvokesComputeScoresCommandOnMessageBus()
    {
        var messageBus = new FakeMessageBus();
        var job = new ComputeScoresJob(
            messageBus,
            new GenerateSummariesJob(new FakeMessageBus(), new AggregateTrendsJob(new FakeMessageBus()), new FakeTrendsContinuationLink()),
            new FakeSummarizationContinuationLink());

        await job.RunAsync(CreatePerformContext("job-1"));

        var invoked = Assert.Single(messageBus.InvokedMessages);
        Assert.IsType<ComputeScoresCommand>(invoked);
    }

    [Fact]
    public async Task RunAsync_AttachesSummarizationContinuation_ForTheCompletedJobId()
    {
        var summarizationJob = new GenerateSummariesJob(new FakeMessageBus(), new AggregateTrendsJob(new FakeMessageBus()), new FakeTrendsContinuationLink());
        var continuationLink = new FakeSummarizationContinuationLink();
        var job = new ComputeScoresJob(new FakeMessageBus(), summarizationJob, continuationLink);

        await job.RunAsync(CreatePerformContext("job-42"));

        // Proves the chain attaches to the specific job instance that just finished scoring (not a
        // hardcoded id) and targets the actual GenerateSummariesJob this job was constructed with
        // (not some other instance).
        Assert.Equal("job-42", continuationLink.AttachedParentJobId);
        Assert.Same(summarizationJob, continuationLink.AttachedSummarizationJob);
    }

    // Same PerformContext-construction approach as DiscoverRepositoriesJobTests.cs's own helper -
    // kept local rather than shared, mirroring the vertical-slice convention applied to tests. See
    // that file's own comments for why each fake member below is shaped the way it is.
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
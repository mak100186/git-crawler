using System.Reflection;

using GitCrawler.Api.Features.Crawling.DiscoverRepositories;
using GitCrawler.Api.Features.Scoring.ComputeScores;
using GitCrawler.Api.Features.Summarization.GenerateSummaries;
using GitCrawler.Api.Features.Trends.AggregateTrends;

using Hangfire;
using Hangfire.Server;
using Hangfire.Storage;

namespace GitCrawler.Api.Tests.Features.Crawling.DiscoverRepositories;

// Covers the Hangfire-to-Wolverine glue class Program.cs's RecurringJob.AddOrUpdate targets - both
// that RunAsync invokes the Wolverine command (retry/backoff/upsert idempotency belong to and are
// already covered by DiscoverRepositoriesCommandHandlerTests) and, as of F-007, that it requests
// the scoring continuation for the right job/target afterward. IScoringContinuationLink is faked
// rather than exercising the real BackgroundJob.ContinueJobWith static call, which requires a live
// JobStorage.Current unavailable in a unit test (Task Packet's explicit testing constraint - see
// IScoringContinuationLink's own comment); real end-to-end verification of the chaining behavior is
// left to the Integration Agent's live Postgres+Hangfire check.
//
// ComputeScoresJob's own constructor now takes a GenerateSummariesJob + ISummarizationContinuationLink
// (F-008, link 3 of the chain) - a FakeSummarizationContinuationLink is passed here purely to satisfy
// that constructor; this file's own assertions are unaffected since they only exercise
// DiscoverRepositoriesJob's chain into ComputeScoresJob, not ComputeScoresJob's own chain into
// GenerateSummariesJob (that's GenerateSummariesJobTests' responsibility).
public class DiscoverRepositoriesJobTests
{
    [Fact]
    public void RunAsync_IsDecoratedWithDisableConcurrentExecution()
    {
        // F-016/NFR-003: a reflection-based attribute-presence check, consistent with this
        // codebase's existing practice of not exercising real Hangfire statics in a fast unit test
        // (see IScoringContinuationLink's own comment above) - BackgroundJob.ContinueJobWith and
        // Hangfire's own distributed-lock acquisition both require a live JobStorage.Current that
        // isn't reachable here either.
        var attribute = typeof(DiscoverRepositoriesJob)
            .GetMethod(nameof(DiscoverRepositoriesJob.RunAsync))!
            .GetCustomAttribute<DisableConcurrentExecutionAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(60 * 60, attribute!.TimeoutSec);
    }

    [Fact]
    public async Task RunAsync_InvokesDiscoverRepositoriesCommandOnMessageBus()
    {
        var messageBus = new FakeMessageBus();
        var job = new DiscoverRepositoriesJob(messageBus, CreateComputeScoresJob(), new FakeScoringContinuationLink());

        await job.RunAsync(CreatePerformContext("job-1"));

        var invoked = Assert.Single(messageBus.InvokedMessages);
        Assert.IsType<DiscoverRepositoriesCommand>(invoked);
    }

    [Fact]
    public async Task RunAsync_AttachesScoringContinuation_ForTheCompletedJobId()
    {
        var scoringJob = CreateComputeScoresJob();
        var continuationLink = new FakeScoringContinuationLink();
        var job = new DiscoverRepositoriesJob(new FakeMessageBus(), scoringJob, continuationLink);

        await job.RunAsync(CreatePerformContext("job-42"));

        // Proves the chain attaches to the specific job instance that just finished crawling (not a
        // hardcoded id) and targets the actual ComputeScoresJob this job was constructed with (not
        // some other instance).
        Assert.Equal("job-42", continuationLink.AttachedParentJobId);
        Assert.Same(scoringJob, continuationLink.AttachedScoringJob);
    }

    // Local helper purely to satisfy ComputeScoresJob's constructor (which now also needs a
    // GenerateSummariesJob + ISummarizationContinuationLink for its own F-008 chain attachment) -
    // this file's tests never exercise that chain themselves.
    private static ComputeScoresJob CreateComputeScoresJob() =>
        new(
            new FakeMessageBus(),
            new GenerateSummariesJob(new FakeMessageBus(), new AggregateTrendsJob(new FakeMessageBus()), new FakeTrendsContinuationLink()),
            new FakeSummarizationContinuationLink());

    // PerformContext's own constructor doesn't touch Hangfire's ambient JobStorage.Current - only
    // the static BackgroundJob.ContinueJobWith call (behind IScoringContinuationLink, faked above)
    // does - so a real PerformContext is safely constructible here with storage/connection fakes
    // whose members RunAsync never actually calls.
    //
    // Note: Hangfire.JobCancellationToken.Null (despite the type) is itself a null reference in
    // 1.8.24 - it's meant as a "pass null to mean no token" convention for call sites that check for
    // it, not a null-object instance - so passing it straight into PerformContext's constructor trips
    // its own internal ArgumentNullException.ThrowIfNull(cancellationToken) guard. A real no-op
    // implementation is needed instead; RunAsync never reads it either way (see above).
    private static PerformContext CreatePerformContext(string jobId) =>
        new(new FakeJobStorage(), new FakeStorageConnection(), new BackgroundJob(jobId, null!, DateTime.UtcNow), new NoOpJobCancellationToken());

    // See CreatePerformContext's note above on why Hangfire.JobCancellationToken.Null can't be used
    // here directly.
    private sealed class NoOpJobCancellationToken : IJobCancellationToken
    {
        public CancellationToken ShutdownToken => CancellationToken.None;

        public void ThrowIfCancellationRequested()
        {
        }
    }

    // The abstract JobStorage base requires these two members, neither of which RunAsync exercises.
    private sealed class FakeJobStorage : JobStorage
    {
        public override IStorageConnection GetConnection() => throw new NotSupportedException();

        public override IMonitoringApi GetMonitoringApi() => throw new NotSupportedException();
    }

    // Every member throws, since none of them are meant to be exercised by RunAsync - a fake used
    // for it would fail loudly instead of silently passing on the wrong call.
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
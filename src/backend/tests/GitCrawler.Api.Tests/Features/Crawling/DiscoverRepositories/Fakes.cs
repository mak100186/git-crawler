using GitCrawler.Api.Features.Crawling.DiscoverRepositories;
using GitCrawler.Api.Features.Scoring.ComputeScores;
using GitCrawler.Api.Features.Summarization.GenerateSummaries;
using GitCrawler.Api.Features.Trends.AggregateTrends;

using Wolverine;

namespace GitCrawler.Api.Tests.Features.Crawling.DiscoverRepositories;

// Test double for IGitHubDiscoveryClient - the abstraction this feature exists specifically to
// make possible (Task Packet: "no live external calls during development"). Each queue lets a
// test script a sequence of responses/failures per call, so rate-limit-then-succeed scenarios can
// be expressed without a real GitHub API.
internal class FakeGitHubDiscoveryClient : IGitHubDiscoveryClient
{
    private readonly Queue<Func<DiscoveryPage>> _discoveryResponses = new();
    private readonly Queue<Func<int>> _contributorResponses = new();

    public int DiscoverRepositoriesCallCount { get; private set; }

    public int GetContributorCountCallCount { get; private set; }

    public void EnqueueDiscoveryPage(DiscoveryPage page) => _discoveryResponses.Enqueue(() => page);

    public void EnqueueDiscoveryFailure(Exception exception) => _discoveryResponses.Enqueue(() => throw exception);

    public void EnqueueContributorCount(int count) => _contributorResponses.Enqueue(() => count);

    public void EnqueueContributorFailure(Exception exception) => _contributorResponses.Enqueue(() => throw exception);

    public Task<DiscoveryPage> DiscoverRepositoriesAsync(string? afterCursor, CancellationToken cancellationToken)
    {
        DiscoverRepositoriesCallCount++;
        return Task.FromResult(_discoveryResponses.Dequeue()());
    }

    public Task<int> GetContributorCountAsync(string owner, string name, CancellationToken cancellationToken)
    {
        GetContributorCountCallCount++;
        return Task.FromResult(_contributorResponses.Dequeue()());
    }
}

// Fixed-clock TimeProvider that also backs Polly's retry-delay timer (ADR-018's ResiliencePipeline
// is built with this as its TimeProvider). Overriding CreateTimer records the duration the pipeline
// actually asked to wait (replacing the old bespoke IRetryDelay fake) while firing the timer via the
// real base-class implementation with a near-zero due time, so rate-limit/backoff tests observe the
// requested delays without blocking for the real minutes/hours the handler would otherwise sleep.
internal class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public List<TimeSpan> RequestedDelays { get; } = [];

    public override DateTimeOffset GetUtcNow() => utcNow;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        RequestedDelays.Add(dueTime);
        return base.CreateTimer(callback, state, TimeSpan.Zero, period);
    }
}

// Fake for the F-007 seam over Hangfire's static BackgroundJob.ContinueJobWith (see
// IScoringContinuationLink's own comment for why that call isn't unit-testable directly). Records
// what DiscoverRepositoriesJob asked to attach, instead of touching real Hangfire statics.
internal class FakeScoringContinuationLink : IScoringContinuationLink
{
    public string? AttachedParentJobId { get; private set; }

    public ComputeScoresJob? AttachedScoringJob { get; private set; }

    public void AttachAfter(string parentJobId, ComputeScoresJob scoringJob)
    {
        AttachedParentJobId = parentJobId;
        AttachedScoringJob = scoringJob;
    }
}

// F-008: ComputeScoresJob's constructor now also needs an ISummarizationContinuationLink (its own
// chain attachment into GenerateSummariesJob, link 3). This file's tests only exercise
// DiscoverRepositoriesJob's chain into ComputeScoresJob (link 1 -> 2), not link 2 -> 3, so this fake
// exists purely to satisfy that constructor - GenerateSummariesJobTests is where the
// Summarization-slice equivalent of this fake actually gets asserted against.
internal class FakeSummarizationContinuationLink : ISummarizationContinuationLink
{
    public void AttachAfter(string parentJobId, GenerateSummariesJob summarizationJob)
    {
    }
}

// F-009: GenerateSummariesJob's constructor now also needs an AggregateTrendsJob +
// ITrendsContinuationLink (its own chain attachment into the Trend Aggregator, link 4). This file's
// tests only exercise DiscoverRepositoriesJob's chain into ComputeScoresJob (link 1 -> 2), so this
// fake exists purely to satisfy the constructor of the GenerateSummariesJob instance that
// ComputeScoresJob itself needs to be constructed - see the same rationale on
// FakeSummarizationContinuationLink above.
internal class FakeTrendsContinuationLink : ITrendsContinuationLink
{
    public void AttachAfter(string parentJobId, AggregateTrendsJob trendsJob)
    {
    }
}

// Hand-rolled fake for Wolverine's IMessageBus (no mocking library is referenced in this test
// project - see the other fakes in this file for the same pattern). Only InvokeAsync(object, ...)
// is exercised by DiscoverRepositoriesJob; every other member of this fairly large interface
// throws if called, so a test would fail loudly instead of silently passing on the wrong overload.
internal class FakeMessageBus : IMessageBus
{
    public List<object> InvokedMessages { get; } = [];

    public string? TenantId { get; set; }

    public Task InvokeAsync(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
    {
        InvokedMessages.Add(message);
        return Task.CompletedTask;
    }

    public Task InvokeAsync(object message, DeliveryOptions options, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public Task<T> InvokeAsync<T>(object message, DeliveryOptions options, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> StreamAsync<TResponse>(object message, CancellationToken cancellation = default) =>
        throw new NotSupportedException();

    public IAsyncEnumerable<TResponse> StreamAsync<TResponse>(object message, DeliveryOptions options, CancellationToken cancellation = default) =>
        throw new NotSupportedException();

    public Task<TResponse> StreamAsync<TRequest, TResponse>(IAsyncEnumerable<TRequest> source, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public Task<TResponse> StreamAsync<TRequest, TResponse>(IAsyncEnumerable<TRequest> source, DeliveryOptions options, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public Task InvokeForTenantAsync(string tenantId, object message, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public Task<T> InvokeForTenantAsync<T>(string tenantId, object message, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public IDestinationEndpoint EndpointFor(string endpointName) => throw new NotSupportedException();

    public IDestinationEndpoint EndpointFor(Uri uri) => throw new NotSupportedException();

    public IReadOnlyList<Envelope> PreviewSubscriptions(object message) => throw new NotSupportedException();

    public IReadOnlyList<Envelope> PreviewSubscriptions(object message, DeliveryOptions options) => throw new NotSupportedException();

    public ValueTask SendAsync<T>(T message, DeliveryOptions? options = null) => throw new NotSupportedException();

    public ValueTask PublishAsync<T>(T message, DeliveryOptions? options = null) => throw new NotSupportedException();

    public ValueTask BroadcastToTopicAsync(string topicName, object message, DeliveryOptions? options = null) => throw new NotSupportedException();
}
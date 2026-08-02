using GitCrawler.Api.Features.Crawling.DiscoverRepositories;
using GitCrawler.Api.Features.Scoring.ComputeScores;

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

// Records requested wait durations instead of actually waiting, so rate-limit/backoff tests run
// instantly rather than blocking for the real minutes/hours the handler would otherwise sleep.
internal class FakeRetryDelay : IRetryDelay
{
    public List<TimeSpan> RequestedDelays { get; } = [];

    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken)
    {
        RequestedDelays.Add(duration);
        return Task.CompletedTask;
    }
}

// Minimal fixed-clock TimeProvider - lets tests control "now" precisely (e.g. to compute an exact
// expected wait duration from a resetAt value) without pulling in a testing package for it.
internal class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
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
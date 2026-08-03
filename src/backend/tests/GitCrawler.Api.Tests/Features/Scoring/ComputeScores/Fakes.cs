using GitCrawler.Api.Features.Summarization.GenerateSummaries;
using GitCrawler.Api.Features.Trends.AggregateTrends;

using Wolverine;

namespace GitCrawler.Api.Tests.Features.Scoring.ComputeScores;

// Minimal fixed-clock TimeProvider - lets tests control "now" precisely (e.g. to compute an exact
// expected CommitsPerWeek, or to set up a repo's LastCrawledAtUtc relative to a Score's
// ComputedAtUtc) without pulling in a testing package for it. Same pattern as the Crawling feature's
// own fake (see Features/Crawling/DiscoverRepositories/Fakes.cs) - kept local rather than shared
// across feature test folders, mirroring the vertical-slice convention (ADR-015) applied to tests.
internal class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

// F-008: ComputeScoresJob now attaches a summarization continuation after scoring completes (link 2
// -> link 3, same "RecurringJob + ContinueJobWith" pattern F-006/F-007 established). Fake for the
// seam over Hangfire's static BackgroundJob.ContinueJobWith - see ISummarizationContinuationLink's
// own comment (Features/Summarization/GenerateSummaries/GenerateSummariesJob.cs) for why that call
// isn't unit-testable directly.
internal class FakeSummarizationContinuationLink : ISummarizationContinuationLink
{
    public string? AttachedParentJobId { get; private set; }

    public GenerateSummariesJob? AttachedSummarizationJob { get; private set; }

    public void AttachAfter(string parentJobId, GenerateSummariesJob summarizationJob)
    {
        AttachedParentJobId = parentJobId;
        AttachedSummarizationJob = summarizationJob;
    }
}

// F-009: GenerateSummariesJob's constructor now also needs an AggregateTrendsJob +
// ITrendsContinuationLink (its own chain attachment into the Trend Aggregator, link 4). This file's
// tests only exercise ComputeScoresJob's chain into GenerateSummariesJob (link 2 -> 3), not link
// 3 -> 4, so this fake exists purely to satisfy that constructor - GenerateSummariesJobTests is
// where the F-008->F-009 attachment itself gets asserted against.
internal class FakeTrendsContinuationLink : ITrendsContinuationLink
{
    public void AttachAfter(string parentJobId, AggregateTrendsJob trendsJob)
    {
    }
}

// Hand-rolled fake for Wolverine's IMessageBus - same pattern as the Crawling feature's own local
// copy (Features/Crawling/DiscoverRepositories/Fakes.cs). Only the typed InvokeAsync<T>(object, ...)
// is exercised by ComputeScoresJob/GenerateSummariesJob (see those classes' own comments for why
// it's the typed overload, not the bare object one); every other member throws if called, so a
// test would fail loudly instead of silently passing on the wrong overload.
internal class FakeMessageBus : IMessageBus
{
    public List<object> InvokedMessages { get; } = [];

    public string? TenantId { get; set; }

    public Task InvokeAsync(object message, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public Task InvokeAsync(object message, DeliveryOptions options, CancellationToken cancellation = default, TimeSpan? timeout = null) =>
        throw new NotSupportedException();

    public Task<T> InvokeAsync<T>(object message, CancellationToken cancellation = default, TimeSpan? timeout = null)
    {
        InvokedMessages.Add(message);
        return Task.FromResult(default(T)!);
    }

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
using GitCrawler.Api.Features.Summarization.GenerateSummaries;
using GitCrawler.Api.Features.Trends.AggregateTrends;

using Wolverine;

namespace GitCrawler.Api.Tests.Features.Summarization.GenerateSummaries;

// Test double for IRepositorySummarizer - the abstraction this feature exists specifically to make
// possible (ADR-001: no live LM Studio calls during development/testing). Records every context it
// was asked to summarize, and lets a test script per-call success/failure the same way
// FakeGitHubDiscoveryClient does for F-005 (see Features/Crawling/DiscoverRepositories/Fakes.cs).
internal class FakeRepositorySummarizer : IRepositorySummarizer
{
    private readonly Queue<Func<string>> _responses = new();

    public List<RepositorySummarizationContext> Requests { get; } = [];

    public void EnqueueSummary(string content) => _responses.Enqueue(() => content);

    public void EnqueueFailure(Exception exception) => _responses.Enqueue(() => throw exception);

    public Task<string> SummarizeAsync(RepositorySummarizationContext context, CancellationToken cancellationToken)
    {
        Requests.Add(context);
        return Task.FromResult(_responses.Dequeue()());
    }
}

// GenerateSummariesCommandHandler fetches READMEs directly via IHttpClientFactory (reusing the
// GitHubRest named client - Task Packet's explicit instruction, no second REST-client abstraction),
// so exercising the 404/success/failure paths needs a fake HttpClientFactory + HttpMessageHandler
// rather than a fake interface. No mocking library is referenced in this test project (same
// hand-rolled-fake convention as every other Fakes.cs in this suite).
internal class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly Dictionary<string, HttpClient> _clients = [];

    public void RegisterClient(string name, HttpClient client) => _clients[name] = client;

    public HttpClient CreateClient(string name) =>
        _clients.TryGetValue(name, out var client) ? client : throw new InvalidOperationException($"No fake HttpClient registered for '{name}'.");
}

// Overrides SendAsync only - the one member GenerateSummariesCommandHandler's README fetch actually
// exercises - so a request for anything unscripted fails loudly via the delegate throwing/returning
// whatever the test wired up, rather than silently hitting real network.
internal class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(respond(request));
}

// Fixed-clock TimeProvider - same minimal pattern as the Scoring/Crawling features' own local
// copies (kept local per feature test folder, mirroring the vertical-slice convention applied to
// tests).
internal class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

// Fake for the F-008 seam over Hangfire's static BackgroundJob.ContinueJobWith (see
// ISummarizationContinuationLink's own comment for why that call isn't unit-testable directly).
// Records what ComputeScoresJob asked to attach, instead of touching real Hangfire statics.
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
// ITrendsContinuationLink (its own chain attachment into the Trend Aggregator, link 4). Fake for the
// seam over Hangfire's static BackgroundJob.ContinueJobWith - see ITrendsContinuationLink's own
// comment (Features/Trends/AggregateTrends/AggregateTrendsJob.cs) for why that call isn't
// unit-testable directly. Records what GenerateSummariesJob asked to attach.
internal class FakeTrendsContinuationLink : ITrendsContinuationLink
{
    public string? AttachedParentJobId { get; private set; }

    public AggregateTrendsJob? AttachedTrendsJob { get; private set; }

    public void AttachAfter(string parentJobId, AggregateTrendsJob trendsJob)
    {
        AttachedParentJobId = parentJobId;
        AttachedTrendsJob = trendsJob;
    }
}

// Hand-rolled fake for Wolverine's IMessageBus - same pattern as the Scoring/Crawling features' own
// local copies. Only InvokeAsync(object, ...) is exercised by GenerateSummariesJob; every other
// member of this fairly large interface throws if called, so a test would fail loudly instead of
// silently passing on the wrong overload.
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
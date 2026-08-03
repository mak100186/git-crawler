using Wolverine;

namespace GitCrawler.Api.Tests.Features.Trends.AggregateTrends;

// Fixed-clock TimeProvider - same minimal pattern as the Scoring/Summarization features' own local
// copies (kept local per feature test folder, mirroring the vertical-slice convention applied to
// tests).
internal class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

// Hand-rolled fake for Wolverine's IMessageBus - same pattern as the Scoring/Summarization/Crawling
// features' own local copies. Only the typed InvokeAsync<T>(object, ...) is exercised by
// AggregateTrendsJob (see that class's own comment for why it's the typed overload, not the bare
// object one); every other member of this fairly large interface throws if called, so a test would
// fail loudly instead of silently passing on the wrong overload.
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
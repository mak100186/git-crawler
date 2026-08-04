using GitCrawler.Api.Features.Digest.SendDigest;

using Wolverine;

namespace GitCrawler.Api.Tests.Features.Digest.SendDigest;

// Test double for IEmailSender - the abstraction this feature exists specifically to make possible
// (no live SMTP send during development/testing, same rationale as FakeRepositorySummarizer for
// IRepositorySummarizer - Features/Summarization/GenerateSummaries/Fakes.cs). Records every message
// it was asked to send, and lets a test script a thrown failure the same way FakeRepositorySummarizer
// does for its own per-repo calls.
internal class FakeEmailSender : IEmailSender
{
    private Exception? _failure;

    public List<EmailMessage> SentMessages { get; } = [];

    public void FailNextSendWith(Exception exception) => _failure = exception;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (_failure is { } failure)
        {
            _failure = null;
            throw failure;
        }

        SentMessages.Add(message);
        return Task.CompletedTask;
    }
}

// Fixed-clock TimeProvider - same minimal pattern as every other feature's own local copy (kept
// local per feature test folder, mirroring the vertical-slice convention applied to tests).
internal class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}

// Hand-rolled fake for Wolverine's IMessageBus - same pattern as every other feature's own local
// copy. Only the typed InvokeAsync<T>(object, ...) is exercised by SendDigestJob (see that class's
// own comment for why it's the typed overload, not the bare object one); every other member of this
// fairly large interface throws if called, so a test would fail loudly instead of silently passing
// on the wrong overload.
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
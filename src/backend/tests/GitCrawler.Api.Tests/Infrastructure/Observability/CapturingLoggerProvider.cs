using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace GitCrawler.Api.Tests.Infrastructure.Observability;

// Captures every log entry emitted through it, including the structured named values behind each
// message template's {Placeholder} tokens - not just the rendered message string. Microsoft.
// Extensions.Logging already builds that structured state (a FormattedLogValues-shaped
// IEnumerable<KeyValuePair<string, object>> with one entry per template placeholder, plus a trailing
// "{OriginalFormat}" entry) before it ever reaches an ILoggerProvider, regardless of provider - this
// just captures and re-exposes it, so tests can assert on ObservabilityMiddleware's actual
// RecordsProcessed/ElapsedMs/MessageType values rather than string-matching a rendered log line.
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentBag<LogEntry> _entries = [];

    public IReadOnlyCollection<LogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    public sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception, IReadOnlyDictionary<string, object?> State);

    private sealed class CapturingLogger(string category, ConcurrentBag<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var namedValues = (state as IEnumerable<KeyValuePair<string, object?>>) ?? [];
            var stateDictionary = namedValues.ToDictionary(kv => kv.Key, kv => kv.Value);

            entries.Add(new LogEntry(logLevel, category, formatter(state, exception), exception, stateDictionary));
        }
    }
}
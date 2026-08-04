using Microsoft.Extensions.Logging;

using Wolverine;

namespace GitCrawler.Api.Tests.Infrastructure.Observability;

// Covers F-014's Test Expectations: a success record (records-processed + duration + status), a
// failure record (with exception detail, control flow unaffected), platform-wide wiring across
// several unrelated handler types (not one hand-picked example), and the two named edge cases (a
// zero-count result, and an already-slow handler). See ObservabilityHostFixture's own header
// comment for why this exercises local fake handlers rather than the real GitCrawler.Api handler
// graph directly.
public class ObservabilityMiddlewareTests(ObservabilityHostFixture fixture) : IClassFixture<ObservabilityHostFixture>
{
    [Fact]
    public async Task Success_emits_completion_record_with_records_processed_duration_and_ok_status()
    {
        var result = await fixture.Bus.InvokeAsync<CrawlLikeResult>(new CrawlLikeCommand());

        Assert.Equal(12, result.DiscoveredCount);

        var entry = SingleCompletionEntry(nameof(CrawlLikeCommand));

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Equal(12, Convert.ToInt32(entry.State["RecordsProcessed"]));
        Assert.True(Convert.ToInt64(entry.State["ElapsedMs"]) >= 0);
        Assert.Contains("OK", entry.Message);
    }

    [Fact]
    public async Task Failure_emits_failure_record_with_exception_detail_and_exception_still_propagates()
    {
        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Bus.InvokeAsync(new FailingCommand()));

        Assert.Equal("simulated handler failure", thrown.Message);

        var entry = SingleCompletionEntry(nameof(FailingCommand));

        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        Assert.Contains("FAILED", entry.Message);

        // AC3 requires records-processed on every invocation, including failures - 0 because the
        // handler threw before producing a countable result (see ObservabilityMiddleware.OnException).
        Assert.Equal(0, Convert.ToInt32(entry.State["RecordsProcessed"]));

        // The success path (RecordSuccess/RecordSuccessVoid, wired via RecordsProcessedPolicy's
        // postprocessor frame) must never also fire for a failed invocation - only OnException's own
        // completion log above should exist for this MessageType.
        Assert.DoesNotContain(
            fixture.Logs.Entries,
            e => MessageTypeOf(e) == nameof(FailingCommand) && e.Message.Contains("OK"));
    }

    [Fact]
    public async Task Wired_platform_wide_fires_for_several_unrelated_handler_types_not_one_opted_in_handler()
    {
        await fixture.Bus.InvokeAsync<PlatformWideProbeAResult>(new PlatformWideProbeACommand());
        await fixture.Bus.InvokeAsync<PlatformWideProbeBResult>(new PlatformWideProbeBQuery());
        await fixture.Bus.InvokeAsync(new PlatformWideProbeCCommand());

        // Three structurally unrelated message types - a command, a query, and a void handler with
        // no cascading result at all - each produced their own "Completed ... - OK" record, proving
        // this is genuinely global middleware/policy registration rather than something opted into
        // one handler at a time.
        foreach (var messageType in new[]
                 {
                     nameof(PlatformWideProbeACommand), nameof(PlatformWideProbeBQuery), nameof(PlatformWideProbeCCommand),
                 })
        {
            SingleCompletionEntry(messageType);
        }
    }

    [Fact]
    public async Task Zero_count_result_still_emits_success_record_not_an_error()
    {
        var result = await fixture.Bus.InvokeAsync<ScoreLikeResult>(new ScoreLikeQuery());

        Assert.Equal(0, result.ScoredCount);

        var entry = SingleCompletionEntry(nameof(ScoreLikeQuery));

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Equal(0, Convert.ToInt32(entry.State["RecordsProcessed"]));
    }

    [Fact]
    public async Task Void_handler_with_no_countable_result_still_emits_success_record()
    {
        await fixture.Bus.InvokeAsync(new NoOpCommand());

        var entry = SingleCompletionEntry(nameof(NoOpCommand));

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Equal(1, Convert.ToInt32(entry.State["RecordsProcessed"]));
    }

    [Fact]
    public async Task Bool_first_result_extracts_the_count_property_not_the_leading_bool()
    {
        var result = await fixture.Bus.InvokeAsync<DigestLikeResult>(new DigestLikeCommand());

        Assert.True(result.Sent);
        Assert.Equal(8, result.RepositoryCount);

        var entry = SingleCompletionEntry(nameof(DigestLikeCommand));

        Assert.Equal(8, Convert.ToInt32(entry.State["RecordsProcessed"]));
    }

    [Fact]
    public async Task Non_count_int_property_result_reports_one_not_the_raw_property_value()
    {
        var result = await fixture.Bus.InvokeAsync<CreateEntityLikeResult>(new CreateEntityLikeCommand());

        Assert.Equal(42, result.Id);

        var entry = SingleCompletionEntry(nameof(CreateEntityLikeCommand));

        // No "*Count"-named property and no collection on this result - the heuristic must not grab
        // the leading `Id` property's raw value (42) as the count; it defaults to 1 (one entity
        // created), matching BookmarkDto/CreateBookmarkCommandHandler's shape.
        Assert.Equal(1, Convert.ToInt32(entry.State["RecordsProcessed"]));
    }

    [Fact]
    public async Task Slow_handler_still_returns_correctly_with_duration_reflecting_actual_elapsed_time()
    {
        var result = await fixture.Bus.InvokeAsync<SummarizeLikeResult>(new SummarizeLikeCommand());

        Assert.Equal(3, result.SummarizedCount);
        Assert.Equal(1, result.FailedCount);

        var entry = SingleCompletionEntry(nameof(SummarizeLikeCommand));

        // The handler itself awaits a 60ms delay - a small margin below that accounts for timer
        // resolution without making this test flaky.
        Assert.True(
            Convert.ToInt64(entry.State["ElapsedMs"]) >= 50,
            $"expected at least ~50ms elapsed, got {entry.State["ElapsedMs"]}");
        Assert.Equal(3, Convert.ToInt32(entry.State["RecordsProcessed"]));
    }

    // Picks out this specific invocation's own "Completed ..." record by MessageType - the fixture's
    // logger provider accumulates entries across every test in this class (one shared host, per
    // ObservabilityHostFixture's own comment), but every fake command/query type here is unique to
    // the test(s) that use it, so filtering by MessageType alone is enough to isolate it. Fails
    // loudly (via Single) rather than silently picking the wrong entry if that assumption is ever
    // violated.
    private CapturingLoggerProvider.LogEntry SingleCompletionEntry(string messageType) =>
        Assert.Single(fixture.Logs.Entries, e => MessageTypeOf(e) == messageType && e.Message.StartsWith("Completed"));

    // envelope.MessageType (what ObservabilityMiddleware actually logs under the "MessageType" state
    // key) is Wolverine's fully-qualified CLR type name (e.g.
    // "GitCrawler.Api.Tests.Infrastructure.Observability.CrawlLikeCommand"), not the bare type name -
    // strip the namespace so this matches the nameof(...) values every SingleCompletionEntry caller
    // above passes in, consistent with SingleCompletionEntry's own comment that bare-name filtering is
    // enough to isolate a record, since every fake command/query type here is unique.
    private static string? MessageTypeOf(CapturingLoggerProvider.LogEntry entry)
    {
        if (!entry.State.TryGetValue("MessageType", out var value) || value is not string fullName)
        {
            return null;
        }

        var lastDot = fullName.LastIndexOf('.');
        return lastDot >= 0 ? fullName[(lastDot + 1)..] : fullName;
    }
}
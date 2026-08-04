using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;

using Wolverine;
using Wolverine.Attributes;

namespace GitCrawler.Api.Infrastructure.Observability;

// F-014: cross-cutting Wolverine middleware wrapping every command/query handler platform-wide -
// the one deliberate exception to ADR-015's Features/<Area>/<Operation> vertical-slice layout,
// since this applies to every slice rather than belonging to one. Registered once, globally, via
// opts.Policies.AddMiddleware<ObservabilityMiddleware>() in Program.cs's UseWolverine(...) block -
// no handler opts in individually, and nothing here is bolted onto the Hangfire *Job.cs glue
// classes specifically (it wraps the Wolverine command/query invocation those jobs trigger via
// IMessageBus.InvokeAsync, exactly the same as every HTTP-triggered query).
//
// What this adds beyond the Hangfire dashboard (F-006/ADR-009, NFR-005's own gap note in
// Architecture.md): Hangfire's dashboard shows job-level history - a RecurringJob's own
// start/end/succeeded/failed/retry-count - for the handful of top-level scheduled jobs only. It has
// no visibility into what happens *inside* one job execution (e.g. DiscoverRepositoriesJob's single
// Hangfire "job" wraps one DiscoverRepositoriesCommand invocation, whose own duration/outcome/volume
// Hangfire never separately records), and none at all into the Web API's query handlers, which
// aren't Hangfire jobs to begin with. This middleware instruments every Wolverine command/query
// invocation individually - job-triggered or HTTP-triggered - giving the stage-level
// duration/outcome/volume detail the dashboard has no equivalent for. The "Starting ..." line from
// Before below, specifically, is what lets a solo operator diagnose a run that's *currently* stuck
// (e.g. mid rate-limit wait inside DiscoverRepositoriesCommandHandler's indefinite Polly retry
// loop, ADR-018): a "Starting X" with no matching "Completed X" for an implausibly long time,
// alongside that handler's own "GitHub rate limit hit; waiting..." warnings, is the log-only
// diagnosis NFR-005 asks for.
//
// Two Wolverine idioms combine to deliver this, not one. This class uses the simpler
// attribute-based Before/OnException convention (WolverineBeforeAttribute/
// WolverineOnExceptionAttribute) for everything it can reliably get that way: duration and
// success/failure status. What it deliberately does NOT attempt is binding a parameter to "whatever
// the handler happened to return" - verified empirically against WolverineFx 6.24.2 that this
// convention's parameter resolver only recognises a fixed set of well-known types (Envelope,
// CancellationToken, DI services); a same-named `object result` parameter is left unresolved and
// silently defaulted to `new object()` rather than the real return value or null, with no public
// option to opt into "bind the return value" this way. Getting the actual, correctly-typed return
// value for the "records processed" figure (AC3) instead requires RecordsProcessedPolicy, a raw
// IChainPolicy with full reflection access to each chain's real handler return type at chain-build
// time, wiring a generic MethodCall frame directly against it (see that class's own comment).
public class ObservabilityMiddleware
{
    // Keyed by Envelope.Id rather than a value returned from Before() and threaded through to
    // OnException/RecordSuccess*: Wolverine's own codegen has a scoping bug (reproduced against
    // 6.24.2) where a value returned from a [WolverineBefore] method and consumed by a
    // [WolverineOnException] method on the same middleware type generates an invalid `catch` block
    // referencing a variable declared inside the sibling `try` block ("CS0103: the name '...' does
    // not exist in the current context" at Wolverine's own runtime-compilation step). Envelope.Id is
    // available unconditionally throughout the whole generated method instead - it's a property read
    // off the ambient Envelope, not a locally-scoped variable this middleware introduces - so a
    // static, per-invocation dictionary keyed by it sidesteps the bug entirely without touching
    // Wolverine internals. Guid keys are unique per invocation (a fresh Envelope.Id every call), so
    // there's no cross-invocation collision risk even under full concurrency.
    private static readonly ConcurrentDictionary<Guid, Stopwatch> Stopwatches = new();

    [WolverineBefore]
    public static void Before(Envelope envelope, ILogger<ObservabilityMiddleware> logger)
    {
        Stopwatches[envelope.Id] = Stopwatch.StartNew();

        logger.LogInformation("Starting {MessageType} ({EnvelopeId})", envelope.MessageType, envelope.Id);
    }

    [WolverineOnException]
    public static void OnException(Exception exception, Envelope envelope, ILogger<ObservabilityMiddleware> logger)
    {
        // RecordsProcessed=0 here, not omitted and not an attempt to recover a partial count from the
        // exception context: the handler threw before (or during) producing a result, so zero records
        // were successfully processed by this invocation. AC3 requires records-processed on every
        // invocation, including failures - this keeps the failure log line schema-consistent with the
        // success line's RecordsProcessed field instead of leaving it absent.
        logger.LogError(
            exception,
            "Completed {MessageType} ({EnvelopeId}) in {ElapsedMs}ms - FAILED, RecordsProcessed={RecordsProcessed}",
            envelope.MessageType,
            envelope.Id,
            ElapsedMillisecondsSince(envelope),
            0);

        // Observe and log only - never alter control flow (Task Packet constraint: F-005's
        // rate-limit retries, F-008's per-repo skip-on-failure, etc. must keep behaving exactly as
        // they did before this middleware existed). ExceptionDispatchInfo.Capture(...).Throw()
        // rethrows with the original stack trace intact, unlike a bare `throw exception;` (which
        // would reset it to this line) - the caller sees the identical exception it would have seen
        // with no middleware in place at all.
        ExceptionDispatchInfo.Capture(exception).Throw();
    }

    // Called only by RecordsProcessedPolicy's generated postprocessor frame (see that class), never
    // discovered by Wolverine's own attribute scanning - the generic parameter it needs (the
    // handler's actual return type) can't be expressed on an ordinary attribute-convention method.
    // Public (not internal) so that reflection lookup, and the reflection-built closed generic
    // MethodCall RecordsProcessedPolicy constructs from it, need no non-public BindingFlags.
    public static void RecordSuccess<TResult>(Envelope envelope, ILogger<ObservabilityMiddleware> logger, TResult result)
    {
        LogCompletion(envelope, logger, ExtractRecordsProcessed(result));
    }

    // Counterpart to RecordSuccess<TResult> above for handlers with no return value at all (a
    // void/bare-Task Handle/HandleAsync) - RecordsProcessedPolicy picks whichever of the two applies
    // per chain, since there's no return variable to bind a generic parameter to here. Defaults
    // RecordsProcessed to 1 (one invocation, one unit of work performed) rather than 0 - this is a
    // completed, successful operation, not an empty result set.
    public static void RecordSuccessVoid(Envelope envelope, ILogger<ObservabilityMiddleware> logger)
    {
        LogCompletion(envelope, logger, recordsProcessed: 1);
    }

    private static void LogCompletion(Envelope envelope, ILogger logger, int recordsProcessed) =>
        logger.LogInformation(
            "Completed {MessageType} ({EnvelopeId}) in {ElapsedMs}ms - OK, RecordsProcessed={RecordsProcessed}",
            envelope.MessageType,
            envelope.Id,
            ElapsedMillisecondsSince(envelope),
            recordsProcessed);

    // -1 (rather than throwing) if this invocation's Before somehow never ran - defensive only;
    // every chain Wolverine compiles gets this middleware applied platform-wide (AC2), so in
    // practice this always finds and removes its own Stopwatch. TryRemove (not TryGetValue) so a
    // completed invocation's entry doesn't linger in the dictionary indefinitely.
    private static long ElapsedMillisecondsSince(Envelope envelope) =>
        Stopwatches.TryRemove(envelope.Id, out var stopwatch) ? stopwatch.ElapsedMilliseconds : -1;

    // AC3's "a count meaningful to that stage" has no single schema across this codebase's dozen-odd
    // command/query results (DiscoverRepositoriesResult.DiscoveredCount, ComputeScoresResult.
    // ScoredCount, PagedResult<T>.TotalCount, GetCategoriesResult.Categories, BookmarkDto with no
    // count property at all, ...), and there is no shared marker interface for it today - adding one
    // to every existing result record purely to serve this one cross-cutting feature would be
    // exactly the kind of speculative, single-use abstraction this codebase's own conventions avoid
    // elsewhere. This instead reflects over whatever the handler actually returned, in priority
    // order: (1) an int/long property whose name contains "Count" (matches DiscoveredCount/
    // ScoredCount/SummarizedCount/TrendCount/RepositoryCount/TotalCount - and deliberately skips a
    // leading `bool Sent`-shaped property first, e.g. SendDigestResult); (2) a collection-shaped
    // property's element count (covers GetCategoriesResult.Categories); (3) default to 1 - a single
    // unit was still processed (covers BookmarkDto, DeleteBookmarkResult, PingResult, and any future
    // result shape with no countable signal at all) rather than treating "no count found" as an
    // error, per this feature's own edge-case requirement.
    //
    // Deliberately no "any int/long property" fallback between (1) and (2): an arbitrary int/long
    // property picked in declaration order isn't reliably a count - e.g. BookmarkDto(int Id, int
    // RepositoryId, DateTimeOffset CreatedAtUtc) has no count-shaped property at all, and grabbing
    // its first int property (Id) would misreport "RecordsProcessed=42" for a single bookmark
    // creation instead of the semantically correct 1. Falling straight through to the (3) default of
    // 1 is simpler than - and just as correct as - trying to heuristically detect and skip
    // identifier-shaped properties (Id, {TypeName}Id, ...).
    private static int ExtractRecordsProcessed(object? result)
    {
        if (result is null)
        {
            return 1;
        }

        var properties = result.GetType().GetProperties();

        var namedCountProperty = properties.FirstOrDefault(p =>
            (p.PropertyType == typeof(int) || p.PropertyType == typeof(long))
            && p.Name.Contains("Count", StringComparison.OrdinalIgnoreCase));
        if (namedCountProperty is not null)
        {
            return Convert.ToInt32(namedCountProperty.GetValue(result));
        }

        var collectionProperty = properties.FirstOrDefault(p =>
            typeof(IEnumerable).IsAssignableFrom(p.PropertyType) && p.PropertyType != typeof(string));
        if (collectionProperty?.GetValue(result) is IEnumerable enumerable)
        {
            return enumerable is ICollection collection ? collection.Count : enumerable.Cast<object>().Count();
        }

        return 1;
    }
}
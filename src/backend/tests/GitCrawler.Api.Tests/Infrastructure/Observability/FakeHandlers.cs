namespace GitCrawler.Api.Tests.Infrastructure.Observability;

// Local, disposable fakes standing in for real handlers across several unrelated pipeline areas
// (Crawling, Scoring, Summarization, Digest, Web-API queries) - see ObservabilityHostFixture's own
// header comment for why these are used instead of the real GitCrawler.Api handler graph. Wolverine
// discovers each *Handler class by convention (a public Handle/HandleAsync method), the same
// convention every real handler in this codebase relies on - nothing here opts these into
// ObservabilityMiddleware/RecordsProcessedPolicy individually; both are registered once, globally,
// by ObservabilityHostFixture, exactly like Program.cs registers them for the real app.

// Mirrors DiscoverRepositoriesResult's shape (a leading "DiscoveredCount"-named int) for the
// success + records-processed test.
public record CrawlLikeCommand;
public record CrawlLikeResult(int DiscoveredCount);

public class CrawlLikeHandler
{
    public CrawlLikeResult Handle(CrawlLikeCommand command) => new(DiscoveredCount: 12);
}

// Mirrors a query returning zero rows (e.g. GetHiddenGemsQuery's PagedResult<T> with TotalCount=0
// when nothing matches a filter) - the "no meaningful records processed count still gets a
// duration+success record, not an error" edge case, for a genuinely-zero (not absent) count.
public record ScoreLikeQuery;
public record ScoreLikeResult(int ScoredCount);

public class ScoreLikeHandler
{
    public ScoreLikeResult Handle(ScoreLikeQuery query) => new(ScoredCount: 0);
}

// Async HandleAsync (not the synchronous Handle every other fake here uses) with a simulated delay -
// the "a handler that's already slow still returns correctly with duration reflecting the actual
// elapsed time" edge case.
public record SummarizeLikeCommand;
public record SummarizeLikeResult(int SummarizedCount, int FailedCount);

public class SummarizeLikeHandler
{
    public async Task<SummarizeLikeResult> HandleAsync(SummarizeLikeCommand command, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(60), cancellationToken);
        return new SummarizeLikeResult(SummarizedCount: 3, FailedCount: 1);
    }
}

// Mirrors SendDigestResult's shape exactly - a leading `bool Sent` property, then an int count -
// proving ObservabilityMiddleware's records-processed heuristic skips the bool and finds the actual
// count property rather than misreading a bool as "no count found".
public record DigestLikeCommand;
public record DigestLikeResult(bool Sent, int RepositoryCount);

public class DigestLikeHandler
{
    public DigestLikeResult Handle(DigestLikeCommand command) => new(Sent: true, RepositoryCount: 8);
}

// A void Handle with no return value at all (no cascading result) - covers RecordsProcessedPolicy's
// no-return-variable branch (RecordSuccessVoid), distinct from ScoreLikeHandler's "returns a result
// whose count happens to be zero" case above.
public record NoOpCommand;

public class NoOpHandler
{
    public void Handle(NoOpCommand command)
    {
    }
}

// Mirrors BookmarkDto's shape exactly (a leading non-count int identifier property, then more
// non-count fields, and no "*Count"-named property anywhere) - proves the records-processed
// heuristic's priority #2 fallback reports 1 (one entity created) rather than misreading the leading
// `Id` property's raw value as the count.
public record CreateEntityLikeCommand;
public record CreateEntityLikeResult(int Id, int ParentId, DateTimeOffset CreatedAtUtc);

public class CreateEntityLikeHandler
{
    public CreateEntityLikeResult Handle(CreateEntityLikeCommand command) => new(Id: 42, ParentId: 7, CreatedAtUtc: DateTimeOffset.UtcNow);
}

// Always throws - the failure-path test: ObservabilityMiddleware.OnException must log the failure
// and rethrow unchanged, and RecordSuccess/RecordSuccessVoid must never fire for this invocation.
public record FailingCommand;

public class FailingHandler
{
    public void Handle(FailingCommand command) => throw new InvalidOperationException("simulated handler failure");
}

// A separate, disjoint set of trivial fakes used only by the platform-wide wiring test
// (ObservabilityMiddlewareTests.Wired_platform_wide_...) - deliberately never invoked by any other
// test in this class. Every test in the class shares one Wolverine host/logger (see
// ObservabilityHostFixture), so each fake command/query type must be invoked by exactly one test
// method for that test's own "find my one completion record by MessageType" lookup to stay valid;
// reusing CrawlLikeCommand/ScoreLikeQuery/etc. here (as well as in their own dedicated tests) would
// make that lookup ambiguous.
public record PlatformWideProbeACommand;
public record PlatformWideProbeAResult(int Count);

public class PlatformWideProbeAHandler
{
    public PlatformWideProbeAResult Handle(PlatformWideProbeACommand command) => new(1);
}

public record PlatformWideProbeBQuery;
public record PlatformWideProbeBResult(int Count);

public class PlatformWideProbeBHandler
{
    public PlatformWideProbeBResult Handle(PlatformWideProbeBQuery query) => new(2);
}

public record PlatformWideProbeCCommand;

public class PlatformWideProbeCHandler
{
    public void Handle(PlatformWideProbeCCommand command)
    {
    }
}
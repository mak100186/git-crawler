using GitCrawler.Api.Features.Trends.AggregateTrends;

using Hangfire;
using Hangfire.Server;

using Wolverine;

namespace GitCrawler.Api.Features.Summarization.GenerateSummaries;

// Hangfire-to-Wolverine glue, same pattern as ComputeScoresJob/DiscoverRepositoriesJob (see those
// classes' header comments for why this indirection exists at all). Registered as scoped in
// Program.cs so Hangfire's AspNetCoreJobActivator can resolve its IMessageBus dependency per job
// execution.
//
// Chain position (Architecture §3, "RecurringJob + ContinueJobWith"): this is link 3 of the
// pipeline chain - ComputeScoresJob.RunAsync attaches to this via BackgroundJob.ContinueJobWith
// once scoring finishes (see that class), rather than this having its own independent RecurringJob
// registration in Program.cs. Summarization's "schedule" IS "right after scoring finishes", not a
// separate cron - same reasoning ComputeScoresJob's header comment already gives for itself.
//
// F-009: link 4 (the Trend Aggregator) is attached below - a PerformContext parameter on RunAsync,
// and a BackgroundJob.ContinueJobWith call (via continuationLink, see ITrendsContinuationLink's own
// comment for why that call is behind a seam rather than a direct static call) once InvokeAsync
// completes. This class's own comment previously said this was "the last link Architecture §3
// currently describes" and deferred Trend Aggregator/Digest Service as speculative - that's now
// stale for Trend Aggregator specifically, which exists and is wired in below. Digest Service
// (F-013) remains the actual not-yet-built next pipeline stage per docs/handoff.md, not something
// this feature chains into speculatively.
public class GenerateSummariesJob(IMessageBus messageBus, AggregateTrendsJob trendsJob, ITrendsContinuationLink continuationLink)
{
    public async Task RunAsync(PerformContext context)
    {
        await messageBus.InvokeAsync(new GenerateSummariesCommand());

        continuationLink.AttachAfter(context.BackgroundJob.Id, trendsJob);
    }
}

// Same extraction-point rationale as IScoringContinuationLink (see DiscoverRepositoriesJob.cs's own
// comment): BackgroundJob.ContinueJobWith is a static Hangfire API requiring a live
// JobStorage.Current, unreachable/unfakeable from a fast unit test. This one-method seam lets tests
// supply a fake that records "a continuation was requested for parent job X, targeting instance Y"
// without touching real Hangfire statics; HangfireSummarizationContinuationLink below (registered
// in Program.cs) is what actually runs the real call in production.
public interface ISummarizationContinuationLink
{
    void AttachAfter(string parentJobId, GenerateSummariesJob summarizationJob);
}

public class HangfireSummarizationContinuationLink : ISummarizationContinuationLink
{
    // `null!` for the PerformContext argument: same Hangfire special-case documented on
    // RecurringJob.AddOrUpdate in Program.cs and on HangfireScoringContinuationLink's own call
    // (DiscoverRepositoriesJob.cs) - Hangfire substitutes the real per-execution context at
    // invocation time regardless of what's passed here while building the expression.
    // GenerateSummariesJob.RunAsync gained this parameter as of F-009, to attach its own
    // continuation (the Trend Aggregator) the same way this method does.
    public void AttachAfter(string parentJobId, GenerateSummariesJob summarizationJob) =>
        BackgroundJob.ContinueJobWith(parentJobId, () => summarizationJob.RunAsync(null!));
}
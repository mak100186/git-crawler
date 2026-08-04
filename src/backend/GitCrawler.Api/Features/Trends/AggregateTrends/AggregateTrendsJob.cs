using Hangfire;

using Wolverine;

namespace GitCrawler.Api.Features.Trends.AggregateTrends;

// Hangfire-to-Wolverine glue, same pattern as ComputeScoresJob/GenerateSummariesJob/
// DiscoverRepositoriesJob (see those classes' header comments for why this indirection exists at
// all). Registered as scoped in Program.cs so Hangfire's AspNetCoreJobActivator can resolve its
// IMessageBus dependency per job execution.
//
// Chain position (Architecture §3, "RecurringJob + ContinueJobWith"): this is link 4 of the
// pipeline chain - GenerateSummariesJob.RunAsync attaches to this via BackgroundJob.ContinueJobWith
// once summarization finishes (see that class), rather than this having its own independent
// RecurringJob registration in Program.cs. Trend aggregation's "schedule" IS "right after
// summarization finishes", not a separate cron - same reasoning every earlier link's own header
// comment already gives for itself.
//
// No PerformContext parameter or further continuation attached here: this remains the last link in
// the ContinueJobWith chain itself (Crawler -> Scoring Engine -> Summarizer -> Trend Aggregator).
// Digest Service (F-013) is now built, but deliberately does NOT chain onto this class via a
// AggregateTrendsJob-owned continuation link the way earlier links do for each other - see
// SendDigestJob's own header comment for why an independent daily RecurringJob was chosen instead
// (this job's own firing cadence isn't daily, since GenerateSummariesJob's hourly recurring schedule
// re-triggers it far more often than once a day).
public class AggregateTrendsJob(IMessageBus messageBus)
{
    // Typed InvokeAsync<AggregateTrendsResult>, not the bare object overload: HandleAsync returns a
    // result record, and Wolverine treats a handler's return value as a cascading outgoing message
    // unless the call is a typed request/reply - with no subscriber for AggregateTrendsResult, the
    // bare overload logs "No routes can be determined for Envelope" on every run. The typed overload
    // captures the reply locally instead of trying to route it.
    public Task RunAsync() => messageBus.InvokeAsync<AggregateTrendsResult>(new AggregateTrendsCommand());
}

// Same extraction-point rationale as ISummarizationContinuationLink/IScoringContinuationLink (see
// GenerateSummariesJob.cs's own comment): BackgroundJob.ContinueJobWith is a static Hangfire API
// requiring a live JobStorage.Current, unreachable/unfakeable from a fast unit test. This
// one-method seam lets tests supply a fake that records "a continuation was requested for parent
// job X, targeting instance Y" without touching real Hangfire statics; HangfireTrendsContinuationLink
// below (registered in Program.cs) is what actually runs the real call in production.
public interface ITrendsContinuationLink
{
    void AttachAfter(string parentJobId, AggregateTrendsJob trendsJob);
}

public class HangfireTrendsContinuationLink : ITrendsContinuationLink
{
    public void AttachAfter(string parentJobId, AggregateTrendsJob trendsJob) =>
        BackgroundJob.ContinueJobWith(parentJobId, () => trendsJob.RunAsync());
}
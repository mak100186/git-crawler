using GitCrawler.Api.Features.Scoring.ComputeScores;

using Hangfire;
using Hangfire.Server;

using Wolverine;

namespace GitCrawler.Api.Features.Crawling.DiscoverRepositories;

// Hangfire's RecurringJob.AddOrUpdate targets a method-call expression (e.g. x => x.RunAsync()),
// not a Wolverine command object directly - this class exists purely to give it something concrete
// to invoke, translating that call into a Wolverine InvokeAsync so the crawl logic itself stays
// entirely owned by DiscoverRepositoriesCommandHandler (ADR-015: every pipeline stage runs as a
// Wolverine command, even when triggered by the Job Scheduler rather than an HTTP endpoint).
// Registered as scoped in Program.cs so Hangfire's AspNetCoreJobActivator can resolve its
// IMessageBus dependency per job execution, the same lifetime Wolverine itself uses for IMessageBus.
//
// Chain position (Architecture §3, "RecurringJob + ContinueJobWith"): this is link 1 of the
// pipeline chain - see Program.cs's recurring-job registration for where it's scheduled. Link 2
// (the Scoring Engine, F-007) now exists and is attached below: PerformContext is a parameter type
// Hangfire special-cases and injects automatically at execution time regardless of what's passed
// for it in the registration expression (see Program.cs), so no DI registration is needed for it.
// After InvokeAsync below completes, continuationLink (see IScoringContinuationLink further down
// this file) registers ComputeScoresJob.RunAsync to run only once this job's own state resolves to
// Succeeded - a failed/aborted crawl (see the retry exhaustion path in
// DiscoverRepositoriesCommandHandler) does not trigger a scoring pass over stale/incomplete data.
//
// Restart-safety (AC2/NFR-003): if Hangfire retries this job after a crash mid-run, re-invoking the
// command from scratch is safe as-is - DiscoverRepositoriesCommandHandler upserts by GitHubId
// (F-005, verified in that feature's review), so a full re-run neither duplicates nor corrupts
// data. No extra checkpoint/dedup logic is added here on top of that.
public class DiscoverRepositoriesJob(IMessageBus messageBus, ComputeScoresJob scoringJob, IScoringContinuationLink continuationLink)
{
    public async Task RunAsync(PerformContext context)
    {
        // Typed InvokeAsync<DiscoverRepositoriesResult>, not the bare object overload - see
        // AggregateTrendsJob.RunAsync's comment for why the bare overload logs a routing warning.
        await messageBus.InvokeAsync<DiscoverRepositoriesResult>(new DiscoverRepositoriesCommand());

        continuationLink.AttachAfter(context.BackgroundJob.Id, scoringJob);
    }
}

// BackgroundJob.ContinueJobWith is a static Hangfire API requiring a live JobStorage.Current to
// function - it isn't reachable or fakeable from a fast unit test (Task Packet's explicit testing
// constraint for this feature). This one-method seam is the extraction point: tests supply a fake
// that records "a continuation was requested for parent job X, targeting instance Y" without
// touching real Hangfire statics; HangfireScoringContinuationLink below (registered in Program.cs)
// is what actually runs the real call in production. Real end-to-end verification of the chaining
// behavior is left to the Integration Agent's live Postgres+Hangfire check.
public interface IScoringContinuationLink
{
    void AttachAfter(string parentJobId, ComputeScoresJob scoringJob);
}

public class HangfireScoringContinuationLink : IScoringContinuationLink
{
    // `null!` for the PerformContext argument: same Hangfire special-case documented on
    // RecurringJob.AddOrUpdate in Program.cs and on this file's own RunAsync signature above -
    // Hangfire substitutes the real per-execution context at invocation time regardless of what's
    // passed here while building the expression. ComputeScoresJob.RunAsync gained this parameter
    // as of F-008, to attach its own continuation (the Summarizer) the same way this method does.
    public void AttachAfter(string parentJobId, ComputeScoresJob scoringJob) =>
        BackgroundJob.ContinueJobWith(parentJobId, () => scoringJob.RunAsync(null!));
}
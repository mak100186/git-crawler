using GitCrawler.Api.Features.Summarization.GenerateSummaries;

using Hangfire;
using Hangfire.Server;

using Wolverine;

namespace GitCrawler.Api.Features.Scoring.ComputeScores;

// Hangfire-to-Wolverine glue, same pattern as DiscoverRepositoriesJob (see that class's header
// comment for why this indirection exists at all). Registered as scoped in Program.cs so
// Hangfire's AspNetCoreJobActivator can resolve its IMessageBus dependency per job execution.
//
// Chain position (Architecture §3, "RecurringJob + ContinueJobWith"): this is link 2 of the
// pipeline chain - DiscoverRepositoriesJob.RunAsync attaches to this via
// BackgroundJob.ContinueJobWith once each crawl finishes (see that class), rather than this having
// its own independent RecurringJob registration in Program.cs. Scoring's "schedule" IS "right after
// crawl finishes" (Architecture §3), not a separate cron.
//
// F-008: link 3 (the Summarizer) is attached below, exactly as this class's own header comment
// previously documented as the plan - a PerformContext parameter on RunAsync, and a
// BackgroundJob.ContinueJobWith call (via continuationLink, see IScoringContinuationLink's
// comment for why that call is behind a seam rather than a direct static call) once InvokeAsync
// completes. Only fires after a successful scoring pass, same restart-safety reasoning
// DiscoverRepositoriesJob's own header comment gives for chaining into this class: a failed/aborted
// scoring run does not trigger summarization over incomplete data.
public class ComputeScoresJob(IMessageBus messageBus, GenerateSummariesJob summarizationJob, ISummarizationContinuationLink continuationLink)
{
    // F-016/NFR-003: same overlap-window closure as every other pipeline-stage job (see
    // DiscoverRepositoriesJob.RunAsync's own comment for the full rationale, including why this is
    // applied to the method rather than the class). 30s: pure computation, no external calls (this
    // class's own header comment above) - a lock held longer than that means something is genuinely
    // stuck, not just slow I/O, so failing a concurrent trigger fast is the right call here.
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task RunAsync(PerformContext context)
    {
        // Typed InvokeAsync<ComputeScoresResult>, not the bare object overload - see
        // AggregateTrendsJob.RunAsync's comment for why the bare overload logs a routing warning.
        await messageBus.InvokeAsync<ComputeScoresResult>(new ComputeScoresCommand());

        continuationLink.AttachAfter(context.BackgroundJob.Id, summarizationJob);
    }
}
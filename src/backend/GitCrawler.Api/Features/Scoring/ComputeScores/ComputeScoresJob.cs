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
// No ContinueJobWith call of its own here yet, mirroring F-006's own restraint for this stage:
// link 3 (the Summarizer, F-008) doesn't exist. Once it does, attach it here the same way
// DiscoverRepositoriesJob attaches to this class - add a PerformContext parameter to RunAsync and
// call BackgroundJob.ContinueJobWith(context.BackgroundJob.Id, () => summarizerJob.RunAsync())
// after InvokeAsync below completes.
public class ComputeScoresJob(IMessageBus messageBus)
{
    public Task RunAsync() => messageBus.InvokeAsync(new ComputeScoresCommand());
}
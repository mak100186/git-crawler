using Wolverine;

namespace GitCrawler.Api.Features.Digest.SendDigest;

// Hangfire-to-Wolverine glue, same pattern as every other *Job class in this codebase (see
// AggregateTrendsJob.cs for the closest analog - also a terminal, no-further-continuation stage).
// Registered as scoped in Program.cs so Hangfire's AspNetCoreJobActivator can resolve its
// IMessageBus dependency per job execution.
//
// Chain position (Task Packet's explicit "decide and document" callout): NOT chain link 5 attached
// onto AggregateTrendsJob via BackgroundJob.ContinueJobWith. AggregateTrendsJob has no fixed daily
// cadence of its own to inherit - it fires every time GenerateSummariesJob's independent
// SummarizationCronSchedule RecurringJob completes (hourly by Program.cs's own default), on top of
// firing once per crawl chain too (see AggregateTrendsJob.cs's own header comment). Chaining the
// digest onto that would email the operator on every one of those runs, not once a day - directly
// contradicting this feature's own "daily email digest" requirement (Task Packet Description /
// Architecture §3's "Digest Service"). Given its own independent RecurringJob registration in
// Program.cs instead (Hangfire:DigestCronSchedule) - the same shape GenerateSummariesJob itself
// already has alongside its own chain attachment (both a continuation target AND a standalone
// recurring job, see Program.cs's own comment on that dual registration) - which decouples "when
// this stage's underlying data becomes available" from "how often the operator should be emailed
// about it".
public class SendDigestJob(IMessageBus messageBus)
{
    // Typed InvokeAsync<SendDigestResult>, not the bare object overload - see AggregateTrendsJob's
    // own RunAsync comment for why the bare overload logs a routing warning.
    public Task RunAsync() => messageBus.InvokeAsync<SendDigestResult>(new SendDigestCommand());
}
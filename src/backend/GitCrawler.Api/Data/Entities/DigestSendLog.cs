namespace GitCrawler.Api.Data.Entities;

// F-016/NFR-003: sequential-retry dedupe marker for SendDigestCommandHandler. Hangfire's own
// automatic-retry (default 10 attempts, unconfigured anywhere in this codebase) re-invokes this
// command if the process dies right after emailSender.SendAsync succeeds but before the job is
// marked Succeeded - without this, that retry would re-send the exact same day's digest.
// [DisableConcurrentExecution] (F-016 part A) only closes the *simultaneous*-overlap window
// between two truly concurrent executions; a sequential retry of an already-completed attempt is a
// different scenario entirely and needs this independent, persisted guard instead. One row per
// calendar day the digest was actually sent, written only after a confirmed successful send (see
// SendDigestCommandHandler.HandleAsync).
public class DigestSendLog
{
    public int Id { get; set; }

    public DateOnly SentForDate { get; set; }

    public DateTimeOffset SentAtUtc { get; set; }
}
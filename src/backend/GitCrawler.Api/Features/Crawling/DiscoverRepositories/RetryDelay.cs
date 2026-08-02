namespace GitCrawler.Api.Features.Crawling.DiscoverRepositories;

// Thin wrapper around Task.Delay so DiscoverRepositoriesCommandHandler's retry/backoff loop
// (F-001 spike §6) is unit-testable without a test actually blocking for real minutes/hours -
// tests substitute a fake that records requested durations instead of waiting them out.
public interface IRetryDelay
{
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken);
}

public class TaskDelayRetryDelay : IRetryDelay
{
    public Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken) =>
        duration > TimeSpan.Zero ? Task.Delay(duration, cancellationToken) : Task.CompletedTask;
}
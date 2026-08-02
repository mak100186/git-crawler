using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Crawling.DiscoverRepositories;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder. Triggered by F-006's Job Scheduler, via
// DiscoverRepositoriesJob.RunAsync's IMessageBus.InvokeAsync call (Hangfire's RecurringJob targets
// a method-call expression, not a command object directly - see that class), which is why this is
// a plain command with no HTTP endpoint, unlike Features/Diagnostics/Ping.
public record DiscoverRepositoriesCommand;

public record DiscoverRepositoriesResult(
    int DiscoveredCount,
    int UpsertedCount,
    int ContributorCountFetches,
    int ContributorCountSkipped);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class DiscoverRepositoriesCommandHandler(
    GitCrawlerDbContext dbContext,
    IGitHubDiscoveryClient discoveryClient,
    ILogger<DiscoverRepositoriesCommandHandler> logger,
    TimeProvider timeProvider,
    IRetryDelay retryDelay)
{
    // F-001 spike §7's recommended mitigation: don't refetch contributor count every crawl -
    // refresh on a slower cadence since it's a slow-moving signal. The spike suggests "roughly
    // weekly"; 7 days is the concrete choice for that.
    private static readonly TimeSpan ContributorCountFreshnessWindow = TimeSpan.FromDays(7);

    // F-001 spike §6.5: exponential backoff starting at 60s, doubling, capped - used here for any
    // transient failure that carries no rate-limit signal at all (the "if absent" case in §6).
    private static readonly TimeSpan InitialBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(30);

    // Spike §6.5's "cap total retries at 5... beyond that, abort the current run" - applied here
    // to both the discovery call and the contributor-count call, not just the page/call spike §6.5
    // originally scoped it to, since both are equally capable of failing indefinitely.
    private const int MaxGenericRetries = 5;

    public async Task<DiscoverRepositoriesResult> HandleAsync(DiscoverRepositoriesCommand command, CancellationToken cancellationToken)
    {
        var discoveredCount = 0;
        var upsertedCount = 0;
        var contributorFetches = 0;
        var contributorSkipped = 0;

        string? cursor = null;
        bool hasNextPage;

        do
        {
            var page = await FetchDiscoveryPageWithRetryAsync(cursor, cancellationToken);
            hasNextPage = page.HasNextPage;
            cursor = page.EndCursor;

            foreach (var discovered in page.Repositories)
            {
                discoveredCount++;

                // Idempotent upsert by GitHubId (not Owner+Name) - re-crawling an already-known
                // repo must update its row, not insert a duplicate or throw a unique-constraint
                // violation (F-001 spike finding; see Repository.cs / GitCrawlerDbContext.cs for
                // the schema-level enforcement this relies on).
                var existing = await dbContext.Repositories.SingleOrDefaultAsync(r => r.GitHubId == discovered.GitHubId, cancellationToken);
                if (existing is null)
                {
                    existing = new Repository { GitHubId = discovered.GitHubId };
                    dbContext.Repositories.Add(existing);
                }

                ApplyDiscoveredFields(existing, discovered);
                existing.LastCrawledAtUtc = timeProvider.GetUtcNow();

                if (NeedsContributorCountRefresh(existing))
                {
                    existing.ContributorCount = await FetchContributorCountWithRetryAsync(discovered.Owner, discovered.Name, cancellationToken);
                    existing.ContributorCountFetchedAtUtc = timeProvider.GetUtcNow();
                    contributorFetches++;
                }
                else
                {
                    contributorSkipped++;
                }

                upsertedCount++;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        while (hasNextPage);

        return new DiscoverRepositoriesResult(discoveredCount, upsertedCount, contributorFetches, contributorSkipped);
    }

    private bool NeedsContributorCountRefresh(Repository repository) =>
        repository.ContributorCountFetchedAtUtc is null
        || timeProvider.GetUtcNow() - repository.ContributorCountFetchedAtUtc.Value > ContributorCountFreshnessWindow;

    private static void ApplyDiscoveredFields(Repository entity, DiscoveredRepository discovered)
    {
        entity.Owner = discovered.Owner;
        entity.Name = discovered.Name;
        entity.Url = discovered.Url;
        entity.DefaultBranch = discovered.DefaultBranch;
        entity.PrimaryLanguage = discovered.PrimaryLanguage;
        entity.StarCount = discovered.StarCount;
        entity.ForkCount = discovered.ForkCount;
        // No license is a real, valid GraphQL result (licenseInfo: null), not an omission - stored
        // as null rather than defaulted to a placeholder, so downstream scoring (F-007) can tell
        // "no license" apart from "not yet crawled".
        entity.LicenseIdentifier = discovered.LicenseIdentifier;
        entity.LicenseName = discovered.LicenseName;
        entity.CreatedAtUtc = discovered.CreatedAtUtc;
        entity.PushedAtUtc = discovered.PushedAtUtc;
        entity.CommitCount = discovered.CommitCount;
    }

    private async Task<DiscoveryPage> FetchDiscoveryPageWithRetryAsync(string? cursor, CancellationToken cancellationToken)
    {
        var genericRetryCount = 0;
        var backoff = InitialBackoff;

        while (true)
        {
            try
            {
                return await discoveryClient.DiscoverRepositoriesAsync(cursor, cancellationToken);
            }
            catch (GitHubGraphQlRateLimitExceededException ex)
            {
                logger.LogWarning("GitHub GraphQL rate limit exhausted; waiting until {ResetAt}", ex.ResetAtUtc);
                await WaitUntilAsync(ex.ResetAtUtc, cancellationToken);
            }
            catch (GitHubSecondaryRateLimitException ex)
            {
                logger.LogWarning("GitHub secondary rate limit hit during discovery; retrying after {RetryAfter}", ex.RetryAfter);
                await retryDelay.DelayAsync(ex.RetryAfter, cancellationToken);
            }
            catch (Exception ex) when (ex is not GitHubRateLimitException)
            {
                genericRetryCount++;
                if (genericRetryCount > MaxGenericRetries)
                {
                    // Abort this run, not the whole schedule - the next Hangfire trigger (F-006)
                    // picks it up (spike §6.5).
                    logger.LogError(ex, "Discovery page fetch failed after {Retries} retries; aborting this run", MaxGenericRetries);
                    throw;
                }

                logger.LogWarning(ex, "Transient failure fetching discovery page (attempt {Attempt}); backing off {Backoff}", genericRetryCount, backoff);
                await retryDelay.DelayAsync(backoff, cancellationToken);
                backoff = NextBackoff(backoff);
            }
        }
    }

    private async Task<int> FetchContributorCountWithRetryAsync(string owner, string name, CancellationToken cancellationToken)
    {
        var genericRetryCount = 0;
        var backoff = InitialBackoff;

        while (true)
        {
            try
            {
                return await discoveryClient.GetContributorCountAsync(owner, name, cancellationToken);
            }
            catch (GitHubRestRateLimitExceededException ex)
            {
                logger.LogWarning("GitHub REST rate limit exhausted; waiting until {ResetAt}", ex.ResetAtUtc);
                await WaitUntilAsync(ex.ResetAtUtc, cancellationToken);
            }
            catch (GitHubSecondaryRateLimitException ex)
            {
                logger.LogWarning("GitHub secondary rate limit hit fetching contributor count for {Owner}/{Name}; retrying after {RetryAfter}", owner, name, ex.RetryAfter);
                await retryDelay.DelayAsync(ex.RetryAfter, cancellationToken);
            }
            catch (Exception ex) when (ex is not GitHubRateLimitException)
            {
                genericRetryCount++;
                if (genericRetryCount > MaxGenericRetries)
                {
                    logger.LogError(ex, "Contributor-count fetch for {Owner}/{Name} failed after {Retries} retries; aborting this run", owner, name, MaxGenericRetries);
                    throw;
                }

                logger.LogWarning(ex, "Transient failure fetching contributor count for {Owner}/{Name} (attempt {Attempt}); backing off {Backoff}", owner, name, genericRetryCount, backoff);
                await retryDelay.DelayAsync(backoff, cancellationToken);
                backoff = NextBackoff(backoff);
            }
        }
    }

    private async Task WaitUntilAsync(DateTimeOffset resetAtUtc, CancellationToken cancellationToken)
    {
        var wait = resetAtUtc - timeProvider.GetUtcNow();
        await retryDelay.DelayAsync(wait, cancellationToken);
    }

    private static TimeSpan NextBackoff(TimeSpan current) =>
        TimeSpan.FromSeconds(Math.Min(current.TotalSeconds * 2, MaxBackoff.TotalSeconds));
}
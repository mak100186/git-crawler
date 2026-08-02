using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

using Polly;
using Polly.Retry;

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
    TimeProvider timeProvider)
{
    // F-001 spike §7's recommended mitigation: don't refetch contributor count every crawl -
    // refresh on a slower cadence since it's a slow-moving signal. The spike suggests "roughly
    // weekly"; 7 days is the concrete choice for that.
    private static readonly TimeSpan ContributorCountFreshnessWindow = TimeSpan.FromDays(7);

    // The generic-transient pathway (any failure that carries no rate-limit signal at all, ADR-018)
    // retries twice with a flat 1-minute gap, then aborts the run - anything still failing after
    // three attempts three minutes apart is treated as not worth retrying further.
    private const int MaxGenericRetries = 2;
    private static readonly TimeSpan GenericRetryDelay = TimeSpan.FromMinutes(1);

    // ADR-018: two Polly pathways chained into one pipeline instead of the two bespoke catch/loop
    // blocks this used to be. The rate-limit pathway retries indefinitely with an exact,
    // signal-driven delay (wait until resetAt / the server-specified Retry-After); the
    // generic-transient pathway is capped and flat. GitHubContributorListUnavailableException is
    // deliberately handled by neither - see BuildResiliencePipeline.
    private readonly ResiliencePipeline resiliencePipeline = BuildResiliencePipeline(timeProvider, logger);

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
            var page = await resiliencePipeline.ExecuteAsync(
                ct => new ValueTask<DiscoveryPage>(discoveryClient.DiscoverRepositoriesAsync(cursor, ct)),
                cancellationToken);
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
                    try
                    {
                        existing.ContributorCount = await resiliencePipeline.ExecuteAsync(
                            ct => new ValueTask<int>(discoveryClient.GetContributorCountAsync(discovered.Owner, discovered.Name, ct)),
                            cancellationToken);
                        existing.ContributorCountFetchedAtUtc = timeProvider.GetUtcNow();
                        contributorFetches++;
                    }
                    catch (GitHubContributorListUnavailableException ex)
                    {
                        // Permanent for this repo (ADR-018) - stamp ContributorCountFetchedAtUtc
                        // anyway so the freshness window above stops this hitting the same wall
                        // every single crawl run, instead of just once every 7 days.
                        logger.LogWarning("{Message}", ex.Message);
                        existing.ContributorCountFetchedAtUtc = timeProvider.GetUtcNow();
                        contributorSkipped++;
                    }
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

    // ADR-018: chained Polly pipeline replacing the old hand-rolled while/catch retry loops.
    // First AddRetry = outer pathway = GitHub's own rate-limit signals (GraphQL/REST primary budget
    // exhausted, or the secondary abuse-detection limit) - retried indefinitely, since these always
    // resolve at a known, signal-provided time rather than being an open-ended failure. Second
    // AddRetry = inner pathway = anything else transient - capped, flat backoff. Neither pathway's
    // ShouldHandle matches GitHubContributorListUnavailableException, so that exception always
    // propagates straight out of ExecuteAsync on the first attempt - a deliberate non-retryable
    // pathway for GitHub's permanent "history/contributor list too large" 403 (see that exception's
    // own comment), which the handler catches at its call site instead.
    private static ResiliencePipeline BuildResiliencePipeline(TimeProvider timeProvider, ILogger logger) =>
        new ResiliencePipelineBuilder { TimeProvider = timeProvider }
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = args => ValueTask.FromResult(args.Outcome.Exception is GitHubRateLimitException),
                MaxRetryAttempts = int.MaxValue,
                DelayGenerator = args => new ValueTask<TimeSpan?>(args.Outcome.Exception switch
                {
                    GitHubGraphQlRateLimitExceededException ex => ResetDelay(ex.ResetAtUtc, timeProvider),
                    GitHubRestRateLimitExceededException ex => ResetDelay(ex.ResetAtUtc, timeProvider),
                    GitHubSecondaryRateLimitException ex => ex.RetryAfter,
                    _ => TimeSpan.Zero,
                }),
                OnRetry = args =>
                {
                    logger.LogWarning(args.Outcome.Exception, "GitHub rate limit hit; waiting {Delay} before retrying", args.RetryDelay);
                    return default;
                },
            })
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = args => ValueTask.FromResult(
                    args.Outcome.Exception is not (null or GitHubRateLimitException or GitHubContributorListUnavailableException)),
                MaxRetryAttempts = MaxGenericRetries,
                BackoffType = DelayBackoffType.Constant,
                Delay = GenericRetryDelay,
                OnRetry = args =>
                {
                    logger.LogWarning(
                        args.Outcome.Exception,
                        "Transient failure (attempt {Attempt}); retrying in {Delay}",
                        args.AttemptNumber + 1,
                        args.RetryDelay);
                    return default;
                },
            })
            .Build();

    private static TimeSpan ResetDelay(DateTimeOffset resetAtUtc, TimeProvider timeProvider)
    {
        var wait = resetAtUtc - timeProvider.GetUtcNow();
        return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
    }
}

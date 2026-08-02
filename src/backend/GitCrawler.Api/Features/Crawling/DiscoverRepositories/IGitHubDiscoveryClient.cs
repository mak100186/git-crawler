namespace GitCrawler.Api.Features.Crawling.DiscoverRepositories;

// Abstraction boundary between this slice's business logic (upsert + rate-limit retry
// orchestration, in DiscoverRepositoriesCommand.cs) and the actual GitHub GraphQL/REST calls
// (GitHubDiscoveryClient.cs) - lets the handler be unit-tested with a fake, per the Task Packet's
// "no live external calls during development" constraint. The client makes no upsert/caching
// decisions itself; it only fetches data and signals rate-limit conditions.
public interface IGitHubDiscoveryClient
{
    // Discovers one page of new/updated repos via GraphQL (ADR-004). Throws
    // GitHubGraphQlRateLimitExceededException or GitHubSecondaryRateLimitException when the
    // caller should back off before retrying - see the exception types below for what each
    // carries and how the caller is expected to react (F-001 spike §6).
    Task<DiscoveryPage> DiscoverRepositoriesAsync(string? afterCursor, CancellationToken cancellationToken);

    // REST fallback for contributor count - GraphQL has no cheap equivalent field (F-001 spike
    // §2). Callers are expected to apply the spike §7 caching cadence themselves before calling
    // this; the client always makes the REST call when invoked; it makes no caching decision.
    Task<int> GetContributorCountAsync(string owner, string name, CancellationToken cancellationToken);
}

// One page of GraphQL search results, cursor-paginated via search.pageInfo.
public record DiscoveryPage(bool HasNextPage, string? EndCursor, IReadOnlyList<DiscoveredRepository> Repositories);

// Raw fields the Crawler fetches per repo. GitHubId is GitHub's own numeric repository ID
// (GraphQL's databaseId) - the key Repository.GitHubId upserts on (see Data/Entities/Repository.cs
// for why this, not Owner+Name, is the safe upsert key). CommitCount is the cheap
// history(first:1).totalCount signal (spike §3), not a weekly rate - that derivation is F-007's
// job. Contributor count is deliberately absent here; it comes from a separate call the handler
// decides whether to make (spike §7 caching). Topics (F-010 D1) is capped at 10/repo by
// GitHubDiscoveryClient.BuildDiscoveryQuery to bound the added GraphQL point cost.
public record DiscoveredRepository(
    long GitHubId,
    string Owner,
    string Name,
    string Url,
    string DefaultBranch,
    string? PrimaryLanguage,
    int StarCount,
    int ForkCount,
    string? LicenseIdentifier,
    string? LicenseName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? PushedAtUtc,
    int? CommitCount,
    IReadOnlyList<string> Topics);

// Base for every rate-limit condition IGitHubDiscoveryClient can signal, so a generic catch clause
// elsewhere can exclude "any rate limit" in one filter, while callers still branch on the specific
// subtype for the different resume behaviors the F-001 spike §6 mandates (wait-until-resetAt vs.
// wait-exact-Retry-After).
public abstract class GitHubRateLimitException(string message) : Exception(message);

// GraphQL's primary point budget is exhausted - a GraphQL response with errors[].type ==
// "RATE_LIMITED" (HTTP 200, not a 4xx - spike §6.3). ResetAtUtc is read from a follow-up
// rateLimit-only query, which is documented as near-zero cost and so succeeds even when the main
// budget is at zero (spike §2/§6.1).
public sealed class GitHubGraphQlRateLimitExceededException(DateTimeOffset resetAtUtc)
    : GitHubRateLimitException($"GitHub GraphQL rate limit exhausted (RATE_LIMITED); resets at {resetAtUtc:O}.")
{
    public DateTimeOffset ResetAtUtc { get; } = resetAtUtc;
}

// REST's primary budget is exhausted: 403/429 with x-ratelimit-remaining: 0 (spike §6.4).
// ResetAtUtc is read directly from the x-ratelimit-reset response header.
public sealed class GitHubRestRateLimitExceededException(DateTimeOffset resetAtUtc)
    : GitHubRateLimitException($"GitHub REST rate limit exhausted (x-ratelimit-remaining: 0); resets at {resetAtUtc:O}.")
{
    public DateTimeOffset ResetAtUtc { get; } = resetAtUtc;
}

// GitHub's abuse-detection secondary limit - a 403/429 with a Retry-After header, independent of
// the primary budget and triggerable even when it isn't exhausted (spike §6.5). The wait is
// server-specified, so it's honored literally rather than run through exponential backoff.
public sealed class GitHubSecondaryRateLimitException(TimeSpan retryAfter)
    : GitHubRateLimitException($"GitHub secondary (abuse-detection) rate limit hit; retry after {retryAfter}.")
{
    public TimeSpan RetryAfter { get; } = retryAfter;
}

// Not a GitHubRateLimitException: GitHub's 403 "history or contributor list is too large to list
// contributors for this repository via the API" is permanent for a given repo, not a budget that
// resets - observed live for torvalds/linux. Retrying it (generic backoff or otherwise) always
// reproduces the exact same 403, so the handler must treat this as a one-shot skip instead of
// routing it through either retry pathway.
public sealed class GitHubContributorListUnavailableException(string owner, string name)
    : Exception($"GitHub cannot compute a contributor count for {owner}/{name} (history/contributor list too large via the API).");
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Octokit.GraphQL;
using Octokit.GraphQL.Core;
using Octokit.GraphQL.Model;

namespace GitCrawler.Api.Features.Crawling.DiscoverRepositories;

// Real GitHub access for the Crawler slice (ADR-004: GraphQL-first, REST fallback). Connection is
// injected (registered in Program.cs) rather than constructed here, so its token/lifetime setup
// happens once at startup instead of per call.
public class GitHubDiscoveryClient(
    Connection graphQlConnection,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GitHubDiscoveryClient> logger) : IGitHubDiscoveryClient
{
    // Name of the HttpClientFactory-registered client this class expects (see Program.cs) -
    // exposed as a constant so the registration and the consumer can't drift apart.
    public const string GitHubRestClientName = "GitHubRest";

    // Page size of 50 matches the F-001 spike's assumption 4 - comfortably under GraphQL's ~100
    // connection cap while keeping call count (and therefore point cost) low (spike §1/§4).
    private readonly int _pageSize = configuration.GetValue("GitHub:DiscoveryPageSize", 50);

    // "Baseline discovery criteria (age, activity, exclusions)" is named only qualitatively in
    // Architecture §1, with no concrete numbers given anywhere upstream - these are this
    // feature's own v1 default (Task Packet Constraints: "use your judgment... document your
    // choice"). A repo must have been pushed to within the lookback window (catches "new or
    // updated" without re-discovering GitHub's entire history every run), isn't a fork or
    // archived ("exclusions"), and clears a small star floor to exclude empty/toy repos ("some
    // minimal floor"). Operator-tunable via config rather than hardcoded, since it's a judgment
    // call, not a spec'd threshold - see the Task Packet's Assumptions and Deviations.
    private readonly int _lookbackDays = configuration.GetValue("GitHub:DiscoveryLookbackDays", 2);
    private readonly int _minimumStars = configuration.GetValue("GitHub:DiscoveryMinimumStars", 5);

    public async Task<DiscoveryPage> DiscoverRepositoriesAsync(string? afterCursor, CancellationToken cancellationToken)
    {
        var query = BuildDiscoveryQuery(afterCursor);

        GraphQlDiscoveryResult result;
        try
        {
            result = await graphQlConnection.Run(query, new Dictionary<string, object>(), cancellationToken);
        }
        catch (GraphQLException ex) when (IsPrimaryRateLimitError(ex))
        {
            var resetAtUtc = await FetchGraphQlResetAtAsync(cancellationToken);
            throw new GitHubGraphQlRateLimitExceededException(resetAtUtc);
        }

        // Logged unconditionally, not just near exhaustion, so the real point cost of this query
        // shape is measurable from day one - the F-001 spike's cost bracket (§3) is an estimate
        // this log is meant to replace with real numbers (spike §9 follow-up 3).
        logger.LogInformation(
            "GitHub GraphQL rateLimit after discovery page: cost={Cost} remaining={Remaining}/{Limit} resetAt={ResetAt}",
            result.RateLimit.Cost, result.RateLimit.Remaining, result.RateLimit.Limit, result.RateLimit.ResetAt);

        var repositories = result.Search.Nodes.Where(node => node is not null).Select(node => node!).ToList();

        return new DiscoveryPage(result.Search.HasNextPage, result.Search.EndCursor, repositories);
    }

    public async Task<int> GetContributorCountAsync(string owner, string name, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(GitHubRestClientName);

        // per_page=1 + reading the Link header's "last" page number is the standard cheap way to
        // get a total item count from a GitHub paginated collection without downloading every
        // page (spike §2's REST fallback endpoint: GET /repos/{owner}/{repo}/contributors).
        using var response = await client.GetAsync(
            $"repos/{owner}/{name}/contributors?per_page=1&anon=true", HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (IsRestPrimaryRateLimited(response, out var resetAtUtc))
        {
            throw new GitHubRestRateLimitExceededException(resetAtUtc);
        }

        if (IsRestSecondaryRateLimited(response, out var retryAfter))
        {
            throw new GitHubSecondaryRateLimitException(retryAfter);
        }

        if (await IsContributorListTooLargeAsync(response, cancellationToken))
        {
            throw new GitHubContributorListUnavailableException(owner, name);
        }

        response.EnsureSuccessStatusCode();

        return await ExtractContributorCountAsync(response, cancellationToken);
    }

    private ICompiledQuery<GraphQlDiscoveryResult> BuildDiscoveryQuery(string? afterCursor)
    {
        var searchQuery = BuildSearchQuery();

        // Field-to-need mapping and null-guards below match the F-001 spike §2 query shape
        // exactly (license, fork count, star count, commit count via the cheap
        // history(first:1).totalCount trick) - verified to compile and produce the expected
        // GraphQL document against the actual Octokit.GraphQL 0.4.0-beta package before landing
        // here (see this feature's Assumptions and Deviations for why that verification mattered:
        // this is the first slice in the codebase to use this beta library). Ternary null-guards
        // on DefaultBranchRef/PrimaryLanguage/LicenseInfo are required, not defensive
        // over-caution - GitHub returns null for all three on legitimate repos (e.g. an empty
        // repo has no default branch; many repos have no detected primary language or no
        // license), and Octokit.GraphQL needs the guard expressed in the query tree itself so it
        // knows to skip the nested selection when the server returns null for that field.
        return new Query()
            .Select(q => new GraphQlDiscoveryResult(
                // Queried alongside discovery, not separately, so every call always carries its
                // own remaining-budget snapshot (spike §6.1 pre-flight/tracking mechanism).
                q.RateLimit(null).Select(rl => new GraphQlRateLimitInfo(rl.Cost, rl.Remaining, rl.Limit, rl.ResetAt)).Single(),
                q.Search(searchQuery, SearchType.Repository, _pageSize, afterCursor, null, null)
                    .Select(connection => new GraphQlSearchPage(
                        connection.PageInfo.HasNextPage,
                        connection.PageInfo.EndCursor,
                        connection.Nodes.Select(node => node.Switch<DiscoveredRepository?>(when => when
                            .Repository(repo => new DiscoveredRepository(
                                repo.DatabaseId!.Value,
                                repo.Owner.Login,
                                repo.Name,
                                repo.Url,
                                repo.DefaultBranchRef != null ? repo.DefaultBranchRef.Name : "",
                                repo.PrimaryLanguage != null ? repo.PrimaryLanguage.Name : null,
                                repo.StargazerCount,
                                repo.ForkCount,
                                repo.LicenseInfo != null ? repo.LicenseInfo.SpdxId : null,
                                repo.LicenseInfo != null ? repo.LicenseInfo.Name : null,
                                repo.CreatedAt,
                                repo.PushedAt,
                                repo.DefaultBranchRef != null
                                    ? repo.DefaultBranchRef.Target.Cast<Commit>().History(1, null, null, null, null, null, null).TotalCount
                                    : (int?)null
                            ))
                        )).ToList()
                    )).Single()
            ))
            .Compile();
    }

    private string BuildSearchQuery()
    {
        var since = DateTimeOffset.UtcNow.AddDays(-_lookbackDays).ToString("yyyy-MM-dd");
        return $"pushed:>={since} stars:>={_minimumStars} fork:false archived:false";
    }

    private async Task<DateTimeOffset> FetchGraphQlResetAtAsync(CancellationToken cancellationToken)
    {
        var rateLimitOnlyQuery = new Query().Select(q => q.RateLimit(null).Select(rl => rl.ResetAt).Single()).Compile();
        return await graphQlConnection.Run(rateLimitOnlyQuery, new Dictionary<string, object>(), cancellationToken);
    }

    // Octokit.GraphQL 0.4.0-beta's GraphQLException exposes only a flattened message string, not
    // the response's structured errors[] array (confirmed by reflecting over the installed
    // package - no errors/type property exists on the exception type in this version). Matching
    // the documented GitHub error-type token in that message is the best signal available without
    // hand-rolling the HTTP/JSON layer ourselves. If GitHub or a future Octokit.GraphQL version
    // changes this message format, this heuristic silently stops matching and a RATE_LIMITED
    // condition would fall through to the generic exponential-backoff path in the handler instead
    // of the precise resetAt wait - a correctness risk worth flagging, not a crash risk (see this
    // feature's Assumptions and Deviations).
    private static bool IsPrimaryRateLimitError(GraphQLException ex) =>
        ex.Message.Contains("RATE_LIMITED", StringComparison.OrdinalIgnoreCase);

    private static bool IsRestPrimaryRateLimited(HttpResponseMessage response, out DateTimeOffset resetAtUtc)
    {
        resetAtUtc = default;

        if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
        {
            return false;
        }

        if (!response.Headers.TryGetValues("x-ratelimit-remaining", out var remainingValues)
            || remainingValues.FirstOrDefault() != "0")
        {
            return false;
        }

        if (response.Headers.TryGetValues("x-ratelimit-reset", out var resetValues)
            && long.TryParse(resetValues.FirstOrDefault(), out var resetUnixSeconds))
        {
            resetAtUtc = DateTimeOffset.FromUnixTimeSeconds(resetUnixSeconds);
            return true;
        }

        // remaining:0 without a paired reset timestamp violates GitHub's documented header
        // contract (spike §6.4 assumes both are present together) - fall through to the generic
        // backoff path rather than fabricate a resume time.
        return false;
    }

    // GitHub returns this as a plain 403 with no distinguishing header (unlike the primary/secondary
    // rate-limit cases above), so the only signal available is this documented, fixed message in the
    // response body - confirmed live against torvalds/linux/contributors. It's the same shape of
    // message-substring heuristic already used by IsPrimaryRateLimitError below.
    private static async Task<bool> IsContributorListTooLargeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode is not HttpStatusCode.Forbidden)
        {
            return false;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return body.Contains("too large to list contributors", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRestSecondaryRateLimited(HttpResponseMessage response, out TimeSpan retryAfter)
    {
        retryAfter = default;

        if (response.StatusCode is not (HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests))
        {
            return false;
        }

        if (response.Headers.RetryAfter?.Delta is { } delta)
        {
            retryAfter = delta;
            return true;
        }

        if (response.Headers.RetryAfter?.Date is { } date)
        {
            retryAfter = date - DateTimeOffset.UtcNow;
            return true;
        }

        return false;
    }

    private static async Task<int> ExtractContributorCountAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Headers.TryGetValues("Link", out var linkValues)
            && TryGetLastPageFromLinkHeader(linkValues.First()) is { } lastPage)
        {
            return lastPage;
        }

        // No Link header means the whole (small) collection fit on one page - count it directly
        // rather than assume "1"; a repo can legitimately have 0 contributors (e.g. an
        // anonymous-only commit history).
        var contributors = await response.Content.ReadFromJsonAsync<List<JsonElement>>(cancellationToken);
        return contributors?.Count ?? 0;
    }

    // Link: <https://api.github.com/...&page=2>; rel="next", <...&page=14>; rel="last"
    private static int? TryGetLastPageFromLinkHeader(string linkHeader)
    {
        foreach (var part in linkHeader.Split(','))
        {
            if (!part.Contains("rel=\"last\"", StringComparison.Ordinal))
            {
                continue;
            }

            var pageIndex = part.IndexOf("page=", StringComparison.Ordinal);
            if (pageIndex < 0)
            {
                continue;
            }

            var digits = new string(part[(pageIndex + "page=".Length)..].TakeWhile(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var page))
            {
                return page;
            }
        }

        return null;
    }

    // Internal-to-this-file shapes the compiled GraphQL query deserializes into - not part of
    // IGitHubDiscoveryClient's public contract (DiscoveredRepository is).
    private sealed record GraphQlRateLimitInfo(int Cost, int Remaining, int Limit, DateTimeOffset ResetAt);

    private sealed record GraphQlSearchPage(bool HasNextPage, string? EndCursor, IReadOnlyList<DiscoveredRepository?> Nodes);

    private sealed record GraphQlDiscoveryResult(GraphQlRateLimitInfo RateLimit, GraphQlSearchPage Search);
}
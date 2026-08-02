# ADR-018: Polly for the Crawler's GitHub Retry/Resilience Pathways

> Status: ACCEPTED
> Date: 2026-08-02
> Architecture: docs/architecture.md (v12)

## Context

F-001's spike (§6) specified four distinct failure pathways `DiscoverRepositoriesCommandHandler`
must handle when calling GitHub: GraphQL primary rate limit (wait until a server-provided
`resetAt`), REST primary rate limit (same, from a response header), the abuse-detection secondary
limit (wait an exact server-provided `Retry-After`), and a catch-all "no rate-limit signal at all"
case (exponential backoff, capped retries, then abort the run). The original implementation was a
hand-rolled `while`/`try`/`catch` loop per call site, backed by a bespoke `IRetryDelay` abstraction
whose only job was making the loop's `Task.Delay` calls fakeable in tests.

Live-verifying the crawler against real GitHub data after fixing an unrelated query-building bug
(a `NullReferenceException` in `GitHubDiscoveryClient.BuildDiscoveryQuery`) surfaced a fifth,
previously unseen case: fetching the contributor count for `torvalds/linux` returns a permanent
`403` — `"The history or contributor list is too large to list contributors for this repository
via the API"` — a real, documented GitHub API limitation for repos whose history is too large to
compute this over, not a rate limit and not transient. The catch-all pathway's `when (ex is not
GitHubRateLimitException)` filter had no way to distinguish this from a genuine transient failure,
so it retried this permanently-failing call on the same exponential-backoff schedule as everything
else, then aborted the entire crawl run once retries were exhausted — silently dropping every repo
queued after `torvalds/linux` in that page, not just the one repo that can never succeed.

Two problems needed fixing together: the missing "this is permanent, don't retry it" pathway, and
the growing awkwardness of expressing five branching wait/retry behaviors as nested
`try`/`catch`/`while` blocks by hand. `Polly.Core` was already present in the dependency graph
(pulled in transitively, pinned at 8.6.5) but never referenced directly — nothing in this codebase
used it.

## Decision

`DiscoverRepositoriesCommandHandler` now builds a Polly `ResiliencePipeline` from two chained
`AddRetry` strategies instead of hand-rolled loops:

1. **Rate-limit pathway** (outer): matches any `GitHubRateLimitException` subtype
   (`GitHubGraphQlRateLimitExceededException`, `GitHubRestRateLimitExceededException`,
   `GitHubSecondaryRateLimitException`). Retries indefinitely (`MaxRetryAttempts = int.MaxValue`) —
   these always resolve at a signal-provided time, so there's no reason to cap them — with a
   `DelayGenerator` that computes the exact wait from each exception's `ResetAtUtc` or `RetryAfter`.
2. **Generic-transient pathway** (inner): matches anything else. Retries twice
   (`MaxRetryAttempts = 2`) with a flat 1-minute gap (`DelayBackoffType.Constant`) — reduced from the
   original exponential-backoff-to-5-retries scheme, since a third failure a minute apart is no
   longer treated as worth retrying further.

A new exception, `GitHubContributorListUnavailableException`, represents GitHub's permanent
"history/contributor list too large" 403. It deliberately matches neither pathway's `ShouldHandle`
predicate, so it always propagates out of `ResiliencePipeline.ExecuteAsync` on the first attempt —
a genuine non-retryable pathway rather than a variant of the generic one. The handler catches it at
the one call site that can throw it (the contributor-count fetch), logs a warning, and treats that
repo's contributor count as unavailable for this run rather than aborting the whole crawl. It still
stamps `ContributorCountFetchedAtUtc`, so the existing 7-day freshness window keeps this from
re-attempting (and re-failing) the same permanently-blocked repo on every single crawl cycle.

The bespoke `IRetryDelay` abstraction is removed. The handler's existing `TimeProvider` dependency
now also backs the Polly pipeline directly (`ResiliencePipelineBuilder.TimeProvider`), which supplies
both "now" (for the rate-limit `DelayGenerator` math) and the pipeline's own retry-delay timer — one
injected clock doing both jobs instead of two separate seams.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Keep the hand-rolled loops; just add an `if (Is403TooLarge(ex)) throw;`-style early-exit for the new case | Fixes the immediate bug but does nothing about the underlying awkwardness (five branching wait behaviors expressed as nested `try`/`catch`/`while`) that made the missing pathway easy to miss in the first place. |
| A single Polly retry strategy with one `ShouldHandle`/`DelayGenerator` handling all cases via a big `switch` | Conflates two behaviors that are genuinely different: rate limits are unbounded-but-signal-resolved, generic transients are capped-but-unsignaled. Splitting them into two chained strategies keeps each one's `ShouldHandle`/cap/delay simple and independently reasoned about, matching how the spike itself described them as separate cases. |
| `Microsoft.Extensions.Http.Resilience`'s standard resilience handler (attached to the named `HttpClient` itself) | Operates at the HTTP layer, so it can't see the GraphQL client's `GraphQLException`-wrapped rate-limit signal (Octokit.GraphQL, not raw `HttpClient`, makes that call) or carry the exact `resetAt`/`Retry-After` values through to the delay calculation — the handler-level exception types this ADR retries on exist specifically because GitHub's signal is richer than an HTTP status code. |

## Consequences

- `IRetryDelay`/`TaskDelayRetryDelay` deleted; the handler's constructor drops that parameter. Tests
  substitute a `TimeProvider` fake instead (see below) rather than a separate delay fake.
- `Polly.Core` is now a direct `PackageReference` in `GitCrawler.Api.csproj` (was transitive-only),
  pinned at 8.6.5 to match what was already resolving.
- Generic-transient retries are now capped at 2 attempts with a flat 1-minute gap (was 5 attempts,
  exponential 60s→30min). A failure that survives 3 attempts across ~2 minutes is no longer retried
  further within the same run — the next scheduled crawl (`discover-repositories`, daily) picks it
  up instead of this run holding a Hangfire worker for up to ~31 minutes on a call that's unlikely to
  recover.
- `GitHubContributorListUnavailableException` is a new, permanent (non-`GitHubRateLimitException`)
  exception type. Any future case that's "permanent for this repo, don't retry" should follow the
  same shape: a distinct exception type excluded from both `ShouldHandle` predicates, caught at the
  specific call site that can throw it.
- Test fakes changed: `FakeTimeProvider` (`Fakes.cs`) now also overrides `CreateTimer` to record the
  duration Polly's pipeline actually requested and fire it near-instantly instead of waiting real
  time — this is what `FakeRetryDelay` used to do, now folded into the same fake that already
  supplied "now". `FakeRetryDelay` is deleted.
- `DiscoverRepositoriesCommandHandlerTests.cs` updated: the exponential-backoff test now asserts a
  flat 1-minute gap for 2 retries; the max-retries test now needs 3 consecutive failures (not 6) to
  exceed the new, lower cap; a new test covers the permanent-403 pathway skipping without retry and
  still stamping `ContributorCountFetchedAtUtc`.

## Related

- Architecture section: 3. Components → Crawler / Ingestion; 7. Technology Decisions
- Amends: none (first ADR covering the Crawler's retry/resilience implementation specifically —
  F-001's spike §6 described the required behavior, but no prior ADR recorded the library/pattern
  choice)
- Supersedes: none

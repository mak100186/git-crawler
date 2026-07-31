# Spike F-001: GitHub GraphQL Rate-Limit Budget Validation

> Status: COMPLETE
> Date: 2026-07-31
> Feature: F-001 (Phase 0)
> Resolves (conditionally): Architecture risk A1 (docs/architecture.md §8)
> Related: ADR-004 (GraphQL-first GitHub API access), NFR-004 (100k+ scale-out)

## 0. Confidence Disclaimer

The numeric rate-limit figures and cost-formula behavior below are drawn from GitHub's publicly
documented GraphQL/REST rate-limit model **as of this assistant's training knowledge cutoff**.
GitHub has changed these numbers before (e.g. differentiating GitHub App vs. PAT budgets) and may
change them again. **None of the figures in this document should be treated as authoritative for
production budgeting until verified against live API responses** — specifically the `rateLimit`
object returned by the GraphQL API itself (`cost`, `limit`, `remaining`, `resetAt`) and the REST
API's `x-ratelimit-*` response headers. Everywhere a specific number is used, it is marked as
either **(documented, high confidence)** or **(estimated, needs live verification)**.

## 1. Assumptions Stated Up Front

The architecture doc does not pin an exact discovery query shape, auth mechanism, or page size, so
this spike defines one to model against (per the Task Packet's "you may design a concrete query
shape" allowance):

1. **Auth mechanism:** a single Personal Access Token (classic or fine-grained), not a GitHub App
   installation token. Basis: NFR-002 says "GitHub API token stored via environment/secrets
   configuration" — singular token, no mention of app/installation machinery, and the platform is
   explicitly a single-operator system (Architecture §1). GitHub App installation budgets scale
   differently (higher, tied to installed repo/user count) — if a GitHub App is adopted later, the
   budget picture in this doc should be re-checked, not assumed to carry over unchanged.
2. **Primary rate-limit budget:** 5,000 points/hour for the GraphQL API under PAT auth **(documented,
   high confidence — this has been GitHub's long-standing published figure, matching the classic
   REST 5,000 requests/hour budget, but confirm against a live `rateLimit` query before relying on
   it)**.
3. **Discovery cadence:** one scheduled crawl run per day (Architecture §2: "each pipeline stage …
   triggered on its own schedule"), executed as a single bounded job rather than continuously
   spread across 24 hours. This is the conservative assumption for budget pressure — a burst
   concentrated into a short window is the worst case for hitting the hourly ceiling.
4. **Page size:** `first: 50` per paginated `search` connection. Chosen as a moderate value — large
   enough to keep call count low, well under GitHub's typical 100-per-connection page cap
   **(documented, high confidence that GraphQL connections cap `first`/`last` at 100 — exact cap
   should still be confirmed live, as some connection types differ)**.
5. **Scope interpretation of "100k+" (NFR-004 / PRD processing-volume assumption):** the PRD/NFR-004
   language ("scaling toward 100k+ repositories … over time") is ambiguous between (a) cumulative
   repository count in the Data Store, built up incrementally at the existing 1K-5K/day discovery
   rate, and (b) a literal 100k+ *repos discovered in a single day* (e.g., an initial backfill).
   This is a real ambiguity, not a nit — the two interpretations have very different rate-limit
   consequences. §5 below evaluates both explicitly rather than silently picking one.

## 2. Discovery Query Shape

```graphql
query DiscoverRepos($searchQuery: String!, $after: String) {
  rateLimit {
    cost
    remaining
    limit
    resetAt
  }
  search(query: $searchQuery, type: REPOSITORY, first: 50, after: $after) {
    repositoryCount
    pageInfo {
      hasNextPage
      endCursor
    }
    nodes {
      ... on Repository {
        nameWithOwner
        url
        description
        createdAt
        pushedAt
        isFork
        isArchived
        primaryLanguage { name }
        licenseInfo { spdxId name }
        stargazerCount
        forkCount
        repositoryTopics(first: 10) {
          nodes { topic { name } }
        }
        defaultBranchRef {
          target {
            ... on Commit {
              history(first: 1) { totalCount }
            }
          }
        }
      }
    }
  }
}
```

Field-to-need mapping (Architecture §3 Crawler + PRD scoring signals — license, commits-per-week,
contributor count, fork count):

| Signal needed | Source in query above | Notes |
|---|---|---|
| License presence/type | `licenseInfo { spdxId name }` | Covered by GraphQL directly. |
| Fork count | `forkCount` | Covered by GraphQL directly. |
| Popularity signal | `stargazerCount` | Covered by GraphQL directly. |
| Recency/activity proxy | `pushedAt`, `defaultBranchRef.target.history(first:1).totalCount` | `totalCount` on a `first:1` history connection is a documented cheap way to get a commit count without paginating full history; weekly commit *rate* needs either repeated sampling over time or the REST commit-activity statistics endpoint. |
| Topics/language (filter/sort, FR-004) | `repositoryTopics`, `primaryLanguage` | Covered by GraphQL directly. |
| **Contributor count** | **Not present** | GraphQL's `Repository` type has no direct, cheap "contributor count" field. The accurate source is the REST `GET /repos/{owner}/{repo}/contributors` (or `/stats/contributors`) endpoint — which is exactly the kind of "certain commit-activity statistics endpoint" ADR-004 already calls out as the REST-fallback case, and which is also documented to sometimes return `202 Accepted` while GitHub computes results asynchronously. This is a second, independent rate-limit budget (REST's 5,000 req/hour) and is analyzed separately in §4. |

## 3. Point-Cost Model

GitHub's GraphQL point-cost model charges per query based on the number of objects the query
*could* return across all connections, not per HTTP call **(documented, high confidence on the
general shape of this model)**. The exact numeric formula/coefficients GitHub uses internally are
not something this document reproduces with confidence — GitHub's own guidance is to read the
`cost` value the API returns for your actual query shape rather than hand-derive it, and this spike
follows that guidance rather than inventing a precise formula.

Instead of a single fabricated number, this spike brackets the per-call cost with three scenarios,
all plausible for a `search(first: 50)` query with two shallow nested connections
(`repositoryTopics(first: 10)`, `history(first: 1)`):

| Scenario | Cost per call (50 repos) | Basis |
|---|---|---|
| Best case | 1 point | GitHub charges a minimum of ~1 point for a call whose connections resolve to small/paginated-once result sets; shallow nested connections like these are commonly reported to add little beyond the top-level connection cost. |
| Mid estimate | 5 points | A defensible middle estimate accounting for the two nested connections each contributing a small multiplier on top of the base search cost. |
| Worst case (pessimistic) | 20 points | Conservative upper bound assuming nested connections cost more than typically reported, used to stress-test whether the budget still holds under a pessimistic reading. |

**This bracket is an estimate, not a measurement.** The actual number must be read from the
`rateLimit { cost }` field this query already requests — that field returns the *exact* cost of the
call it accompanies, live, with no ambiguity. The Crawler implementation (F-005) should log this
value on every call from day one; the budget table below should be recalculated against real
numbers before Phase 1 sign-off, not left on this spike's estimate.

## 4. Budget Table — 1,000/day and 5,000/day (TC-001-01)

Assumes: 50 repos/page → calls/day = repos/day ÷ 50. Assumes the daily crawl executes as a single
burst within roughly one hourly rate-limit window (§1 assumption 3 — the conservative case).

### GraphQL discovery query

| Volume | Calls/day | Cost/day (best, 1 pt/call) | Cost/day (mid, 5 pt/call) | Cost/day (worst, 20 pt/call) | Hourly budget | Headroom (worst case) |
|---|---|---|---|---|---|---|
| 1,000 repos/day | 20 | 20 | 100 | 400 | 5,000 | 4,600 pts / **92% headroom** |
| 5,000 repos/day | 100 | 100 | 500 | 2,000 | 5,000 | 3,000 pts / **60% headroom** |

**Verdict for GraphQL at 1K-5K/day: comfortable headroom under every scenario, including the
pessimistic bracket.** No mitigation required for the GraphQL discovery query at this volume.

### REST fallback (contributor count, per repo)

REST budget is a **separate** 5,000 requests/hour pool from GraphQL's points **(documented, high
confidence that the two budgets are tracked independently)**. If contributor count is fetched via
one REST call per newly-discovered repo:

| Volume | REST calls/day | Hourly REST budget | Fits in one burst hour? | Headroom / Deficit |
|---|---|---|---|---|
| 1,000 repos/day | 1,000 | 5,000 | Yes | 4,000 requests / **80% headroom** |
| 5,000 repos/day | 5,000 | 5,000 | **Exactly at the ceiling** | **0 headroom — a single retry, secondary-limit hit, or any other REST call in the same window causes a deficit** |

**This is the first real finding of this spike:** at the top of the PRD's stated volume range,
the REST fallback for contributor count — not the GraphQL discovery query — is the binding
constraint, and it has zero margin for error. See §6 for the recommended mitigation.

## 5. Scale-Out Assessment — 100k+ (NFR-004) (TC-001-02)

Evaluated under both readings from assumption 5:

**Reading (a) — cumulative store size, steady 1K-5K/day incremental discovery rate.**
The daily crawl volume never actually changes; only the cumulative row count in PostgreSQL grows
toward 100k+ over time. Under this reading, **the current query shape holds indefinitely** — the
per-run cost is governed by §4's numbers regardless of how large the Data Store has grown, since
discovery only touches new/updated repos per run, not the whole historical set. This reading is
also consistent with NFR-004's own wording, which frames 100k+ as a **schema/indexing** concern
("Schema and indexing support 100k+ repositories … without a redesign"), not a per-run throughput
target.

**Reading (b) — a literal 100k+ repos discovered/processed in one day** (e.g., a one-time backfill
or a much more aggressive future discovery scope):

| Volume | GraphQL calls/day | Cost/day (best/mid/worst) | Hourly windows needed (worst case) | REST calls/day (contributor count) | Hourly windows needed (REST) |
|---|---|---|---|---|---|
| 100,000 repos/day | 2,000 | 2,000 / 10,000 / 40,000 | **8 hourly windows** (40,000 ÷ 5,000) | 100,000 | **20 hourly windows** (100,000 ÷ 5,000) |

**Explicit statement (required by TC-001-02): the current query shape does NOT hold, as a
single-burst run, at a literal 100k+/day volume — for either the GraphQL query (mid/worst-case
brackets) or, more severely, the REST contributor-count fallback.** At 100k/day, REST alone needs
on the order of a full day's worth of hourly windows spent, which is incompatible with same-day
processing unless the query strategy changes. This is an explicit, not silent, finding.

**What would need to change if reading (b) ever becomes real** (see §6 for the recommended default
mitigation, which is worth adopting regardless of which reading turns out to be true):
- Pace/throttle the crawl across multiple hourly windows instead of one burst (turns a hard ceiling
  into a multi-hour job — mechanically simple, no query redesign).
- Reduce or eliminate the per-repo REST contributor-count call (§6) — this is the larger lever,
  since REST is the binding constraint at scale, not GraphQL.
- Increase page size toward the ~100 cap to reduce call count (marginal — cost tracks nodes
  requested, not call count, so this mainly reduces network/HTTP overhead, not point cost).

## 6. Back-Off / Retry Strategy (TC-001-03)

Concrete mechanism, not "retry later":

1. **Pre-flight budget check.** Before starting a batch of calls, query `rateLimit { remaining,
   resetAt }` (this field is documented to be free/near-zero cost to query on its own). If
   `remaining` is less than the next call's expected cost plus a safety margin (recommend 10% of
   the hourly limit, i.e. 500 points), pause the run until `resetAt` rather than attempting the
   call and failing.
2. **Per-call cost tracking.** Read the `rateLimit.cost` field returned alongside every actual
   query response and log it (feeds NFR-005 observability and closes the loop on the §3 estimate
   vs. reality gap).
3. **Primary limit exhaustion (GraphQL).** If a query response's `errors[]` array contains a
   rate-limit-type error (GitHub returns a normal `200` with a GraphQL-level error object for this,
   not a `403`), stop issuing further calls for the remainder of this run, sleep until `resetAt`
   (read from the last successful `rateLimit` response), then resume.
4. **Primary limit exhaustion (REST).** A `403`/`429` response with an `x-ratelimit-remaining: 0`
   header means the primary REST budget is exhausted; sleep until the Unix timestamp in
   `x-ratelimit-reset`, then resume.
5. **Secondary rate limits (abuse-detection, both APIs).** GitHub enforces a separate,
   independently-triggered secondary limit (e.g., too many concurrent requests, too rapid a burst)
   that can return a `403`/`429` even when the primary budget isn't exhausted **(documented, high
   confidence that secondary limits exist and are distinct from the primary budget; the exact
   default wait-time GitHub recommends when no `Retry-After` header is present should be confirmed
   live rather than trusted from this document)**. Mechanism:
   - If a `Retry-After` header is present, sleep exactly that many seconds, then retry.
   - If absent, apply exponential backoff starting at 60 seconds, doubling each retry, capped at a
     15-minute ceiling, with random jitter (±20%) to avoid synchronized retry storms.
   - Cap total retries at 5 for a single page/call; beyond that, abort the current run (not the
     whole schedule) and let the next scheduled Hangfire trigger (Architecture §3 Job Scheduler)
     pick it up.
6. **Resumability.** Persist the `pageInfo.endCursor` and per-page processing state to the Data
   Store as each page completes, not only at run end — this is what makes step 5's "abort and let
   the next trigger resume" safe and non-duplicating, directly implementing NFR-003 ("every
   pipeline stage is idempotent and resumable after a container restart mid-run").

## 7. Recommended Mitigation — Contributor Count (applies regardless of scale reading)

Since §4 and §5 both identify the REST contributor-count fallback as the tighter constraint (zero
headroom at 5,000/day; the dominant blocker at a literal 100k/day), the recommended default
mitigation — independent of which NFR-004 reading turns out to be correct — is:

- **Don't fetch contributor count for every discovered repo on every crawl.** Fetch it only once
  per repo (on first discovery) and refresh on a slower cadence (e.g. weekly) rather than daily,
  since contributor count is a slow-moving signal. This turns "1 REST call per repo per day" into
  "1 REST call per repo per week," cutting REST volume by roughly 7x for already-seen repos and
  removing the zero-headroom condition found in §4.
- If REST volume is still a concern at higher scale, consider `mentionableUsers.totalCount` (a
  GraphQL-native field, cheap to add to the existing query) as an imperfect but zero-extra-cost
  proxy to pre-filter which repos are worth the REST call for exact contributor count, rather than
  calling REST for every discovered repo unconditionally.

This is a design recommendation for F-005 (GitHub Crawler), not something this spike implements.

## 8. Resolution Verdict on Risk A1

| Scope | Verdict |
|---|---|
| 1,000-5,000 repos/day (PRD volume target) | **Resolved.** GraphQL discovery query holds with wide headroom under every cost-estimate scenario in §4. REST contributor-count fallback holds at 1,000/day but has zero headroom at 5,000/day at a naive "one REST call per repo per crawl" design — mitigated by the caching/refresh-cadence change in §7, which this spike recommends adopting as part of F-005 rather than leaving as a residual risk. |
| 100k+ scale-out (NFR-004) | **Conditionally resolved / mitigation adopted, not unconditionally resolved.** Under the reading that 100k+ refers to cumulative Data Store size with unchanged daily discovery volume (§5 reading (a), and the reading this spike considers best-supported by NFR-004's own "schema and indexing" framing), the budget holds indefinitely with no further change. Under the literal "100k+/day" reading (§5 reading (b)), the budget does **not** hold as a single-burst run — the required mitigation (multi-window pacing, plus the §7 REST reduction) is identified but not yet implemented, since no code exists yet in this Phase 0 spike. |
| Overall | Sufficient to unblock F-005 (GitHub Crawler) design and implementation. **Follow-up (out of scope for this spike, flagged for the orchestrator/human):** update Architecture §8's risk register to mark A1 resolved-with-conditions, referencing this document, and confirm with the Architecture owner (Maxx) which NFR-004 reading — (a) or (b) — is the intended one, since it changes whether §5/§7's mitigations are "nice to have" or "must build before any 100k-scale run." |

## 9. Follow-Ups (Not in Scope for This Spike)

1. Architecture doc's own risk register (§8, row A1) update — noted per Task Packet's own
   out-of-scope instruction; this spike's verdict (§8) is the input for that edit, not the edit
   itself.
2. Confirm the NFR-004 100k+ reading (cumulative vs. literal daily volume) with the Architecture
   owner — §5/§8 flag this as unresolved ambiguity with real consequences.
3. Once F-005 (GitHub Crawler) exists and a real GitHub token is available, replace every
   "(estimated, needs live verification)" figure in §3-§5 with measured values from actual
   `rateLimit` responses, and re-run this budget table with real numbers.
4. Implement the §7 contributor-count caching/refresh-cadence design as part of F-005, not as a
   later hardening pass — §4 shows it's needed at the top of the stated volume range (5,000/day),
   not only at exotic scale.

## Version History
| Version | Date | Change | Triggered By |
|---|---|---|---|
| v1 | 2026-07-31 | Initial spike output | F-001 Task Packet |

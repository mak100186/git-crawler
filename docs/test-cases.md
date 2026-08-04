# Test Cases: GitHub Hidden Gems Discovery Platform

> Status: ACTIVE
> Version: v14
> Last updated: 2026-08-04
> Covers: Phase 0 (F-001, F-002, F-003), Phase 1 (F-004, F-005, F-006, F-007), Phase 2 (F-008, F-009, F-018), Phase 3 (F-010, F-011, F-012 so far)
> Source of truth for acceptance criteria: docs/project-management.md

Scenarios are added per phase as features are scoped. Each scenario maps to one or more PMBook
Acceptance Criteria and is meant to be concretely executable by the Integration Agent (or manually,
if a step requires an external system the agent can't reach, e.g. a real GitHub token or a running
LM Studio instance — those steps are marked **Manual**).

---

## F-001 — Spike: GitHub GraphQL rate-limit budget validation

### TC-001-01 (Happy path) — Point-cost budget computed and documented
1. Run the point-cost model / calculator produced by the spike against a simulated discovery query
   sized for 1,000 repos/day (low end of the FR-001 target range).
2. Repeat for 5,000 repos/day (high end).
3. **Expect:** a written budget table (points consumed vs. GitHub's hourly/points-per-minute limit)
   exists in the spike's output artifact, for both volumes, with headroom or deficit stated
   explicitly.

### TC-001-02 (Edge case) — Scale-out target (100k+ repos)
1. Extrapolate the same point-cost model to the 100k+ repos scale-out target from NFR-004.
2. **Expect:** the spike states whether the current query shape holds at that volume or requires a
   different pagination/query strategy — this must be an explicit statement, not silence.

### TC-001-03 (Regression-sensitive) — Rate-limit exhaustion behavior
1. **Manual/simulated:** Using a real or mocked GitHub API response for a `403`/rate-limit-exceeded
   response (GraphQL cost or REST secondary rate limit), verify the back-off strategy documented by
   the spike actually specifies a concrete wait/retry mechanism (not just "retry later").
2. **Expect:** risk A1 (Architecture §8) is marked resolved in the spike's output, or an explicit
   mitigation is proposed if the budget doesn't hold at target volume.

---

## F-002 — Spike: LM Studio inference throughput benchmark

### TC-002-01 (Happy path) — Model availability confirmed
1. **Manual:** Query LM Studio's local catalog/API for the configured summarization model
   (originally Gemma 4 E4B per ADR-013; superseded 2026-08-01 by Llama 3.2 3B Instruct per
   ADR-017 — see `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9-§10 for why).
2. **Expect:** the spike's output states the exact model identifier and quantization actually
   loaded and available, or explicitly states it is unavailable (per the handoff doc's caveat that
   this identifier postdates verifiable training data).

### TC-002-02 (Happy path) — Throughput benchmark against NFR-001
1. **Manual:** Run a summary-generation request against LM Studio's local API for a representative
   repository README (~1-3 KB of content).
2. Measure wall-clock time from request to completed summary.
3. **Expect:** the measured time is compared explicitly against NFR-001's "on the order of seconds
   per repository" target, and the spike states pass/fail against that target.

### TC-002-03 (Edge case) — Model unavailable or underperforming
1. If TC-002-01 finds the model unavailable, or TC-002-02 shows throughput far outside the NFR-001
   target: **expect** the spike's output explicitly recommends revisiting ADR-013 or NFR-001,
   per F-002's acceptance criteria, rather than silently proceeding.

### TC-002-04 (Regression-sensitive) — Repeatability
1. Run the same benchmark request 3 times in a row.
2. **Expect:** the spike reports variance across runs (not just a single sample), since a single
   fast run could mask a p95 tail-latency problem relevant to NFR-001.

---

## F-003 — Project scaffolding & Docker Compose skeleton

### TC-003-01 (Happy path) — Backend builds
1. From `src/`, run `dotnet build` against the scaffolded .NET 10 solution.
2. **Expect:** build succeeds with zero errors; all projects target `net10.0`; Wolverine is a
   referenced package; a vertical-slice folder convention is visible in the project layout.

### TC-003-02 (Happy path) — Frontend builds
1. From the Angular project directory, run the Angular CLI production build.
2. **Expect:** build succeeds with zero errors; Angular Material + CDK are installed and confirmed
   compatible with the scaffolded Angular version; a Material theme is configured.

### TC-003-03 (Happy path) — Static asset integration
1. Build the Angular app, then build/run the .NET host.
2. Request the host's root URL.
3. **Expect:** the Angular build output is served from the ASP.NET Core host's static file root
   (i.e., copied into the host's wwwroot or equivalent as part of the build/Docker image, not
   served from a separate process).

### TC-003-04 (Happy path) — `make up` brings up the full stack (Compose + host LM Studio, ADR-016)
1. Run `make up` from the repo root.
2. **Expect:** Docker Compose brings up two services — the app container (API + served dashboard)
   and `postgres:18.4` (pinned tag, verified via `docker compose config` or image inspection — must
   not resolve to `latest`); LM Studio is **not** a Compose service (ADR-016 — it runs
   host-installed, already on the operator's machine).
3. **Expect:** the Makefile checks Docker is running (starting Docker Desktop if needed), checks
   LM Studio's local server is responding on its configured port (starting it via `lms server
   start` if needed), and loads the configured model (`LMSTUDIO_MODEL`, default
   `llama-3.2-3b-instruct` per ADR-017) via `lms load`.
4. **Expect:** `make status` (or manual `curl`) confirms all three are reachable — HTTP 200 on the
   app's health endpoint, a successful `pg_isready` against Postgres, and a reachable LM Studio API
   port (`/v1/models`).
5. **Edge case:** running `make up` a second time without tearing anything down should detect
   Docker/LM Studio/the model are already up and skip redundant start/load steps rather than
   erroring or reloading.

### TC-003-05 (Edge case) — Package compatibility with .NET 10 / PostgreSQL 18
1. Inspect the scaffolded solution's package references: EF Core, Hangfire, Wolverine, GitHub API
   client, Npgsql.
2. **Expect:** each is confirmed compatible with .NET 10 and (for Npgsql/EF Core) PostgreSQL 18 —
   either by successful build/connection, or by an explicit note in the Developer Output if a
   package requires a preview/RC version to support .NET 10 at time of scaffolding.

### TC-003-06 (Regression-sensitive) — Clean rebuild from scratch
1. Remove all build artifacts and containers (`docker compose down -v`, `dotnet clean`).
2. Re-run TC-003-01 through TC-003-04 from a clean checkout.
3. **Expect:** identical successful outcome — scaffolding must not depend on stale local state
   (cached images, prior `dotnet restore`, etc.) to succeed.

---

## F-004 — Data Store schema (EF Core)

### TC-004-01 (Happy path) — Migration applies cleanly to a fresh PostgreSQL 18.4 instance
1. From `src/backend/GitCrawler.Api`, against a freshly-created, empty PostgreSQL 18.4 database
   (e.g. a clean `make up` with an empty `data/postgres/` bind mount), start the app.
2. **Expect:** `Program.cs`'s startup `Database.Migrate()` call applies the `InitialCreate`,
   `AddCrawlerRawSignalFields`, and `AddScoreStarCountSignal` migrations with no errors, creating
   the `Repositories`, `Scores`, `Summaries`, `TrendAggregates`, and `Bookmarks` tables in the
   `public` schema.
3. **Expect:** `Repositories.GitHubId` has a unique index (`IX_Repositories_GitHubId`) and
   `Bookmarks.RepositoryId` has a unique index — inspect via `\d "Repositories"` / `\d "Bookmarks"`
   in `psql`, or `dotnet ef migrations script` against the three migrations.

### TC-004-02 (Edge case) — Hangfire schema coexists without collision
1. After F-006's Hangfire wiring runs at least once against the same database, inspect the
   database's schemas.
2. **Expect:** Hangfire's own job-storage tables live under a separate `hangfire` schema
   (`UsePostgreSqlStorage`'s default), not the `public` schema EF Core's `DbSet`s use — no table
   name collisions, no EF Core migration defines any Hangfire table.

### TC-004-03 (Regression-sensitive) — Idempotent upsert target exists
1. Insert two `Repository` rows with the same `GitHubId` directly via `INSERT` (bypassing the
   application).
2. **Expect:** the second insert fails with a unique-constraint violation on `GitHubId` — proves
   the schema-level constraint F-005's crawler upsert logic depends on is actually enforced by the
   database, not just assumed by application code.

---

## F-005 — GitHub Crawler

### TC-005-01 (Happy path) — Discovery and idempotent upsert
1. **Manual/live** (requires `GITHUB_TOKEN`): trigger `DiscoverRepositoriesCommand` (directly via a
   test harness invoking Wolverine's `IMessageBus.InvokeAsync`, or by waiting for F-006's scheduled
   `discover-repositories` Hangfire job to fire).
2. **Expect:** repositories matching the discovery criteria
   (`GitHub:DiscoveryLookbackDays`/`GitHub:DiscoveryMinimumStars` in `appsettings.json`) are written
   to `Repositories`, with `LicenseIdentifier`/`LicenseName` correctly null for unlicensed repos
   (not defaulted to a placeholder).
3. Re-run the same command immediately.
4. **Expect:** no duplicate rows are created — existing rows (matched by `GitHubId`) are updated in
   place (e.g. `LastCrawledAtUtc` advances).

### TC-005-02 (Edge case) — Contributor-count caching cadence
1. Crawl a repo for the first time. **Expect:** `ContributorCountFetchedAtUtc` is set and a REST
   call was made (per the F-001 spike §7 mitigation).
2. Re-crawl the same repo within 7 days. **Expect:** no new REST contributor-count call is made
   (`ContributorCount`/`ContributorCountFetchedAtUtc` unchanged).
3. **Manual/simulated:** re-crawl after `ContributorCountFetchedAtUtc` is artificially aged past 7
   days. **Expect:** a fresh REST call is made and the timestamp advances.

### TC-005-03 (Regression-sensitive) — Rate-limit backoff actually engages
1. **Manual/simulated:** force a GraphQL `RATE_LIMITED` condition (e.g. exhaust a low-quota test
   token, or substitute a faked `IGitHubDiscoveryClient` in a test harness that throws
   `GitHubGraphQlRateLimitExceededException`).
2. **Expect:** the crawl waits until the reported `resetAt` before retrying, rather than failing
   the run outright or retrying immediately and re-triggering the same limit.
3. Repeat for a REST `403`/`429` with `x-ratelimit-remaining: 0`. **Expect:** the wait targets
   `x-ratelimit-reset`, per the F-001 spike's §6 back-off strategy.

---

## F-006 — Job Scheduler (Hangfire)

### TC-006-01 (Happy path) — Recurring job registered and dashboard reachable
1. Run `make up`, then check Hangfire's dashboard at `/hangfire` (unauthenticated — no auth
   system exists elsewhere in this single-operator v1; see ADR-009 Consequences for why an
   earlier shared-secret filter was tried and reverted).
2. **Expect:** HTTP 200 and the dashboard renders fully styled (CSS/JS/stats requests all
   succeed); the `discover-repositories` recurring job is listed with its configured cron
   schedule (`Hangfire:CrawlerCronSchedule`, default `0 3 * * *`).

### TC-006-02 (Happy path) — Crawl-to-score chaining
1. Trigger the `discover-repositories` job (manually via the dashboard, or by waiting for its
   schedule) against a database with no existing `Score` rows for the discovered repos.
2. **Expect:** once the crawl completes, a `ComputeScoresJob` continuation fires automatically
   (visible in the Hangfire dashboard's job history as a job chained via `ContinueJobWith`) without
   waiting for a separate schedule — completing the "crawl before score" ordering from Architecture
   §3.

### TC-006-03 (Regression-sensitive) — Mid-run restart doesn't duplicate or drop work
1. Start a crawl, then restart the `app` container mid-run (`docker compose restart app` while a
   crawl is in progress).
2. **Expect:** Hangfire's PostgreSQL-backed storage means the job's state survives the restart —
   it either resumes/retries via Hangfire's own retry policy or is cleanly re-queued, and does not
   silently vanish from the dashboard's history.
3. **Expect:** because F-005's upsert is keyed on `GitHubId`, any repos already written before the
   restart are not duplicated when the job re-runs the affected portion of the crawl.

---

## F-007 — Scoring Engine

### TC-007-01 (Happy path) — Score computed from all five independent signals
1. Seed a `Repository` row with known values (e.g. licensed, `CommitCount`=20 over a 2-week-old
   repo, `ContributorCount`=10, `ForkCount`=50, `StarCount`=25), then trigger
   `ComputeScoresCommand`.
2. **Expect:** a new `Score` row is written with `HasLicense`/`LicenseType`/`CommitsPerWeek`
   (derived, not copied — `CommitCount / weeks-elapsed`)/`ContributorCount`/`ForkCount`/`StarCount`
   all correctly mapped from the source `Repository` (each field traceable to its own source
   column, none swapped with another), plus a `TotalScore` in `[0, 100]`.
3. **Expect:** changing any single signal (e.g. doubling `StarCount` alone) changes `TotalScore`
   while the contribution of the other four signals is unaffected — confirms the "independently
   identifiable, weighted inputs" requirement (FR-002/FR-005), not just "a score gets computed."

### TC-007-02 (Edge case) — No external calls, no license, brand-new repo
1. Confirm (via code inspection or a network-call assertion in a test harness) that
   `ComputeScoresCommandHandler`/`ScoringWeights` make zero HTTP/GraphQL calls — pure computation
   per Architecture §3.
2. Score a repository with no license. **Expect:** `HasLicense = false`, `LicenseType = null`, and
   the license component contributes `0` to `TotalScore` rather than crashing or defaulting to a
   nonzero bonus.
3. Score a repository created today with a nonzero `CommitCount`. **Expect:** `CommitsPerWeek` does
   not read as an absurd/inflated rate — the 1-week elapsed-time floor caps how much a same-day
   repo's rate can spike.

### TC-007-03 (Regression-sensitive) — Re-scoring on re-crawl, not duplicated per run
1. Score a repository once. Re-run `ComputeScoresCommand` immediately without an intervening
   crawl. **Expect:** no new `Score` row is added (the repo's existing score is already newer than
   `LastCrawledAtUtc` — the "needs scoring" condition skips it).
2. Re-crawl the same repository (advancing `LastCrawledAtUtc`), then re-run `ComputeScoresCommand`.
3. **Expect:** a new `Score` row is added (scoring history preserved, per `Score`'s multi-row-per-
   repository design from F-004) reflecting any updated raw signals, rather than overwriting the
   prior row.

---

## F-008 — Summarizer (LM Studio + Llama 3.2 3B Instruct)

### TC-008-01 (Happy path) — Top-scored repository without a summary is summarized
1. Seed a `Repository` with a latest `Score.TotalScore` ≥ `Summarization:MinimumScore` (default 40)
   and no existing `Summary` row, then trigger `GenerateSummariesCommand`.
2. **Expect:** a `Summary` row is written for the repository (`GeneratedAtUtc` set) with **both**
   `ShortContent` and `DetailedContent` populated — two separate `IRepositorySummarizer` calls, not
   one (2026-08-04: split from a single shared summary into a short card summary and a longer
   detailed one, each generated via its own LM Studio call) — and each request carries the repo's
   `Owner`/`Name` — satisfies F-008's "generates a structured summary for top-scored repos without
   one" AC.

### TC-008-02 (Edge case) — Below-threshold and already-summarized repos are excluded
1. Seed one repository with a latest score below `Summarization:MinimumScore`, and a second with a
   score well above it but an existing `Summary` row. Trigger `GenerateSummariesCommand`.
2. **Expect:** neither repository is summarized — the below-threshold repo counts toward
   `SkippedCount`, and the already-summarized repo is untouched (still exactly one `Summary` row for
   it). Confirms Summary's create-once semantics (deliberate divergence from Score's
   append-history/re-scoring-on-recrawl behavior, per the Task Packet).

### TC-008-03 (Edge case) — Missing README (404) does not block summarization
1. Seed an eligible repository. Configure the README fetch (`GET /repos/{owner}/{repo}/readme`) to
   return `404`, then trigger `GenerateSummariesCommand`.
2. **Expect:** the repository is still summarized (`SummarizedCount` increments), using
   `PrimaryLanguage`/`LicenseName` alone — a missing README is a legitimate, non-error input, not a
   per-repo failure.

### TC-008-04 (Regression-sensitive) — Eligibility uses the latest score by time, not the highest ever
1. Seed a repository with two `Score` rows: an earlier one above `MinimumScore`, and a
   chronologically later one (by `ComputedAtUtc`) below it (i.e., the repo went stale on a
   re-crawl). Trigger `GenerateSummariesCommand`.
2. **Expect:** the repository is **not** summarized — eligibility reflects the repo's current
   standing (latest by `ComputedAtUtc`), not its historical peak. Since `Summary` is create-once,
   getting this wrong would permanently summarize a repo off a stale high score.

### TC-008-05 (Regression-sensitive) — Per-repository failure does not abort the batch
1. Seed two eligible repositories. Configure `IRepositorySummarizer` (or the LM Studio call) to
   throw for the first and succeed for the second, then trigger `GenerateSummariesCommand`.
2. **Expect:** the failing repository is logged and skipped (`FailedCount` increments, no `Summary`
   row written — it's retried automatically on the next run since it still has no summary), while
   the second repository is summarized normally (`SummarizedCount` increments). One bad repo must
   never abort the whole run.

### TC-008-06 (Edge case) — Batch size caps a single run
1. Seed more eligible repositories than `Summarization:BatchSize` (default 20; use a smaller
   configured value to keep the test fast), then trigger `GenerateSummariesCommand`.
2. **Expect:** exactly `BatchSize` repositories are summarized (highest-scored first), the remainder
   counted in `SkippedCount` — not attempted this run, picked up automatically on the next one since
   they still have no `Summary` row.

### TC-008-07 (Happy path) — Score-to-summarize chaining (F-006/F-009 chain link 3)
1. Trigger the `compute-scores` continuation (or wait for the crawl-to-score chain, F-006 TC-006-02)
   against a database containing at least one top-scored repo without a summary.
2. **Expect:** once scoring completes, a `GenerateSummariesJob` continuation fires automatically
   (visible in the Hangfire dashboard's job history, chained via `ContinueJobWith`) without waiting
   for a separate schedule — completing "score before summarize" ordering (Architecture §3).

### TC-008-08 (Manual) — Live LM Studio summarization quality and throughput
1. **Manual (requires a running LM Studio instance with `llama-3.2-3b-instruct` loaded, per
   ADR-017/`make up`):** trigger `GenerateSummariesCommand` against real top-scored repositories with
   real README content.
2. **Expect:** each generated summary is non-empty, covers purpose/key features/tech stack/notable
   caveats per `LmStudioRepositorySummarizer`'s system prompt, and completes within NFR-001's
   seconds-per-repository target — consistent with the F-002 spike's live throughput results.

### TC-008-09 (Regression-sensitive) — README content is capped before being sent to the model
Added 2026-08-04 after a live run against `openclaw/openclaw` (a 111KB/~35,489-token README) failed
outright: LM Studio rejected the request with `"n_keep: 35489 >= n_ctx: 8192"` — the loaded model's
context window, exceeded because nothing capped the README's length before it was sent.
1. Seed an eligible repository whose README content exceeds `Summarization:MaxReadmeCharacters`
   (default 6000), then trigger `GenerateSummariesCommand` against a stubbed `IRepositorySummarizer`/
   HTTP handler that records the outgoing prompt text.
2. **Expect:** the README portion of both the short and detailed prompts is truncated to
   `MaxReadmeCharacters` characters, with a `"[README truncated for length]"` marker appended — the
   full, untruncated README is never sent to the model, regardless of its actual size.
3. **Expect also:** a non-success LM Studio response now has its response body surfaced in the thrown
   exception message (previously discarded by a bare `EnsureSuccessStatusCode()`), so a future
   context-window (or other) failure is diagnosable directly from `GenerateSummariesCommandHandler`'s
   existing `logger.LogWarning(ex, ...)` call, without a manual repro.

---

## F-009 — Trend Aggregator

### TC-009-01 (Happy path) — Scored and summarized repository is counted in its category
1. Seed a repository with `PrimaryLanguage` set, a `Score`, and a `Summary`, then trigger
   `AggregateTrendsCommand`.
2. **Expect:** a `TrendAggregate` row is written with `Category` = the repo's `PrimaryLanguage`,
   `RepositoryCount` = 1, `AverageScore` = the repo's latest `TotalScore`, and
   `PeriodStart`/`PeriodEnd` both equal to today (default `Trends:PeriodDays` = 1) — satisfies
   F-009's "rolls up scored + summarized repos into trend summaries" AC (FR-008).

### TC-009-02 (Edge case) — Scored-only (not yet summarized) and null-language repos are excluded
1. Seed one repository with a `Score` but no `Summary`, and a second with both a `Score` and a
   `Summary` but a `null` `PrimaryLanguage`. Trigger `AggregateTrendsCommand`.
2. **Expect:** neither repository contributes to any `TrendAggregate` row — "scored + summarized"
   means both must hold, and a null category has no meaningful trend bucket to attribute to (not
   lumped into a fake "Unknown" category).

### TC-009-03 (Happy path) — Multiple repositories in the same category aggregate correctly
1. Seed two scored-and-summarized repositories sharing the same `PrimaryLanguage` with different
   `TotalScore` values, then trigger `AggregateTrendsCommand`.
2. **Expect:** one `TrendAggregate` row for that category, with `RepositoryCount` = 2 and
   `AverageScore` = the arithmetic mean of both repos' latest scores.

### TC-009-04 (Regression-sensitive) — Rollup uses the latest score by time, not the highest ever
1. Seed a repository with two `Score` rows: an earlier high score and a chronologically later
   (by `ComputedAtUtc`) lower one. Trigger `AggregateTrendsCommand`.
2. **Expect:** the trend's `AverageScore` reflects the latest score, not the historical peak — same
   "latest by `ComputedAtUtc`" rule F-007/F-008 each apply to their own `Score` reads.

### TC-009-05 (Regression-sensitive) — Re-running for the same period upserts, never duplicates
1. Trigger `AggregateTrendsCommand` once against a seeded repo, note the resulting `TrendAggregate`
   row's `Id`. Seed a second repository in the same category, then trigger the command again for the
   same period.
2. **Expect:** still exactly one row for `(Category, PeriodStart, PeriodEnd)` — same `Id`, updated
   `RepositoryCount`/`AverageScore`/`CreatedAtUtc` in place, not a duplicate row. Satisfies NFR-003
   idempotency; there is no unique DB constraint enforcing this (intentional, per the Task Packet),
   so this behavior depends entirely on the single-threaded Hangfire job's query-then-upsert logic.

### TC-009-06 (Edge case) — Configurable multi-day period
1. Configure `Trends:PeriodDays` to a value > 1 (e.g. 3), seed an eligible repo, trigger
   `AggregateTrendsCommand`.
2. **Expect:** `PeriodEnd` = today, `PeriodStart` = `PeriodEnd` − (`PeriodDays` − 1) — confirms the
   window computation, not just the single-day default path.

### TC-009-07 (Happy path) — Summarize-to-aggregate chaining (F-006/F-009 chain link 4)
1. Trigger the `generate-summaries` continuation (or wait for the score-to-summarize chain,
   TC-008-07) against a database containing at least one newly-summarized repo.
2. **Expect:** once summarization completes, an `AggregateTrendsJob` continuation fires automatically
   (visible in the Hangfire dashboard's job history, chained via `ContinueJobWith`) — completing the
   full crawl → score → summarize → aggregate trends chain end-to-end (Architecture §3).

---

## F-018 — Dashboard UX design brief & Claude Designer handoff

No running system to verify — this is a documentation output (`docs/design-briefs/dashboard-ux-brief.md`).

### TC-018-01 (Happy path) — Brief covers all four required views plus filter/sort/bookmark interactions
1. Open `docs/design-briefs/dashboard-ux-brief.md`.
2. **Expect:** §4 specifies a layout for each of the four FR-009 views (Discovery Feed, Hidden Gems,
   Trending, Categories); §5 specifies the FR-004 filter/sort facets (language, star range, topic,
   license) and their controls; §6 specifies the FR-007 bookmark create/list/delete interactions
   (collapsed into a single toggle for create/delete, plus a "bookmarked only" filter for list).

### TC-018-02 (Edge case) — Material-only constraint honored, gaps flagged not silently specced
1. Confirm §2 states the Angular-Material-only constraint (ADR-011) explicitly.
2. **Expect:** every component named across §3-§6 is an actual `@angular/material`/`@angular/cdk`
   component; §7 lists every case where Material has no first-class equivalent (infinite scroll,
   sparkline/growth chart, skeleton loader) with a named Material-native fallback — none silently
   introduces a custom/non-Material widget.

### TC-018-03 (Regression-sensitive) — Handoff scope is honestly stated, review/approval remains open
1. Confirm §8 (Handoff Note) states plainly that no design-tool invocation or external handoff
   occurred — the document itself is the handoff artifact — and that the resulting UX design still
   requires review/approval before F-011 begins.
2. **Expect:** the brief does not overstate its own completion status (its own header still reads
   "DRAFT — pending Claude Designer review") — confirms the PMBook's F-018 "Done" annotation reflects
   "brief authored and handed off," not "design reviewed and approved," matching F-018's actual
   acceptance criteria scope (AC4 handoff, not the downstream review gate that still blocks F-011).

---

## F-010 — Web API

### TC-010-01 (Happy path) — Hidden Gems filters, sorts, and paginates
Originally exercised via the Discovery Feed endpoint; Discovery Feed was decommissioned 2026-08-03
(see TC-011-01/TC-011-02) since `GetHiddenGems` already covers the same shared D4 filter/sort/paginate
contract as a superset — retargeted to Hidden Gems rather than removed, since the underlying
capability itself didn't go away.
1. Seed *scored* repositories (Hidden Gems requires at least one `Score` row) with varied
   `PrimaryLanguage`, `StarCount`, `Topics`, `LicenseIdentifier`, and `FirstDiscoveredAtUtc`. Call the
   Hidden Gems endpoint with a combination of `language`, `minStars`/`maxStars`, `topic`, and `license`
   filters plus `sort=Newest&direction=Desc` (overriding its own `Score desc` default).
2. **Expect:** only repositories matching all supplied facets (AND across facets, OR within a
   multi-value facet) are returned, ordered by `FirstDiscoveredAtUtc` descending, `page`/`pageSize`
   honored (default `pageSize` 24) — satisfies FR-004 and D4 of F-010's Task Packet.
3. Omit all filters, omit `sort`. **Expect:** every repository is returned in Hidden Gems' own default
   order (`Score desc`).

### TC-010-02 (Happy path) — Hidden Gems exposes the FR-005 weighted signal breakdown
1. Seed a scored repository, call the Hidden Gems endpoint.
2. **Expect:** the response's score-breakdown block reports each of the five signals
   (license/commits-per-week/contributor count/fork count/star count) alongside the exact
   `ScoringWeights` constants (0.18/0.27/0.225/0.225/0.10) and the `TotalScore` — not just a single
   aggregate number. Default sort is `Score desc`.

### TC-010-03 (Removed 2026-08-03) — Trending's contributing repos mirror F-009's own membership rule
This scenario covered `/api/trending`'s contributing-repos membership check, removed along with the
dashboard's Trending view (see TC-011-04) — nothing else consumed that endpoint, so it was deleted
entirely rather than kept (unlike `/api/categories`, see TC-010-04). Kept as a removed placeholder
(ID not reused), same precedent as TC-011-11.

### TC-010-04 (Happy path) — Categories list
1. Seed `TrendAggregate` rows for two categories, call the Categories endpoint.
2. **Expect:** one entry per distinct category reflecting the latest period's `RepositoryCount`/
   `AverageScore`. (This scenario originally also covered a per-category drill-down endpoint; that
   endpoint, and the dashboard's dedicated Categories tab it backed, were both removed 2026-08-03 —
   browsing by category is done via Hidden Gems' existing Language filter instead, since Category ≡
   `Repository.PrimaryLanguage`. Step 3's drill-down assertion is removed accordingly; this endpoint's
   list behavior is unchanged and still covered above.)

### TC-010-05 (Happy path) — Bookmark create/list/delete round-trip
1. Call create-bookmark for a repository, then list-bookmarks, then delete-bookmark for the same
   repository, then list-bookmarks again.
2. **Expect:** the repository appears in the list after create and is absent after delete; a repo
   card's `IsBookmarked` flag flips accordingly on the Hidden Gems endpoint in between (FR-007).

### TC-010-06 (Edge case) — Bookmark idempotency
1. Call create-bookmark twice in a row for the same repository.
2. **Expect:** no constraint-violation error on the second call (the unique index on
   `Bookmark.RepositoryId` is respected without surfacing a 409/500).
3. Call delete-bookmark for a repository that was never bookmarked.
4. **Expect:** no error — a defined, documented idempotent response either way.

### TC-010-07 (Edge case) — Topic filter and repos with no topics
1. Seed one *scored* repository with `Topics` containing a known value and one (also scored) with an
   empty `Topics` list. Filter Hidden Gems by that topic value.
2. **Expect:** only the matching repository is returned; the empty-`Topics` repository never matches
   any topic filter and never errors when `Topics` is empty.

### TC-010-08 (Regression-sensitive) — Score/Commits sort uses the latest score, not the highest ever
1. Seed a repository with two `Score` rows: an earlier high `TotalScore`/`CommitsPerWeek` and a
   chronologically later (by `ComputedAtUtc`) lower one. Call Hidden Gems sorted by `Score desc`, then
   again sorted by `Commits desc`.
2. **Expect:** both use the repository's latest score, not its historical peak — same class of bug
   F-008's `GenerateSummariesCommandHandler` had before its first-round fix (`docs/handoff.md`
   "Important context").

### TC-010-09 (Regression-sensitive) — `FirstDiscoveredAtUtc` is set once, never overwritten
1. Crawl a new repository (first insert). **Expect:** `FirstDiscoveredAtUtc` is set.
2. Re-crawl the same repository after a delay (`LastCrawledAtUtc` advances).
3. **Expect:** `FirstDiscoveredAtUtc` is unchanged from step 1 — a repeatedly re-crawled old repo
   must never resurface as "Newest."

### TC-010-10 (Edge case) — Pagination boundaries
1. Seed exactly `pageSize` + 1 matching repositories. Request page 1, then page 2, then a page far
   beyond the last page.
2. **Expect:** page 1 returns a full page, page 2 returns exactly one result, and the out-of-range
   page returns an empty result set — never an error.

### TC-010-11 (Happy path) — Hidden Gems card exposes its own repository's trend growth
Changed 2026-08-04 (operator: "Trend is currently calculated per language. I want it to be calculated
per repository"): originally computed from `TrendAggregate` — a rollup shared by every repository of
the same `PrimaryLanguage`, so every C# repo showed the identical growth figure regardless of its own
standing. Now computed directly from the repository's own `Score` history instead (`Score` already
gets a new row per repo on every re-crawl, per `ComputeScoresCommandHandler` — no schema change was
needed); `TrendAggregate` itself is untouched and still backs the Categories/Language-filter endpoint
(TC-010-04).
1. Seed a repository with two `Score` rows from separate re-crawls (previous `TotalScore` 50, latest
   60), and a second, same-language repository with a single differently-valued `Score`. Call the
   Hidden Gems endpoint.
2. **Expect:** the first repository's `TrendGrowth` is `"▲ +20% vs. last period"` — computed from
   *its own* two most recent `Score.TotalScore` values, not blended with the second, same-language
   repository's score (confirms this is genuinely per-repository, not still secretly per-category).
3. Repeat with only one `Score` row ever recorded for a repository (no re-crawl yet). **Expect:**
   `TrendGrowth` falls back to `"{score} current score"` (no prior score to diff against — reworded
   from the old `"{avg} avg score"`, since it's no longer an average across multiple repos, just this
   one repo's sole score so far).
4. **Expect:** `TrendGrowth` is never `null` for any card Hidden Gems returns — every returned card
   already has at least one `Score` row (Hidden Gems' own `Scores.Any()` filter), so there's always at
   least the single-score fallback in step 3, unlike the old `TrendAggregate`-based version, which
   could be `null` if the nightly Trend Aggregator hadn't run yet for that category.

---

## F-011 — Web Dashboard

### TC-011-01 (Happy path) — Required view renders, is the default route, and is the whole primary nav
1. Load the dashboard. **Expect:** the default route lands on Hidden Gems — the dashboard's sole view
   — composed from Angular Material components (AC1, FR-009); the primary nav has exactly one entry
   ("Hidden Gems"). (Originally four view entries plus a separate F-012 "Bookmarks" entry: Categories
   and Trending were removed 2026-08-03, see TC-011-05 and TC-011-04 respectively; Discovery Feed was
   removed the same day, once Categories/Trending had already folded into Hidden Gems leaving it no
   meaningfully distinct browsing experience; Bookmarks was removed last, once its dedicated view
   turned out to be redundant with Hidden Gems' own "Bookmarked only" filter — see TC-012-01.)

### TC-011-02 (Happy path) — Filter/sort controls work end-to-end (Hidden Gems)
Originally exercised on both Discovery Feed and Hidden Gems (steps 1-4 on Discovery Feed, then
repeated on Hidden Gems); narrowed to Hidden Gems alone now that Discovery Feed is gone (2026-08-03) —
the filter/sort capability itself is unaffected, only the view exercising it changed.
1. On Hidden Gems, select a language via the Language facet (`mat-select multiple`), narrow the Star
   range slider, add a topic via the autocomplete, select a license.
2. **Expect:** each selection renders as a removable chip in the active-filter `mat-chip-set`; the
   grid re-fetches from `GET /api/hidden-gems` with the matching `language[]`/`minStars`/`maxStars`/
   `topic[]`/`license[]` query params (AC2, FR-004).
3. Change the sort control (`mat-button-toggle-group`) and flip the direction icon button.
   **Expect:** the request's `sort`/`direction` params update and results re-order accordingly
   (default sort is `Score desc`).
4. Click "Clear all". **Expect:** all chips disappear and the grid returns to the unfiltered,
   default-sorted result set. **Expect also:** every card shows the score badge, its owner/discovered-
   date/star-count subtitle, and a footer with language/license chips plus an "Open on GitHub" link —
   the score breakdown and trend-growth chip both moved off the card into the click-through detail
   dialog on 2026-08-04 (see TC-011-14) and are no longer shown inline on the card itself.

### TC-011-03 (Happy path) — Bookmark toggle, optimistic UI, undo/retry
1. On any Hidden Gems card, click the bookmark toggle.
2. **Expect:** the icon flips immediately (optimistic), a `mat-snack-bar` confirms ("Added to
   bookmarks") with an "Undo" action, and `POST /api/repositories/{id}/bookmark` fires.
3. Click "Undo" on the snack-bar. **Expect:** the icon flips back and `DELETE
   /api/repositories/{id}/bookmark` fires.
4. Simulate the bookmark API call failing (e.g. stop the backend mid-request). **Expect:** the icon
   reverts to its prior state and the snack-bar shows the error variant ("Couldn't save bookmark —
   try again") with a "Retry" action (FR-007).

### TC-011-04 (Removed 2026-08-03) — Trending view renders server order, no client-side re-sort
This scenario covered the standalone Trending view (per-category trend cards, server-order rendering,
expandable contributing-repos panel), decommissioned per the operator's direction to merge Trending
into Hidden Gems — see TC-010-11/TC-011-13 for its replacement (each Hidden Gems card now shows its
own trend growth directly — computed per repository since 2026-08-04, not per category as originally
built, see TC-010-11's own note). Kept as a removed placeholder (ID not reused), same precedent as
TC-011-11.

### TC-011-05 (Removed 2026-08-03) — Categories grid and drill-down
This scenario covered the standalone Categories tile grid and its Category drill-down route, both
decommissioned per the operator's direction to remove the Categories tab — Category ≡
`Repository.PrimaryLanguage`, and that value remains fully filterable via the existing Language
facet on Hidden Gems (TC-011-02), so no distinct browsing capability was lost. Kept
as a removed placeholder (ID not reused) rather than deleted outright, matching TC-011-11's own
precedent for a superseded scenario in this document.

### TC-011-06 (Edge case) — Empty, loading, and error states
1. Request a filter combination with zero matches. **Expect:** the centered empty-state `mat-card`
   ("No repositories match these filters") with a button that clears all active filters.
2. Toggle "Bookmarked only" with zero bookmarks. **Expect:** the same empty state, not an error.
3. Simulate the API request failing. **Expect:** the centered error-state `mat-card` with a "Retry"
   button that re-issues the request.
4. Observe a fresh load. **Expect:** a centered `mat-progress-spinner` on first load, and an
   indeterminate `mat-progress-bar` under the filter bar on a subsequent refetch (not a full-page
   spinner) while existing results remain visible.

### TC-011-07 (Edge case) — Pagination beyond the last page
1. Filter to a small result set, then request a page far beyond the last page (mirrors TC-010-10 at
   the UI layer).
2. **Expect:** the empty state renders (per TC-011-06), not a crash or an unhandled error — the API's
   `items: []` response for an out-of-range page must be handled the same as a genuine zero-match
   filter.

### TC-011-08 (Edge case) — "Summary pending" placeholder, no layout jump
1. Render a card whose `summaryContent` is `null` (repo scored/discovered but not yet summarized by
   F-008). **Expect:** a fixed-height "Summary pending" placeholder renders in the summary slot, not
   an empty area.
2. Simulate the same repo later returning a non-null `summaryContent` (re-fetch or state update).
   **Expect:** the real summary replaces the placeholder in the same slot with no visible height
   change/layout shift.

### TC-011-09 (Edge case) — Responsive collapse at the 960px breakpoint
Step 2's original "primary nav collapses to a bottom floating pill nav" no longer applies — the
primary nav (Hidden Gems/Bookmarks entries, then just Hidden Gems) was removed entirely on 2026-08-04
once Hidden Gems became the dashboard's only page ("remove the hidden gems tab, we only have one
page"); the toolbar today is brand + a reserved search placeholder only, with nothing to collapse at
any width. Only the filter/sort bar's own collapse behavior still applies.
1. Resize the viewport below 960px on Hidden Gems.
2. **Expect:** the filter/sort bar collapses to a single "Filters · N" button (N = active filter
   count) that opens a `mat-sidenav` containing the same controls the desktop layout shows inline;
   active filter chips remain visible inline next to the trigger button regardless of panel state; the
   sticky toolbar is unchanged at any width.
3. Resize back above 960px. **Expect:** the filter bar returns to its desktop layout with selection
   state preserved.
4. With the sidenav open, **expect:** the repository grid behind it (including each card's "Open on
   GitHub" link) is fully hidden/inert — not visible or clickable through the opened panel. Regression
   check added 2026-08-04 after an operator screenshot showed grid content painting on top of the
   opened sidenav (`.filter-bar__sheet-container` shipped Angular Material's own default `z-index: 1`
   on the container element itself, distinct from the `z-index: 20` this app already applied to the
   sidenav/backdrop *children* — fixed by raising the container to the same `z-index: 20` plus
   `isolation: isolate`, confirmed against the live render).

### TC-011-10 (Removed 2026-08-03) — Category name requiring URL encoding
This scenario covered URL-encoding for the Category drill-down route, removed alongside the rest of
the Categories tab (see TC-011-05). Kept as a removed placeholder (ID not reused), same precedent as
TC-011-11.

### TC-011-11 (Regression-sensitive) — Reserved v2 placeholder is inert
1. Inspect the primary nav and the filter-bar area.
2. **Expect:** the disabled "Search (v2)" field is present (dashed border, reduced opacity) but not
   wired to any handler — clicking it does nothing. This placeholder exists so the shell won't
   reflow when v2 search lands, and must not be mistaken for broken functionality in a future manual
   pass. (This scenario originally also asserted a disabled "Bookmarks · F-012" ghost nav pill; that
   placeholder was superseded by F-012, which replaced it with a live "Bookmarks" nav entry — see
   TC-012-02. The nav-pill assertion is removed here to avoid contradicting TC-012-02; the
   "Search (v2)" placeholder-inertness assertion above remains accurate and unchanged.)

### TC-011-12 (Manual) — Live build-and-serve smoke test (FR-009 AC3)
1. Run `dotnet publish` (or the Docker image build) against `src/backend/GitCrawler.Api` with a real
   Node toolchain available, so `BuildAngularApp`/`CopyAngularApp` execute for real (not just
   `npm run build` in isolation, which the Developer already verified produces
   `dist/dashboard/browser/`).
2. **Expect:** the publish output's `wwwroot` contains the built Angular app; starting the published
   host and requesting `/` serves the dashboard's `index.html` (via `UseDefaultFiles`/
   `UseStaticFiles`), and a client-side route (e.g. `/hidden-gems` requested directly, not via
   in-app navigation) falls back to `index.html` via `MapFallbackToFile` and resolves correctly
   rather than 404ing.
3. Flagged Manual because it requires a live `make up`-equivalent environment with both a .NET SDK
   and Node toolchain in the same execution context — same category of gap Phase 1/2/F-010's own
   Integration passes disclosed for their own live-infrastructure checks (`docs/handoff.md`).

### TC-011-13 (Happy path) — Detail dialog renders the trend-growth chip
Retargeted 2026-08-04 (capability persists, only the vehicle changed, same precedent as TC-010-01's
Discovery Feed retargeting): the trend-growth chip was removed from the compact card entirely that
same day ("dont show trending pilll on the card... just show topic, license on bottom left and
github link on bottom right") — it renders only in the click-through detail dialog now (TC-011-14).
1. Open the detail dialog (TC-011-14) for a repository. **Expect:** its chip row renders a chip with
   `trendGrowth`'s exact text (see TC-010-11 for how the API now computes it per repository).
2. Per TC-010-11's step 4, `trendGrowth` is never `null` for a repository Hidden Gems returns at all
   (every returned repo has at least one `Score` row) — so, unlike the old category-based version,
   there is no "chip omitted" case left to test here.

### TC-011-14 (Happy path) — Card click opens the repository detail dialog (design brief §09)
Converted 2026-08-04 from a right-side `mat-drawer` to a centered `MatDialog` ("i want the overlay to
show under the header. And i want this detail pane to be centered like a modal") — the drawer-specific
assertions below (backdrop click, viewport-width panel) are updated for the dialog's actual behavior,
not carried over unchanged. The card's own "Why this score?" panel referenced in the original version
of this scenario no longer exists (removed the same day, folded into this dialog's own score-breakdown
footer instead) — this scenario no longer compares the dialog's breakdown against a card-level panel,
since there isn't one to compare against.
1. On Hidden Gems, click a card anywhere except the bookmark toggle or the "Open on GitHub" link (the
   card itself no longer has any other interactive control to avoid, now that the score panel is
   gone).
2. **Expect:** a centered dialog opens over the full page, under the sticky header (not just the
   grid), with a dimmed backdrop — showing that repo's full, untruncated **detailed** AI summary
   (`detailedSummaryContent`, distinct from the card's own short `summaryContent` since the
   2026-08-04 two-summary split — not clamped to 3 lines like the card), its topics as a chip list
   (not shown on the compact card at all), its language/license chips, star/fork counts, and
   trend-growth chip merged into the same dark header block as the title bar, an "Open on GitHub"
   link, and an always-expanded five-signal score breakdown in its own footer (every Hidden Gems item
   carries a `scoreBreakdown`, so this always renders here).
3. Click the dialog's close (X) button. **Expect:** it closes and the underlying page is fully
   interactive again. Reopen it and click the dimmed backdrop outside the dialog surface instead.
   **Expect:** same result (`MatDialog`'s default backdrop-click-to-close behavior).

### TC-011-15 (Edge case) — Card's own interactive controls don't also open the detail dialog
Narrowed 2026-08-04: the "Why this score?" panel step below no longer applies — that control was
removed from the card the same day (see TC-011-14's own note) — leaving only the bookmark toggle and
the "Open on GitHub" link as the card's interactive controls to check.
1. Click the bookmark toggle on a card. **Expect:** it toggles as usual (TC-011-03) and the detail
   dialog does **not** open.
2. Click a card's "Open on GitHub" link. **Expect:** it navigates (new tab) and the detail dialog does
   **not** open.

### TC-011-16 (Edge case) — Paginator page-size options
Added 2026-08-04 (operator: "the items per page should be a dropdown with options for 24, 48 and 64
items").
1. Open the paginator's items-per-page control.
2. **Expect:** exactly three options — 24, 48, 64 — not a single option matching whatever page size
   happens to already be selected. Selecting a different one re-fetches with the new `pageSize` and
   resets to page 1.

---

## F-012 — Bookmarking

Originally titled "Bookmarking (dedicated Bookmarks view)" - that dedicated view was decommissioned
2026-08-03 (see TC-012-01/02/03/04/06 below), leaving only the bookmark toggle itself (still F-011's
own feature, TC-011-03) and the "Bookmarked only" filter (TC-011-02, TC-012-05) as F-012's live
surface.

### TC-012-01 (Removed 2026-08-03) — Dedicated Bookmarks view lists bookmarked repos
This scenario covered the standalone `/bookmarks` route's own card-grid listing, decommissioned per
the operator's direction ("i dont think we need the bookmarks tab either since its a filter on the
hidden gems tab") — accurate: Hidden Gems' existing "Bookmarked only" filter (TC-012-05) surfaces the
identical set. Kept as a removed placeholder (ID not reused), same precedent as TC-011-11.

### TC-012-02 (Removed 2026-08-03) — Nav pill is live, not a ghost
This scenario covered the "Bookmarks" nav entry itself, gone along with the view it routed to. Kept
as a removed placeholder (ID not reused), same precedent as TC-011-11.

### TC-012-03 (Removed 2026-08-03) — Empty bookmarks state
This scenario covered the dedicated view's own bookmarks-specific empty-state copy; with the view
gone, an empty "Bookmarked only" filter on Hidden Gems now renders the ordinary, already-covered
TC-011-06 filter-empty state instead (a behavior change, not a gap — there's no longer a
filter-bar-less view to need different copy for). Kept as a removed placeholder (ID not reused), same
precedent as TC-011-11.

### TC-012-04 (Removed 2026-08-03) — Un-bookmarking from the Bookmarks view removes it immediately
This scenario covered a behavior specific to the dedicated view (a card leaving the grid on
un-bookmark, since that view had no other reason to list it). On Hidden Gems, un-bookmarking a card
while "Bookmarked only" is active removes it from view the same way, via the same underlying
optimistic-toggle mechanism (TC-011-03) filtered by the existing `bookmarkedOnly` facet - not a
distinct behavior needing its own scenario. Kept as a removed placeholder (ID not reused), same
precedent as TC-011-11.

### TC-012-05 (Regression-sensitive) — "Bookmarked only" filter reflects current bookmark state
Originally covered cross-view sync between Hidden Gems and the now-removed dedicated Bookmarks view;
retargeted in place to the single remaining view now that there's only one (capability persists, only
the vehicle changed — not marked Removed, same precedent as TC-010-01's Discovery Feed retargeting).
1. On Hidden Gems, bookmark a repo, then toggle "Bookmarked only" on. **Expect:** the repo appears.
2. Un-bookmark it from within the filtered view. **Expect:** it disappears from the current result
   set (its `IsBookmarked` flag flipped, so it no longer matches `bookmarkedOnly=true` on the next
   fetch) — no stale "still bookmarked" toggle or lingering card from a cached prior fetch.

### TC-012-06 (Removed 2026-08-03) — List-fetch error state
This scenario covered `GET /api/bookmarks` specifically, an endpoint fully removed along with the
dedicated view it alone served — Hidden Gems' own request-failure handling (TC-011-06) already covers
the "Bookmarked only" filter, since it's just another facet on the same endpoint. Kept as a removed
placeholder (ID not reused), same precedent as TC-011-11.

---

## Version History
| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v1 | 2026-07-31 | Initial draft covering Phase 0 (F-001, F-002, F-003) | Orchestrator Step 0.0 gap — no test-cases-doc existed at build handoff |
| v2 | 2026-08-02 | Added Phase 1 scenarios: TC-004 (Data Store schema), TC-005 (GitHub Crawler), TC-006 (Job Scheduler), TC-007 (Scoring Engine, including the five-signal independence check added after the star-count amendment) | Orchestrator Step 0.0 gap — test-cases-doc hadn't been extended past Phase 0 when Phase 1 features completed |
| v3 | 2026-08-02 | TC-006-01 updated: Hangfire dashboard access control removed (F-006), so the `?key=` requirement and the access-denied assertion no longer apply | Operator: "remove the auth for hangfire" |
| v4 | 2026-08-02 | Added Phase 2 scenarios: TC-008 (Summarizer, including score-to-summarize chaining and a Manual live-LM-Studio quality/throughput check), TC-009 (Trend Aggregator, including the upsert-idempotency and summarize-to-aggregate chaining checks), TC-018 (Dashboard UX design brief, documentation-only) | Orchestrator Step 0.0 gap — test-cases-doc hadn't been extended past Phase 1 when Phase 2 features completed (same gap-closure pattern as v2) |
| v5 | 2026-08-02 | Added F-010 scenarios (TC-010): Discovery Feed/Hidden Gems/Trending/Categories filter-sort-paginate contract, bookmark CRUD + idempotency, topic filtering, and two regression checks specific to F-010's two schema additions (`FirstDiscoveredAtUtc` set-once, latest-not-highest score sort) | Orchestrator Step 0.0 gap — test-cases-doc hadn't been extended for F-010 when it completed (same gap-closure pattern as v2/v4); F-010 was run as a standalone slice of Phase 3, not the full phase |
| v6 | 2026-08-02 | Added F-011 scenarios (TC-011): four-view navigation, filter/sort end-to-end, bookmark toggle optimistic/undo/retry, Trending server-order rendering, Categories grid/drill-down, loading/empty/error states, pagination-beyond-last-page, "Summary pending" no-layout-jump, 960px responsive collapse (filter bar → sidenav, nav → bottom pills), category-name URL-encoding, reserved F-012/v2 placeholder inertness, and a Manual live-publish smoke test for FR-009 AC3 | Orchestrator drafted this section directly, before dispatching the Integration Agent, per this skill's own Step 0.0 gap-closure pattern (same as v2/v4/v5) — stated explicitly to both the Integration and Reviewer-Integration Agents in their prompts to avoid the misattribution the F-010 run's Reviewer-Integration initially made (`docs/handoff.md` "Important context") |
| v7 | 2026-08-03 | Added F-012 scenarios (TC-012): dedicated `/bookmarks` view lists bookmarks server-ordered, live nav pill replacing the F-011 ghost placeholder, bookmarks-specific empty state, un-bookmark-removes-card-from-this-view behavior (distinct from the generic toggle check), cross-view bookmark-state sync, and list-fetch error state | Orchestrator Step 0.0 gap-closure — test-cases-doc had no F-012 coverage before this feature's Task Packet was generated (same pattern as v2/v4/v5/v6), drafted before dispatching the Developer Agent this time (F-012's Task Packet references these scenarios directly) |
| v8 | 2026-08-03 | TC-011-11 updated: removed its now-contradicted assertion that the "Bookmarks · F-012" nav pill is present/disabled (F-012 replaced it with a live nav entry — TC-012-02), retitled to "Reserved v2 placeholder is inert", and kept the still-accurate "Search (v2)" inert-placeholder assertion | F-012 Integration pass, retry after Reviewer-Integration flagged the internal TC-011-11/TC-012-02 contradiction |
| v9 | 2026-08-03 | Categories tab decommissioned: TC-010-04 narrowed to the still-live Categories list endpoint only (drill-down step removed); TC-011-01 narrowed to three required views; TC-011-03/TC-012-01/TC-012-05 no longer mention Categories/a Category drill-down as a card source; TC-011-05 and TC-011-10 marked Removed (IDs kept, not reused, same precedent as TC-011-11) | Operator: "make category a filter and get rid of the category tab" — implemented directly via Claude Code, not an orchestrated run |
| v10 | 2026-08-03 | Trending tab decommissioned and merged into Hidden Gems: TC-010-03 and TC-011-04 marked Removed (same precedent as TC-011-11/TC-011-05/TC-011-10) — `/api/trending` is fully removed, unlike `/api/categories`; TC-011-01 narrowed to two required views; TC-011-02/TC-011-03/TC-012-01/TC-012-05 no longer mention Trending as a card source or view. New TC-010-11 (backend: `HiddenGemCardDto.TrendGrowth` computation — growth percentage, single-period fallback, null-when-no-data) and TC-011-13 (frontend: the trend-growth chip renders/omits correctly) cover the replacement functionality | Operator: "merge trending, add the trending score to the repo card on the hidden gems and then remove the trending tab as well" — implemented directly via Claude Code, not an orchestrated run |
| v11 | 2026-08-03 | Discovery Feed tab decommissioned: `/api/discovery-feed` is fully removed, like Trending, since `GetHiddenGems` already covers the same shared D4 contract as a superset. TC-010-01 retargeted from Discovery Feed to Hidden Gems in place (capability persists, only the vehicle changed — not marked Removed, unlike Trending/Categories' own dedicated scenarios which covered feature-specific behavior that's genuinely gone); TC-010-04/05/07/08 no longer mention Discovery Feed. TC-011-01 narrowed to Hidden Gems as the sole required view (Bookmarks called out as a separate, non-FR-009 nav entry); TC-011-02 narrowed to Hidden Gems alone (was: Discovery Feed then repeated on Hidden Gems); TC-011-03/09/TC-012-01/05 no longer mention Discovery Feed as a card source or view | Operator: "Discovery Feed: remove it. there isnt much difference between that and the hidden gems." — implemented directly via Claude Code, not an orchestrated run |
| v12 | 2026-08-03 | New TC-011-14 (card click opens the repository detail pane per design brief §09 — full summary, topics, score breakdown) and TC-011-15 (a card's own interactive controls — bookmark toggle, score panel, GitHub link — don't also trigger it) | Operator: "adjust the ui of repo card... click to open details pane. see 09 in the Dashboard Design.dc.html" — implemented directly via Claude Code, not an orchestrated run |
| v13 | 2026-08-03 | F-012's dedicated Bookmarks view decommissioned: TC-012-01/02/03/04/06 marked Removed (same precedent as TC-011-11/05/10/TC-010-03) — the view is gone, its "Bookmarked only" filter equivalent already existed on Hidden Gems; TC-012-05 retargeted in place to that filter (capability persists, same precedent as TC-010-01's Discovery Feed retargeting) rather than marked Removed. TC-011-01 updated: primary nav is now exactly one entry ("Hidden Gems"), not a separate FR-009-views-vs-Bookmarks-nav-entry distinction. TC-011-03/14 no longer mention Bookmarks as a second card source | Operator: "i dont think we need the bookmarks tab either since its a filter on the hidden gems tab" — implemented directly via Claude Code, not an orchestrated run |
| v14 | 2026-08-04 | Full documentation sync pass — several rounds of direct, operator-directed UI/backend changes on 2026-08-04 had left this doc describing behavior that no longer exists. TC-008-01 amended for the two-summary split (short + detailed, one `Summary` row, two LM Studio calls); new TC-008-09 for the README-length cap (`Summarization:MaxReadmeCharacters`) added after a live `openclaw/openclaw` context-window failure. TC-010-11 rewritten: `TrendGrowth` is now computed per repository from its own `Score` history, not per language/category from `TrendAggregate` (operator: "Trend is currently calculated per language. I want it to be calculated per repository"). TC-011-02 no longer claims the card shows a "Why this score?" panel or a trend chip (both removed from the card, consolidated into the detail dialog). TC-011-04's forward-pointer and TC-011-13 both corrected/retargeted for the same per-repository trend change (TC-011-13 now describes the dialog's chip, not a card-level one). TC-011-09 corrected: there is no bottom nav to collapse (the primary nav was removed entirely once Hidden Gems became the sole page) — only the filter bar collapses; added a step 4 regression check for the narrow-viewport GitHub-link-showing-through-the-sidenav defect (operator-confirmed fixed). TC-011-14/15 updated for the drawer→`MatDialog` conversion and the removed card-level score panel. New TC-011-16 for the paginator's 24/48/64 page-size dropdown. `docs/prd.md` (v8 — US-8 reworded), `docs/architecture.md` (v22 — §3 Web Dashboard), and `docs/project-management.md` (v29+) were also found with a separate, unrelated drift while doing this pass: each doc's own header `Version` marker had silently fallen behind its own changelog table (e.g. Architecture's header still read v18 while its table already reached v22) — fixed in each file alongside its content corrections | Operator: "now update all documents for whatever has been implemented so far" |

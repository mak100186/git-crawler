# Test Cases: GitHub Hidden Gems Discovery Platform

> Status: ACTIVE
> Version: v7
> Last updated: 2026-08-03
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
2. **Expect:** a `Summary` row is written for the repository (`GeneratedAtUtc` set), and the request
   passed to `IRepositorySummarizer` carries the repo's `Owner`/`Name` — satisfies F-008's "generates
   a structured summary for top-scored repos without one" AC.

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

### TC-010-01 (Happy path) — Discovery Feed filters, sorts, and paginates
1. Seed repositories with varied `PrimaryLanguage`, `StarCount`, `Topics`, `LicenseIdentifier`, and
   `FirstDiscoveredAtUtc`. Call the Discovery Feed endpoint with a combination of `language`,
   `minStars`/`maxStars`, `topic`, and `license` filters plus `sort=Newest&direction=Desc`.
2. **Expect:** only repositories matching all supplied facets (AND across facets, OR within a
   multi-value facet) are returned, ordered by `FirstDiscoveredAtUtc` descending, `page`/`pageSize`
   honored (default `pageSize` 24) — satisfies FR-004 and D4 of F-010's Task Packet.
3. Omit all filters. **Expect:** every repository is returned in default order (`Newest desc`).

### TC-010-02 (Happy path) — Hidden Gems exposes the FR-005 weighted signal breakdown
1. Seed a scored repository, call the Hidden Gems endpoint.
2. **Expect:** the response's score-breakdown block reports each of the five signals
   (license/commits-per-week/contributor count/fork count/star count) alongside the exact
   `ScoringWeights` constants (0.18/0.27/0.225/0.225/0.10) and the `TotalScore` — not just a single
   aggregate number. Default sort is `Score desc`.

### TC-010-03 (Happy path) — Trending's contributing repos mirror F-009's own membership rule
1. Seed repositories matching `AggregateTrendsCommandHandler`'s criteria (scored + summarized,
   non-null `PrimaryLanguage`) for one category, plus a repo that is scored but not summarized in
   the same category. Call the Trending endpoint and expand that trend's contributing-repos list.
2. **Expect:** the scored-but-not-summarized repo is excluded — the read-side membership check is
   byte-for-byte identical to F-009's own write-side rollup criteria (D3).

### TC-010-04 (Happy path) — Categories list and drill-down
1. Seed `TrendAggregate` rows for two categories, call the Categories endpoint.
2. **Expect:** one entry per distinct category reflecting the latest period's `RepositoryCount`/
   `AverageScore`.
3. Call the drill-down endpoint for one category. **Expect:** it returns the same shape as Discovery
   Feed, scoped to `PrimaryLanguage == category`, with the full D4 filter/sort contract still usable
   within that scope (D2).

### TC-010-05 (Happy path) — Bookmark create/list/delete round-trip
1. Call create-bookmark for a repository, then list-bookmarks, then delete-bookmark for the same
   repository, then list-bookmarks again.
2. **Expect:** the repository appears in the list after create and is absent after delete; a repo
   card's `IsBookmarked` flag flips accordingly on the Discovery Feed/Hidden Gems endpoints in
   between (FR-007).

### TC-010-06 (Edge case) — Bookmark idempotency
1. Call create-bookmark twice in a row for the same repository.
2. **Expect:** no constraint-violation error on the second call (the unique index on
   `Bookmark.RepositoryId` is respected without surfacing a 409/500).
3. Call delete-bookmark for a repository that was never bookmarked.
4. **Expect:** no error — a defined, documented idempotent response either way.

### TC-010-07 (Edge case) — Topic filter and repos with no topics
1. Seed one repository with `Topics` containing a known value and one with an empty `Topics` list.
   Filter Discovery Feed by that topic value.
2. **Expect:** only the matching repository is returned; the empty-`Topics` repository never matches
   any topic filter and never errors when `Topics` is empty.

### TC-010-08 (Regression-sensitive) — Score/Commits sort uses the latest score, not the highest ever
1. Seed a repository with two `Score` rows: an earlier high `TotalScore`/`CommitsPerWeek` and a
   chronologically later (by `ComputedAtUtc`) lower one. Call Hidden Gems sorted by `Score desc` and
   Discovery Feed sorted by `Commits desc`.
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

---

## F-011 — Web Dashboard

### TC-011-01 (Happy path) — Four required views render and navigate
1. Load the dashboard. **Expect:** the default route lands on Discovery Feed; the primary nav has
   exactly four entries in order (Discovery Feed → Hidden Gems → Trending → Categories); each entry
   navigates to its own view, all composed from Angular Material components (AC1, FR-009).

### TC-011-02 (Happy path) — Filter/sort controls work end-to-end (Discovery Feed / Hidden Gems)
1. On Discovery Feed, select a language via the Language facet (`mat-select multiple`), narrow the
   Star range slider, add a topic via the autocomplete, select a license.
2. **Expect:** each selection renders as a removable chip in the active-filter `mat-chip-set`; the
   grid re-fetches from `GET /api/discovery-feed` with the matching `language[]`/`minStars`/
   `maxStars`/`topic[]`/`license[]` query params (AC2, FR-004).
3. Change the sort control (`mat-button-toggle-group`) and flip the direction icon button.
   **Expect:** the request's `sort`/`direction` params update and results re-order accordingly.
4. Click "Clear all". **Expect:** all chips disappear and the grid returns to the unfiltered,
   default-sorted (Newest desc) result set.
5. Repeat steps 1-4 on Hidden Gems. **Expect:** identical facet/sort behavior, default sort is Score
   desc, and each card additionally shows the score badge and an expandable "Why this score?"
   breakdown panel (five signals + weights, sourced from `HiddenGemCardDto.scoreBreakdown`).

### TC-011-03 (Happy path) — Bookmark toggle, optimistic UI, undo/retry
1. On any card (Discovery Feed, Hidden Gems, a Category drill-down, or a Trending card's expanded
   contributing-repo row), click the bookmark toggle.
2. **Expect:** the icon flips immediately (optimistic), a `mat-snack-bar` confirms ("Added to
   bookmarks") with an "Undo" action, and `POST /api/repositories/{id}/bookmark` fires.
3. Click "Undo" on the snack-bar. **Expect:** the icon flips back and `DELETE
   /api/repositories/{id}/bookmark` fires.
4. Simulate the bookmark API call failing (e.g. stop the backend mid-request). **Expect:** the icon
   reverts to its prior state and the snack-bar shows the error variant ("Couldn't save bookmark —
   try again") with a "Retry" action (FR-007).

### TC-011-04 (Happy path) — Trending view renders server order, no client-side re-sort
1. Seed `TrendAggregate` rows so `/api/trending` returns trends in a specific `AverageScore`-descending
   order. Load the Trending view.
2. **Expect:** trends render in exactly the order the API returned (no filter/sort bar present on
   this view); expanding a trend's `mat-expansion-panel` lists its `contributingRepositories` as
   rows (name, language, stars, bookmark toggle), matching F-009's own membership rule.

### TC-011-05 (Happy path) — Categories grid and drill-down
1. Load Categories. **Expect:** one clickable `mat-card` tile per category (label + repo count).
2. Click a tile. **Expect:** navigation to `/categories/{category}` renders Discovery Feed's exact
   card-grid + `mat-paginator` layout, with the category pre-applied as a filter chip and the
   filter/sort bar otherwise fully usable (additional language/star/topic/license narrowing composes
   with the pinned category, per D2/D4).

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
1. Resize the viewport below 960px on Discovery Feed or Hidden Gems.
2. **Expect:** the primary nav collapses to a bottom floating pill nav (brand-only toolbar); the
   filter/sort bar collapses to a single "Filters · N" button (N = active filter count) that opens a
   `mat-sidenav` containing the same controls the desktop layout shows inline; active filter chips
   remain visible inline next to the trigger button regardless of panel state.
3. Resize back above 960px. **Expect:** both the nav and filter bar return to their desktop layout
   with selection state preserved.

### TC-011-10 (Edge case) — Category name requiring URL encoding
1. Drill into a category whose name contains characters needing URL encoding (e.g. a space or `+`,
   such as a `C++`-derived `PrimaryLanguage` value).
2. **Expect:** the `/categories/{category}/repositories` request encodes the segment correctly and
   the drill-down resolves to the matching category, not a 404 or a mismatched filter.

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

---

## F-012 — Bookmarking (dedicated Bookmarks view)

### TC-012-01 (Happy path) — Dedicated Bookmarks view lists bookmarked repos
1. Bookmark two or three repos from any of the four existing views (Discovery Feed, Hidden Gems,
   Trending, Categories), then navigate to the new `/bookmarks` route.
2. **Expect:** the same card-grid layout used elsewhere in the dashboard renders exactly the
   bookmarked repos, most-recently-bookmarked first (matching `GET /api/bookmarks`'s server-side
   ordering — `ListBookmarksQueryHandler` orders by each repo's max `Bookmark.CreatedAtUtc` desc).

### TC-012-02 (Happy path) — Nav pill is live, not a ghost
1. Inspect the primary nav (desktop) and bottom pill nav (narrow viewport).
2. **Expect:** the "Bookmarks · F-012" dashed, disabled placeholder pill from F-011 (TC-011-11) is
   gone, replaced by a real "Bookmarks" nav entry that routes to `/bookmarks` and highlights active
   like the other four entries.

### TC-012-03 (Edge case) — Empty bookmarks state
1. Navigate to `/bookmarks` with zero bookmarks saved.
2. **Expect:** the shared empty-state treatment (per TC-011-06) renders with bookmarks-specific
   copy, not the generic "No repositories match these filters" filter-oriented text (there is no
   filter bar on this view to clear).

### TC-012-04 (Happy path) — Un-bookmarking from the Bookmarks view removes it immediately
1. From `/bookmarks`, toggle a card's bookmark off via the existing `BookmarkToggle` control.
2. **Expect:** the same optimistic toggle + snack-bar Undo/Retry behavior as elsewhere
   (TC-011-03) — but specific to this view, the card also leaves the grid once the toggle reaches
   its "off" state (it has no other reason to be listed here), and clicking Undo restores the card
   to the grid rather than just flipping the toggle's own visual state.

### TC-012-05 (Regression-sensitive) — Bookmark state stays in sync across views
1. Bookmark a repo from Discovery Feed, then navigate to `/bookmarks` and confirm it appears.
2. Remove the bookmark from `/bookmarks`, then navigate back to Discovery Feed (or Hidden Gems /
   Trending / a Category) and locate the same repo's card.
3. **Expect:** the card's bookmark toggle reflects the removed state (each view refetches or
   otherwise reflects current server state on navigation — no stale "still bookmarked" toggle from
   a cached prior fetch).

### TC-012-06 (Edge case) — List-fetch error state
1. Simulate the `GET /api/bookmarks` request failing.
2. **Expect:** the shared error-state treatment (per TC-011-06) renders with a "Retry" button that
   re-issues the request — same pattern as the other views, no unhandled exception or blank page.

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

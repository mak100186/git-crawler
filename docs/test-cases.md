# Test Cases: GitHub Hidden Gems Discovery Platform

> Status: ACTIVE
> Version: v2
> Last updated: 2026-08-02
> Covers: Phase 0 (F-001, F-002, F-003), Phase 1 (F-004, F-005, F-006, F-007)
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
1. Run `make up`, then check Hangfire's dashboard at `/hangfire?key=<HANGFIRE_DASHBOARD_KEY>`
   (the key configured via `.env`'s `HANGFIRE_DASHBOARD_KEY`).
2. **Expect:** HTTP 200 and the dashboard renders; the `discover-repositories` recurring job is
   listed with its configured cron schedule (`Hangfire:CrawlerCronSchedule`, default `0 3 * * *`).
3. Request `/hangfire` with no `key` query parameter, or an incorrect one.
4. **Expect:** access denied (fails closed) — confirms NFR-003's access-control requirement, not
   just that the dashboard exists.

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

## Version History
| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v1 | 2026-07-31 | Initial draft covering Phase 0 (F-001, F-002, F-003) | Orchestrator Step 0.0 gap — no test-cases-doc existed at build handoff |
| v2 | 2026-08-02 | Added Phase 1 scenarios: TC-004 (Data Store schema), TC-005 (GitHub Crawler), TC-006 (Job Scheduler), TC-007 (Scoring Engine, including the five-signal independence check added after the star-count amendment) | Orchestrator Step 0.0 gap — test-cases-doc hadn't been extended past Phase 0 when Phase 1 features completed |

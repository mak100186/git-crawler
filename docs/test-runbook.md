# Test Runbook: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-02
> Covers: Phase 0 (F-001, F-002, F-003), Phase 1 (F-004, F-005, F-006, F-007), Phase 2 (F-008, F-009), Phase 3 (F-010, F-011 so far)

Manual step-by-step verification instructions for each shipped feature. Automated coverage lives
in `src/backend/tests/` (xUnit) and `src/frontend/src/**/*.spec.ts` (Vitest) — this runbook is for
flows that need a human or a running environment to verify (Docker Compose, live LM Studio, etc.).

---

## F-001 — GitHub GraphQL rate-limit budget spike (documentation only)

No running system to verify — this is a research output.

1. Open `docs/spikes/f-001-github-graphql-rate-limit-budget.md`.
2. Confirm §4 has a budget table for both 1,000/day and 5,000/day discovery volumes, with
   headroom/deficit stated numerically.
3. Confirm §5 gives an explicit statement on whether the query shape holds at the 100k+
   (NFR-004) scale-out target.
4. Confirm §6 gives a concrete back-off/retry mechanism (not "retry later").
5. **Edge case:** when F-005 (Crawler) is eventually implemented, its GitHub client must log real
   `rateLimit.cost` values from day one — the spike's cost figures are estimates, not
   measurements, and the budget table needs recalculating against live numbers at that point.

## F-002 — LM Studio inference throughput spike (documentation + operator-executed benchmark)

**Already executed 2026-08-01 — see `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9 for
the recorded results (2.57-2.82s p95 per repo, Pass).** Steps below are for re-running this check
after a model/hardware/LM Studio version change, not a first-time gap.

1. Open `docs/spikes/f-002-lm-studio-throughput-benchmark.md`.
2. Confirm §2 gives a concrete availability check for the (now-superseded) Gemma 4 E4B model, and
   §10 for the live comparison that led to the current pick, Llama 3.2 3B Instruct (ADR-017).
3. **Manual (requires a running LM Studio instance with the model loaded):** follow §3's runnable
   `curl` benchmark methodology (Python's `json` module can substitute for `jq` if unavailable,
   per §9's disclosed tooling note) — 10 runs per README-size tier, capturing mean/p50/p95/max via
   the `awk` aggregation in §3.7 (`sed -n 's/.*total_time_s=\([0-9.]*\)/\1/p'` if `grep -P` fails
   on locale, as it did in this environment).
4. Compare the resulting p95 against the pass/marginal/fail bands in §5 (≤30s p95 = pass,
   30s-2min = marginal, >2min = fail).
5. **Edge case:** if the model is unavailable in LM Studio's catalog, or throughput falls in the
   "fail" band, follow §6's escalation path (supersede ADR-013 or revisit NFR-001) — do not
   silently proceed to implement F-008 (Summarizer) against an unverified assumption.
6. Record any material change against a new spike version (§9's Version History pattern) — PM-004
   itself is closed, this is now a re-verification check, not an open item.

## F-003 — Project scaffolding & Docker Compose skeleton

### Happy path — full stack comes up healthy
1. From the repo root, run `make up` (ADR-016 — not `docker compose up` directly; LM Studio is
   host-installed, not a Compose service, and the Makefile is what brings both up together). Pass
   `LMSTUDIO_MODEL=<identifier>` if you're not using the default (`llama-3.2-3b-instruct`, ADR-017,
   confirmed present via `lms ls` as of 2026-08-01 — re-check if it's missing on your machine).
2. Run `make status` — expect Docker running, Compose services (`app`, `postgres`) both `healthy`,
   and LM Studio responding on its configured port.
3. Run `make health` — expect all four lines `OK` (app `/health`, app `/api/ping`, Postgres, LM
   Studio `/v1/models`) and a `0` exit code. This replaces steps 4-7 below with one command; they're
   kept here as the manual equivalent, useful when you need to see the raw response body/headers
   rather than just pass/fail.
4. `curl http://localhost:<app-port>/` — expect `200` with the Angular dashboard's `index.html`
   (Material CSS variables present in `<head>`).
5. `curl http://localhost:<app-port>/health` — expect `200 Healthy`.
6. `curl http://localhost:<app-port>/api/ping` — expect `200` with a JSON payload
   (`{"status":"ok","serverTimeUtc":"..."}`), proving the Wolverine vertical-slice command bus
   round-trips end-to-end through a live HTTP request, not just at build time.
7. `docker exec <postgres-container> pg_isready` — expect "accepting connections".
8. `curl http://localhost:1234/v1/models` — expect `200`, listing the loaded model under the
   identifier `gitcrawler-summarizer` (see `lms ps`).
9. Tear down: `make down` (stops `app`+`postgres`; LM Studio is left running on the host — it's
   the operator's own application, not this project's to stop). Use `make stop-lmstudio` if you
   specifically want to unload the model this Makefile loaded. Confirm with `make health` — app and
   Postgres should now report `FAIL` (and the target should exit non-zero), LM Studio should still
   report `OK`.

### Edge case — `.env` also drives bare `dotnet run` (outside Docker)
`Program.cs` bridges every flat `.env` name it reads (`GITHUB_TOKEN`, `LMSTUDIO_PORT`,
`LMSTUDIO_IDENTIFIER`, `POSTGRES_PASSWORD`/`POSTGRES_DB`/`POSTGRES_USER`/`POSTGRES_PORT`) to its
hierarchical config key — not just the GitHub token — so this same technique applies to any of
them.
1. Ensure `.env` exists at the repo root with real values set (see `docs/setup.md` §1-2).
2. From `src/backend/GitCrawler.Api/`, run `dotnet run`.
3. `curl http://localhost:<port>/health` — expect `200 Healthy` (confirms the process started
   normally with no `.env`-loading failure).
4. There's no config-inspection endpoint by design (never expose secrets over HTTP) — to verify a
   value actually loaded, temporarily add a masked debug line after the relevant bridge in
   `Program.cs` (e.g. print `.Length` and the last 4 characters only, never the full value — or
   for non-secret values like `LmStudio:BaseUrl`/`LmStudio:Model`, print the value directly), run,
   confirm it matches `.env`, then remove the line before committing.
5. **LM Studio and Postgres reachability in this mode:** bare `dotnet run` still needs LM Studio
   running on the host (ADR-016 — true either way) and Postgres reachable at
   `localhost:$POSTGRES_PORT` — bring it up via `docker compose up -d postgres` (or full `make up`
   and just run the API bare instead of via the `app` container) since `docker-compose.yml` now
   publishes Postgres's port to the host for exactly this case.

### Edge case — backend and frontend build independently
1. From `src/backend/`, run `dotnet build`. Expect 0 errors, all projects targeting `net10.0`.
2. From `src/backend/`, run `dotnet test`. Expect the smoke test in
   `tests/GitCrawler.Api.Tests/SmokeTests.cs` to pass. **Note (post-Phase 1):** this was the only
   test at F-003 scaffolding time; F-004 through F-007 have since added substantial xUnit coverage
   alongside it (43 tests total as of this Integration pass) — expect the full suite to pass, not
   just this one file. See `docs/test-cases.md` for the Phase 1 scenario-to-test mapping; Phase 1
   manual/live flows are not yet in this runbook (tracked separately, pending Finalization).
3. From `src/frontend/`, run `npm run build`. Expect 0 errors, production bundle emitted.
4. From `src/frontend/`, run `npm run lint`. Expect a clean pass.
5. From `src/frontend/`, run `npm run test -- --watch=false`. Expect `app.spec.ts`'s two specs to
   pass (component creates; Material toolbar renders the expected title).

### Regression-sensitive — clean rebuild from scratch
1. `docker compose down -v` (remove all containers/named volumes), then `rm -rf data/postgres/*`
   to also clear Postgres's bind-mounted data directory — unlike a named Docker volume, `-v`
   does not remove a bind mount's host-side contents (see the Known Caveat below).
2. Backend: delete `bin/`/`obj/` under `src/backend/`, then `dotnet restore && dotnet build &&
   dotnet test` from nothing. Expect an identical successful outcome to the first build — no
   dependency on cached NuGet state or a prior `dotnet restore`.
3. Frontend: delete `node_modules/` and `dist/` under `src/frontend/`, then `npm ci && npm run
   build`. Expect an identical successful outcome — no dependency on a prior `npm install`.
4. Re-run the Happy Path steps above against the freshly rebuilt images. Expect identical results.

---

## F-004 — Data Store schema (EF Core)

### Happy path — migrations apply to a fresh PostgreSQL 18.4 instance
1. Ensure `data/postgres/` is empty (fresh database — see F-003's clean-rebuild step).
2. `make up`. Watch the app container's startup logs (`make logs`).
3. **Expect:** no migration errors; the app starts successfully. `Database.Migrate()` runs at
   startup and applies `InitialCreate`, `AddCrawlerRawSignalFields`, and `AddScoreStarCountSignal`
   in order.
4. `docker exec <postgres-container> psql -U $POSTGRES_USER -d $POSTGRES_DB -c '\dt'` — expect
   `Repositories`, `Scores`, `Summaries`, `TrendAggregates`, `Bookmarks`, and `__EFMigrationsHistory`
   tables in the `public` schema.
5. `\d "Repositories"` — expect a unique index on `GitHubId`. `\d "Bookmarks"` — expect a unique
   index on `RepositoryId`.

### Edge case — Hangfire's schema coexists without collision
1. After F-006's job scheduler has run at least once (see F-006's Happy Path below), re-run the
   `psql` schema check.
2. `\dn` — expect a `hangfire` schema alongside `public`, created automatically by
   `UsePostgreSqlStorage`, with no table-name collisions against the `public` schema's EF Core
   tables.

---

## F-005 — GitHub Crawler

### Happy path — discovery and idempotent upsert
1. Ensure `.env`'s `GITHUB_TOKEN` is set to a real token (see `docs/setup.md` §1).
2. Trigger the `discover-repositories` job manually from the Hangfire dashboard
   (`http://localhost:8080/hangfire` → find the job → "Trigger now"), or wait for its scheduled
   run (`Hangfire:CrawlerCronSchedule`, default 3 AM daily).
3. `psql -c 'SELECT "Owner", "Name", "LicenseIdentifier", "ContributorCount" FROM "Repositories" LIMIT 10;'`
   — expect rows matching the configured discovery criteria (`GitHub:DiscoveryLookbackDays`/
   `DiscoveryMinimumStars` in `appsettings.json`), with `LicenseIdentifier` correctly `NULL` for
   unlicensed repos (not a placeholder string).
4. Re-trigger the same job immediately.
5. **Expect:** `SELECT COUNT(*), COUNT(DISTINCT "GitHubId") FROM "Repositories";` — both counts
   equal (no duplicates); `LastCrawledAtUtc` on previously-seen rows has advanced.

### Edge case — contributor-count caching cadence
1. After a first crawl, `SELECT "Owner", "Name", "ContributorCountFetchedAtUtc" FROM "Repositories" LIMIT 5;`
   — expect non-null timestamps.
2. Re-trigger discovery within 7 days. **Expect:** `ContributorCountFetchedAtUtc` unchanged for
   those rows (no redundant REST call) — confirm via Hangfire's job log/duration, which should be
   noticeably faster than the first run once GraphQL discovery dominates the runtime instead of N
   REST calls.

### Regression-sensitive — rate-limit backoff
1. **Manual (hard to force live):** if a `403`/rate-limit response is ever observed in the app
   logs during a real crawl, confirm the log shows a wait until the reported reset time (GraphQL
   `resetAt` or REST `x-ratelimit-reset`), not an immediate retry or an aborted run. See the F-001
   spike §6 for the expected back-off shape; automated coverage of this logic lives in
   `DiscoverRepositoriesCommandHandlerTests` (no live rate-limit trigger needed to verify the code
   path — see `docs/test-cases.md` TC-005-03).

---

## F-006 — Job Scheduler (Hangfire)

### Happy path — dashboard reachable, recurring job registered
1. `make up`, then `curl -I "http://localhost:8080/hangfire"` — expect `200`.
2. Open the dashboard in a browser — expect the `discover-repositories` recurring job listed
   under "Recurring Jobs" with its configured cron expression, and the page rendering fully
   styled (CSS/JS assets load unauthenticated, same as the page itself).

### Happy path — crawl-to-score chaining
1. Trigger `discover-repositories` manually from the dashboard against a database with no existing
   `Score` rows.
2. Watch the dashboard's "Succeeded" job list. **Expect:** shortly after the crawl job completes, a
   `ComputeScoresJob` run appears, chained via `ContinueJobWith` — not on its own independent
   schedule.

### Regression-sensitive — mid-run container restart
1. Trigger a crawl, then immediately `docker compose restart app` while it's in progress.
2. Check the dashboard's job history after the app comes back up. **Expect:** the job's state
   survived the restart (visible in history, not silently vanished); no duplicate `Repository` rows
   result once the affected portion of the crawl completes (per F-005's `GitHubId` upsert).

---

## F-007 — Scoring Engine

### Happy path — score computed from all five signals
1. After a crawl-to-score chain completes (see F-006 above),
   `SELECT "RepositoryId", "HasLicense", "CommitsPerWeek", "ContributorCount", "ForkCount", "StarCount", "TotalScore" FROM "Scores" LIMIT 10;`
2. **Expect:** all five signal columns populated (not null/zero across the board unless the source
   repo genuinely has zero for that signal), `TotalScore` between 0 and 100.

### Edge case — re-scoring on re-crawl
1. Note a repository's current `Score` row count and latest `TotalScore`.
2. Re-trigger discovery (advancing `LastCrawledAtUtc`), then wait for or manually trigger
   `ComputeScoresJob`.
3. **Expect:** a new `Score` row appears for that repository (history preserved, not overwritten) —
   `SELECT COUNT(*) FROM "Scores" WHERE "RepositoryId" = <id>;` increases by 1.

---

## F-008 — Summarizer

### Happy path — score-to-summarize chaining and summary generation
1. Ensure LM Studio is up and the configured model is loaded (`make up`, or `make status`/`make
   health` to confirm — `LmStudio:Model` bridged from `LMSTUDIO_IDENTIFIER`, default
   `llama-3.2-3b-instruct` per ADR-017).
2. After a crawl-to-score chain completes (see F-006/F-007 above) with at least one repository
   scoring at or above `Summarization:MinimumScore` (default 40) and no existing `Summary` row,
   watch the Hangfire dashboard's "Succeeded" job list.
3. **Expect:** shortly after the `ComputeScoresJob` run completes, a `GenerateSummariesJob` run
   appears, chained via `ContinueJobWith` — not on its own independent schedule.
4. `psql -c 'SELECT "RepositoryId", LEFT("Content", 80) AS preview, "GeneratedAtUtc" FROM "Summaries" ORDER BY "GeneratedAtUtc" DESC LIMIT 10;'`
   — expect non-empty `Content` for each row, and only for repositories that met the score
   threshold.

### Edge case — README-missing and per-repo failure handling
1. Pick a repository with no README (or temporarily point at one) among the eligible batch.
2. **Expect:** it still gets a `Summary` row — check the app logs (`make logs`) don't show an error
   for that repo, confirming the 404-is-not-fatal path.
3. **Manual (hard to force live):** if LM Studio is stopped or unreachable during a summarization
   run, confirm the app logs show a `LogWarning` ("Summarization failed for {Owner}/{Name};
   skipping") per affected repo rather than the whole job failing/aborting, and that those repos
   still have no `Summary` row afterward (picked up automatically on the next run). Automated
   coverage of this logic lives in `GenerateSummariesCommandHandlerTests` (see
   `docs/test-cases.md` TC-008-05) — no live LM Studio outage needed to verify the code path.

### Regression-sensitive — create-once, latest-score-wins
1. Note a repository's `Summary` row (if any) and its latest `Score.TotalScore`.
2. Re-crawl and re-score the same repository so a new, lower `Score` row is added below
   `Summarization:MinimumScore`, then wait for/trigger the summarization chain again.
3. **Expect:** `SELECT COUNT(*) FROM "Summaries" WHERE "RepositoryId" = <id>;` is unchanged — a
   `Summary` is never regenerated once it exists, and a repo that's since fallen below threshold
   does not get summarized off a stale high score (see `docs/test-cases.md` TC-008-02/TC-008-04).

---

## F-009 — Trend Aggregator

### Happy path — summarize-to-aggregate chaining and rollup
1. After a summarize step completes (see F-008 above) with at least one repository that now has
   both a `Score` and a `Summary`, watch the Hangfire dashboard's "Succeeded" job list.
2. **Expect:** shortly after the `GenerateSummariesJob` run completes, an `AggregateTrendsJob` run
   appears, chained via `ContinueJobWith` — completing the full crawl → score → summarize →
   aggregate-trends chain (Architecture §3).
3. `psql -c 'SELECT "Category", "RepositoryCount", "AverageScore", "PeriodStart", "PeriodEnd" FROM "TrendAggregates" ORDER BY "RepositoryCount" DESC LIMIT 10;'`
   — expect one row per distinct `PrimaryLanguage` among scored-and-summarized repos, with
   `PeriodStart`/`PeriodEnd` both today's date (default `Trends:PeriodDays` = 1).

### Edge case — excluded repositories
1. Confirm no `TrendAggregate` row's `RepositoryCount` includes repositories that have a `Score` but
   no `Summary` yet, or a `null` `PrimaryLanguage` — cross-check
   `SELECT COUNT(*) FROM "Repositories" WHERE "PrimaryLanguage" IS NULL AND EXISTS (SELECT 1 FROM "Scores" WHERE "RepositoryId" = "Repositories"."Id");`
   against the trend totals; these repos should contribute to neither.

### Regression-sensitive — idempotent re-run (NFR-003)
1. Note the current row count and a specific row's `Id` in `TrendAggregates` for today's period.
2. Trigger the chain again for the same day (e.g. re-run `discover-repositories` and let it flow
   through, or manually re-trigger `AggregateTrendsCommand` if a test harness is available).
3. **Expect:** `SELECT COUNT(*) FROM "TrendAggregates" WHERE "PeriodStart" = CURRENT_DATE AND "PeriodEnd" = CURRENT_DATE;`
   is unchanged (still one row per category) and the previously-noted row's `Id` is the same —
   updated in place, not duplicated. There is no unique DB constraint enforcing this (a deliberate
   Task Packet choice); this depends on the single-threaded Hangfire job's query-then-upsert logic,
   so it's worth re-checking after any change that might make this job run concurrently.

---

## F-010 — Web API

### Happy path — Discovery Feed filter/sort/paginate
1. After a crawl-to-score chain has run at least once (see F-005/F-006 above) so `Repositories` has
   varied `PrimaryLanguage`/`StarCount`/`Topics`/`LicenseIdentifier`/`FirstDiscoveredAtUtc` values,
   `curl "http://localhost:<app-port>/api/discovery-feed"` with no query params.
2. **Expect:** `200` with a `PagedResult<RepositoryCardDto>` body, ordered `FirstDiscoveredAtUtc`
   descending (the endpoint's default `sort=Newest&direction=Desc`), `pageSize` 24 unless overridden.
3. Repeat with combined filters, e.g.
   `curl "http://localhost:<app-port>/api/discovery-feed?language=C%23&minStars=10&topic=cli&license=MIT&sort=Stars&direction=Asc&page=1&pageSize=5"`.
4. **Expect:** only repositories matching every supplied facet (AND across facets), ordered by the
   requested sort/direction, with the requested page/pageSize honored (see `docs/test-cases.md`
   TC-010-01).

### Happy path — Hidden Gems score breakdown
1. `curl "http://localhost:<app-port>/api/hidden-gems"`.
2. **Expect:** `200`, default sort `Score desc`; each card's score-breakdown block reports all five
   signals (license/commits-per-week/contributor count/fork count/star count) with the exact
   `ScoringWeights` constants (0.18/0.27/0.225/0.225/0.10) alongside `TotalScore`, not just the
   aggregate number (TC-010-02).

### Happy path — Trending and Categories
1. After a summarize-to-aggregate chain has run (see F-009 above),
   `curl "http://localhost:<app-port>/api/trending"`.
2. **Expect:** `200`; each trend's contributing-repos list excludes any repo that has a `Score` but
   no `Summary` yet, matching `AggregateTrendsCommandHandler`'s own membership rule byte-for-byte
   (TC-010-03).
3. `curl "http://localhost:<app-port>/api/categories"`.
4. **Expect:** `200`, one entry per distinct category reflecting the latest period's
   `RepositoryCount`/`AverageScore`.
5. `curl "http://localhost:<app-port>/api/categories/<category>/repositories"` (URL-encode the
   category if it contains `+`/`#`, e.g. `C%23`).
6. **Expect:** `200`, same `PagedResult<RepositoryCardDto>` shape as Discovery Feed, scoped to that
   `PrimaryLanguage`, with the same filter/sort contract still usable within that scope — a
   caller-supplied `language` query param is ignored in favor of the route segment (TC-010-04).

### Happy path — bookmark create/list/delete round-trip
1. Pick a `RepositoryId` from a prior Discovery Feed response, then
   `curl -X POST "http://localhost:<app-port>/api/repositories/<id>/bookmark"`.
2. **Expect:** `200` with a `BookmarkDto` body.
3. `curl "http://localhost:<app-port>/api/bookmarks"`. **Expect:** the repository is present.
4. Re-fetch Discovery Feed or Hidden Gems for that repository. **Expect:** its card's `IsBookmarked`
   flag is now `true` (FR-007).
5. `curl -X DELETE "http://localhost:<app-port>/api/repositories/<id>/bookmark"`. **Expect:** `204`.
6. Re-fetch `/api/bookmarks`. **Expect:** the repository is absent again (TC-010-05).

### Edge case — bookmark idempotency
1. `curl -X POST "http://localhost:<app-port>/api/repositories/<id>/bookmark"` twice in a row for
   the same `id`.
2. **Expect:** both calls return `200` — no constraint-violation error on the repeat (the unique
   index on `Bookmark.RepositoryId` is respected without surfacing a 409/500).
3. `curl -X DELETE "http://localhost:<app-port>/api/repositories/<id>/bookmark"` for an `id` that
   was never bookmarked. **Expect:** `204`, not an error (TC-010-06).

### Edge case — topic filter and repos with no topics
1. `curl "http://localhost:<app-port>/api/discovery-feed?topic=<known-topic>"` where `<known-topic>`
   is a value present in at least one repo's `Topics`.
2. **Expect:** only repositories whose `Topics` contains that value are returned; repositories with
   an empty `Topics` array never match and the call never errors (TC-010-07).

### Regression-sensitive — sort uses the latest score, not the highest ever
1. Pick a repository with more than one `Score` row (re-scored after a re-crawl — see F-007's
   Edge case above), where the earlier row has a higher `TotalScore`/`CommitsPerWeek` than the most
   recent one.
2. `curl "http://localhost:<app-port>/api/hidden-gems?sort=Score&direction=Desc"` and
   `curl "http://localhost:<app-port>/api/discovery-feed?sort=Commits&direction=Desc"`.
3. **Expect:** both endpoints rank that repository using its latest (by `ComputedAtUtc`) score, not
   its historical peak — same class of bug F-008's `GenerateSummariesCommandHandler` had before its
   first-round fix (`docs/handoff.md` "Important context"). Automated coverage:
   `GetHiddenGemsQueryHandlerTests.Handle_MultipleScores_UsesLatestByComputedAtUtc_NotHighestEverTotalScore`
   and `GetDiscoveryFeedQueryHandlerTests.Handle_SortByScore_UsesLatestScoreNotHighestEver_RespectsDirection`
   (TC-010-08).

### Regression-sensitive — `FirstDiscoveredAtUtc` is set once, never overwritten
1. Trigger discovery for a repository not yet in the database.
   `psql -c 'SELECT "FirstDiscoveredAtUtc" FROM "Repositories" WHERE "GitHubId" = <id>;'`. **Expect:**
   a non-default timestamp close to "now".
2. Re-trigger discovery for the same repository after a delay (`LastCrawledAtUtc` advances).
3. **Expect:** `FirstDiscoveredAtUtc` is unchanged from step 1 — a repeatedly re-crawled old repo
   must never resurface as "Newest" on `/api/discovery-feed?sort=Newest`. Automated coverage:
   `DiscoverRepositoriesCommandHandlerTests.Handle_NewGitHubId_SetsFirstDiscoveredAtUtcToNow` and
   `Handle_ExistingGitHubId_NeverOverwritesFirstDiscoveredAtUtc` (TC-010-09).

### Edge case — pagination boundaries
1. With a seeded set of `pageSize + 1` matching repositories,
   `curl "http://localhost:<app-port>/api/discovery-feed?pageSize=5&page=1"`, then `page=2`, then a
   page far beyond the last (e.g. `page=999`).
2. **Expect:** page 1 returns a full page of 5, page 2 returns exactly 1 result, and the
   out-of-range page returns an empty result set — `200` with an empty array, never an error
   (TC-010-10).

**Not executed live in this Integration pass** — no `make up` stack was running in this
environment (same category of gap as Phase 1/2's runbook entries above); automated coverage for
every scenario above exists and passes (`GetDiscoveryFeedQueryHandlerTests`,
`GetHiddenGemsQueryHandlerTests`, `GetTrendingQueryHandlerTests`, `GetCategoriesQueryHandlerTests`,
`GetCategoryRepositoriesQueryHandlerTests`, `CreateBookmarkCommandHandlerTests`,
`DeleteBookmarkCommandHandlerTests`, `ListBookmarksQueryHandlerTests`, all SQLite-backed against
real EF Core query translation, not mocked) — an operator should run this section's `curl`/`psql`
steps at least once against a freshly-seeded `make up` stack before relying on F-010 in anything
resembling production.

---

## F-011 — Web Dashboard

Automated coverage for every scenario below lives in `src/frontend/src/**/*.spec.ts` (57 Vitest
specs, all passing as of this Integration pass — see `docs/test-cases.md`'s F-011 section for the
full TC-011-01 through TC-011-11 scenario text). The steps here are the manual/live-browser
equivalent, useful for confirming the built bundle actually renders and behaves this way, not just
that the isolated component/unit tests pass.

### Happy path — four views navigate, filter/sort works end-to-end
1. `make up`, then open `http://localhost:8080/` in a browser.
2. **Expect:** lands on Discovery Feed by default; the primary nav (`mat-toolbar`) has exactly four
   entries in order — Discovery Feed, Hidden Gems, Trending, Categories — each navigating to its own
   route (TC-011-01).
3. On Discovery Feed, select a language via the Language `mat-select`, narrow the star range slider,
   add a topic via the autocomplete, select a license. **Expect:** each selection renders as a
   removable chip; the grid re-fetches from `GET /api/discovery-feed` with matching
   `language[]`/`minStars`/`maxStars`/`topic[]`/`license[]` query params (TC-011-02).
4. Change the sort control and flip the direction icon button. **Expect:** results re-order to
   match. Click "Clear all". **Expect:** chips disappear, grid returns to the default (Newest desc)
   set.
5. Repeat on Hidden Gems. **Expect:** identical facet/sort behavior, default sort Score desc, each
   card shows a score badge and an expandable "Why this score?" breakdown (TC-011-02 step 5).

### Happy path — bookmark toggle, optimistic UI, undo/retry
1. On any repository card, click the bookmark toggle. **Expect:** the icon flips immediately, a
   snack-bar confirms ("Added to bookmarks") with an "Undo" action, and
   `POST /api/repositories/{id}/bookmark` fires (TC-011-03).
2. Click "Undo" on the snack-bar. **Expect:** the icon flips back and
   `DELETE /api/repositories/{id}/bookmark` fires. **Note:** this specific click-through (the actual
   Undo action firing a real reversal call) is not exercised by an automated test — the Vitest specs
   cover the initial optimistic-flip/confirm, the failed-write/Retry path, and the
   already-bookmarked-toggle-to-remove path, but mock the snack-bar's `onAction()` as a
   non-emitting observable (see `bookmark-toggle.spec.ts`) rather than simulating a real Undo click.
   Code review confirms `BookmarkToggle.apply()`'s reversal branch is wired correctly, but this step
   needs a live click to fully confirm.
3. Stop the backend mid-request (or block the network tab) and click the toggle again. **Expect:**
   the icon reverts to its prior state and the snack-bar shows the error variant ("Couldn't save
   bookmark — try again") with a "Retry" action.

### Happy path — Trending and Categories
1. Load Trending. **Expect:** trends render in exactly the order `/api/trending` returned (no
   filter/sort bar on this view); expanding a trend's `mat-expansion-panel` lists its
   `contributingRepositories` as rows (name, language, stars, bookmark toggle) (TC-011-04). **Note:**
   automated coverage (`trending.spec.ts`) confirms the trend list renders and the
   empty-contributing-repositories case doesn't crash, but doesn't independently assert the rendered
   order matches server order byte-for-byte — code review confirms the template does a plain `@for`
   iteration over the API response with no client-side sort/reorder applied.
2. Load Categories. **Expect:** one clickable `mat-card` tile per category (label + repo count).
   Click a tile. **Expect:** navigation to `/categories/{category}` renders the Discovery Feed's
   exact card-grid + `mat-paginator` layout, with the category pre-applied as a non-removable filter
   chip and the rest of the filter/sort bar still usable (TC-011-05).

### Edge case — empty, loading, error states and pagination beyond the last page
1. Filter to zero matches. **Expect:** the centered empty-state card with a "clear all filters"
   button. Toggle "Bookmarked only" with zero bookmarks. **Expect:** the same empty state, not an
   error (TC-011-06).
2. Simulate a failed request (stop the backend). **Expect:** the centered error-state card with a
   "Retry" button that re-issues the request.
3. Request a page far beyond the last page for a small filtered result set. **Expect:** the empty
   state renders, not a crash (TC-011-07, mirrors TC-010-10 at the UI layer).

### Edge case — "Summary pending" placeholder and responsive collapse
1. Load a card whose `summaryContent` is `null`. **Expect:** a fixed-height "Summary pending"
   placeholder in the summary slot. When that repo's summary later becomes available (re-fetch),
   **expect:** the real summary replaces the placeholder with no visible layout shift (TC-011-08).
   **Note:** the layout-shift-absence part of this check is inherently visual and not covered by an
   automated assertion; only the initial null-state rendering is unit-tested.
2. Resize the browser below 960px on Discovery Feed or Hidden Gems. **Expect:** the primary nav
   collapses to a bottom pill nav; the filter/sort bar collapses to a "Filters · N" button opening a
   `mat-sidenav` with the same controls; active filter chips stay visible inline (TC-011-09). Resize
   back above 960px. **Expect:** both return to desktop layout with selection state preserved.
3. Drill into a category whose name needs URL encoding (e.g. a `C++`-derived language). **Expect:**
   the request encodes correctly and resolves to the matching category, not a 404 (TC-011-10).
4. Inspect the nav and filter-bar area. **Expect:** a visibly disabled "Bookmarks · F-012" nav pill
   and a disabled "Search (v2)" field are present but inert — clicking either does nothing
   (TC-011-11).

### Regression-sensitive — live build-and-serve smoke test (FR-009 AC3, TC-011-12)
**Executed live in this Integration pass** (both a .NET 10 SDK and a Node toolchain — v26.5.1/npm
12.0.2 — were available in the environment, so this did not need to be deferred as Manual):
1. `dotnet publish src/backend/GitCrawler.Api/GitCrawler.Api.csproj -c Release -o <dir>` — the
   `BuildAngularApp`/`CopyAngularApp` MSBuild targets ran for real (not just `npm run build` in
   isolation): `npm run build` executed against `src/frontend`, producing
   `dist/dashboard/browser/`, and its contents were copied into `<dir>/wwwroot/`.
2. **Expect:** `<dir>/wwwroot/` contains `index.html`, the built JS/CSS chunks, and `favicon.ico`.
   **Confirmed** — all present.
3. Started the published host (`dotnet GitCrawler.Api.dll`, run from `<dir>` so `wwwroot` resolves
   correctly relative to the content root — see the caveat below) against the already-running
   `postgres` Compose service. `curl http://localhost:<port>/` — **expect** `200` serving the
   dashboard's `index.html`. **Confirmed.**
4. `curl http://localhost:<port>/hidden-gems` (a client-side route requested directly, not via
   in-app navigation) — **expect** `200`, falling back to `index.html` via `MapFallbackToFile` rather
   than 404ing. **Confirmed** — response body was the same `index.html` shell, letting the Angular
   router take over client-side.
5. **Caveat for whoever re-runs this:** run `dotnet <published-dll>` with the publish output
   directory itself as the working directory (as a real deployment/Docker image would), not the repo
   root. ASP.NET Core resolves `WebRootPath` relative to the process's content root (defaulted from
   the working directory when unspecified) — running from the repo root while pointing at a DLL
   elsewhere resolves `wwwroot` to `<repo-root>/wwwroot` (which doesn't exist) instead of
   `<publish-dir>/wwwroot`, producing a spurious 404 that has nothing to do with the actual publish
   pipeline (confirmed by reproducing it, then re-running correctly from the publish directory with
   `POSTGRES_*`/`GITHUB_TOKEN` passed as explicit env vars instead of relying on `.env`
   auto-discovery, which fixed it).
6. **Side effect to note:** this run applied the pending `AddRepositoryTopicsAndFirstDiscoveredAt`
   EF Core migration to the shared local dev Postgres database (it had not yet been applied there,
   despite the long-running `app` container appearing healthy — that container predates the
   migration). This is the legitimate F-010 migration catching up, not a schema change introduced by
   this Integration pass; no data was altered beyond the new columns' defaults (`Topics = '{}'`,
   `FirstDiscoveredAtUtc = -infinity`), consistent with PM-006's already-documented backfill gap.

---

### Known caveats to check when re-running this runbook later
- `postgres:18.4`'s data directory must be mounted at `/var/lib/postgresql` (not the older
  `/var/lib/postgresql/data` convention) — check `docker-compose.yml`'s inline comment if the
  Postgres container fails to start after a Postgres image update.
- Postgres data is bind-mounted to `./data/postgres` (not a named Docker volume), so it survives
  `docker compose down -v` intact by design — delete `data/postgres/*` manually for a truly fresh
  database.
- `Octokit.GraphQL` is pinned to a prerelease (`0.4.0-beta`) since no stable release exists yet —
  re-check NuGet for a stable release periodically.
- LM Studio is host-installed (ADR-016), not a Compose service — `make up` will fail at the
  `check-lmstudio` step if LM Studio isn't installed, or if its `lms` CLI hasn't been enabled
  (LM Studio → Settings → Developer). See `docs/setup.md`.
- `llama-3.2-3b-instruct` is the `LMSTUDIO_MODEL` default (ADR-017), confirmed present via `lms ls`
  on the target machine as of 2026-08-01 — re-check with `lms ls` if it's missing or you're on a
  different machine, and pass `make up LMSTUDIO_MODEL=<identifier>` accordingly. The original pin,
  Gemma 4 E4B (ADR-013), remains downloaded but unused — superseded after live testing found it
  truncated output on reasoning-token overhead (spike §9-§10).
- `docs/diagrams/mmd/daily-discovery-flow.mmd` is stale as of Phase 1: it depicts the Scheduler
  triggering Scoring independently/in parallel with the Crawler, but the actual implementation
  (F-006/F-007) is a single `RecurringJob` (Crawler only) that chains into Scoring via Hangfire
  `ContinueJobWith` — flagged by Phase 1's Integration pass, not yet corrected (requires a manual
  diagramming pass, out of scope for the orchestrator's automated agents).
- Phase 1's live infrastructure checks (F-004's fresh-migration run, F-006's dashboard reachability,
  F-006's mid-run restart persistence) were not executable in the Integration Agent's environment
  (no Docker available there) — automated test coverage exists for the underlying logic, but an
  operator should run this runbook's F-004/F-006 Happy Path and Regression-sensitive steps at least
  once against a real `make up` stack before relying on Phase 1 in anything resembling production.
- Phase 2's Integration Agent environment *did* have Docker available, and found the stack already
  running (`app`+`postgres`, both healthy) with 1,002 crawled repositories and 2,000 `Score` rows
  present — but that running container predates the F-008/F-009 code (its logs show no
  summarization/trend activity at all despite qualifying scores existing) and LM Studio's local
  server was not reachable/could not be started in this session (`lms server start` timed out). A
  live end-to-end run of the F-008/F-009 Happy Path steps above was therefore not attempted — same
  Manual-verification gap as Phase 1's, just for a different reason (LM Studio unavailability
  instead of no Docker). An operator should run F-008/F-009's Happy Path steps at least once against
  a freshly-rebuilt `make up` stack with LM Studio actually running before relying on Phase 2.
- F-011's Integration Agent environment had both a .NET 10 SDK and a Node toolchain available, so
  TC-011-12 (the live `dotnet publish`-and-serve smoke test) was executed for real rather than
  deferred as Manual — see the F-011 section above for the full steps and result (pass: `wwwroot`
  populated correctly, `/` and a direct client-side route both served `index.html`). The only
  F-011 gaps still open are the three noted inline in that section (Undo-click, Trending
  server-order, and "no layout shift" are implementation-reviewed but not independently
  automated-test-asserted) and the pre-existing `daily-discovery-flow.mmd` staleness above — neither
  is new to this pass.

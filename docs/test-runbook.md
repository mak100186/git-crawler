# Test Runbook: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-04
> Covers: Phase 0 (F-001, F-002, F-003), Phase 1 (F-004, F-005, F-006, F-007), Phase 2 (F-008, F-009), Phase 3 (F-010, F-011, F-012 — complete), Phase 4 (F-013, F-014 so far)

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
4. `psql -c 'SELECT "RepositoryId", LEFT("ShortContent", 80) AS short_preview, LEFT("DetailedContent", 80) AS detailed_preview, "GeneratedAtUtc" FROM "Summaries" ORDER BY "GeneratedAtUtc" DESC LIMIT 10;'`
   — expect both `ShortContent` and `DetailedContent` non-empty for each row (split into two
   columns/two separate LM Studio calls 2026-08-04 — was a single `Content` column), and only for
   repositories that met the score threshold.

### Edge case — README length is capped before being sent to LM Studio
Added 2026-08-04 after a live run against `openclaw/openclaw` (111KB README) failed with LM Studio's
`"n_keep: 35489 >= n_ctx: 8192"` — the loaded model's context window, exceeded because nothing capped
the README before that point.
1. Find (or seed) a repository whose README exceeds `Summarization:MaxReadmeCharacters` (default
   6000), then trigger summarization for it.
2. **Expect:** it summarizes successfully rather than failing with a context-length error — confirm
   via the app logs (`make logs`) showing no `LogWarning` for that repo.
3. **If a future failure of this kind does occur:** confirm the app logs now show LM Studio's actual
   response body in the warning (not just a bare "400 Bad Request") — `CallLmStudioAsync` was changed
   the same day to surface it, specifically so this class of failure is diagnosable from the logs
   alone next time, without a manual `curl` repro against LM Studio directly.

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

### Happy path — Hidden Gems filter/sort/paginate
Originally exercised against the Discovery Feed endpoint; `/api/discovery-feed` was removed
2026-08-03 (see the Removed note after the next section) — retargeted to `/api/hidden-gems` since it
already covers the same shared D4 contract as a superset (TC-010-01).
1. After a crawl-to-score chain has run at least once (see F-005/F-006 above) so `Repositories` has
   varied `PrimaryLanguage`/`StarCount`/`Topics`/`LicenseIdentifier`/`FirstDiscoveredAtUtc` values,
   `curl "http://localhost:<app-port>/api/hidden-gems?sort=Newest&direction=Desc"` (overriding Hidden
   Gems' own `Score desc` default).
2. **Expect:** `200` with a `PagedResult<HiddenGemCardDto>` body, ordered `FirstDiscoveredAtUtc`
   descending, `pageSize` 24 unless overridden.
3. Repeat with combined filters, e.g.
   `curl "http://localhost:<app-port>/api/hidden-gems?language=C%23&minStars=10&topic=cli&license=MIT&sort=Stars&direction=Asc&page=1&pageSize=5"`.
4. **Expect:** only repositories matching every supplied facet (AND across facets), ordered by the
   requested sort/direction, with the requested page/pageSize honored (see `docs/test-cases.md`
   TC-010-01).

### Happy path — Hidden Gems score breakdown and trend growth
1. `curl "http://localhost:<app-port>/api/hidden-gems"`.
2. **Expect:** `200`, default sort `Score desc`; each card's score-breakdown block reports all five
   signals (license/commits-per-week/contributor count/fork count/star count) with the exact
   `ScoringWeights` constants (0.18/0.27/0.225/0.225/0.10) alongside `TotalScore`, not just the
   aggregate number (TC-010-02).
3. Re-crawl and re-score a repository already returned by this endpoint (advancing its `Score`
   history to two rows), then re-fetch the same endpoint. **Expect:** that card's `trendGrowth` is a
   non-null string, computed from *that repository's own* two most recent `Score.TotalScore` values —
   either a percentage-change label ("▲ +18% vs. last period") once a second `Score` row exists, or a
   "{score} current score" fallback if it's still on its first (TC-010-11). Changed 2026-08-04
   (operator: "Trend is currently calculated per language. I want it to be calculated per
   repository") — previously this came from `TrendAggregate`, a rollup shared by every repository of
   the same `PrimaryLanguage`, so every repo in a language showed the identical figure regardless of
   its own standing; `TrendAggregate` itself is unchanged and still backs the Categories endpoint
   below, just no longer this field. (This merges what the now-removed standalone Trending view used
   to show separately — see below.)

**Removed 2026-08-03** — this section previously also covered `/api/trending` directly (a trend's
contributing-repos list, matching `AggregateTrendsCommandHandler`'s own membership rule). That
endpoint is gone entirely, not just its dashboard view — nothing else consumed it, unlike
`/api/categories` which stays mapped (see `docs/changelog.md` Revision 10). `/api/discovery-feed` is
gone the same way, same day (see `docs/changelog.md` Revision 11) — unlike Trending, its capability
didn't disappear, it was simply subsumed by `/api/hidden-gems` (see the section above).

### Happy path — Categories list
Changed 2026-08-04: the response shape simplified from `{ category, repositoryCount, averageScore,
periodStart, periodEnd }` to just `{ category }` — the endpoint now queries `Repository
.PrimaryLanguage` directly instead of `TrendAggregate`, since the dashboard's Language filter (the
only consumer) never read the rollup fields.
1. `curl "http://localhost:<app-port>/api/categories"`.
2. **Expect:** `200`, one entry per distinct language among *scored* repositories — just
   `{"category": "..."}` per entry now (TC-010-04). (This endpoint's per-category drill-down sibling,
   `/api/categories/<category>/repositories`, was removed 2026-08-03 along with the dashboard's
   Categories tab it only existed to serve — see `docs/changelog.md` Revision 9. This list endpoint
   itself stays mapped, still backing the dashboard's Language filter option list, just reading from a
   different source now.)

### Happy path — bookmark create/delete round-trip
`GET /api/bookmarks` (the list endpoint) was removed 2026-08-03 along with the dashboard's dedicated
Bookmarks view it alone served (see `docs/changelog.md`) - this round-trip is now verified via Hidden
Gems' own `bookmarkedOnly` filter instead of a separate list call.
1. Pick a `RepositoryId` from a prior Hidden Gems response, then
   `curl -X POST "http://localhost:<app-port>/api/repositories/<id>/bookmark"`.
2. **Expect:** `200` with a `BookmarkDto` body.
3. `curl "http://localhost:<app-port>/api/hidden-gems?bookmarkedOnly=true"`. **Expect:** the
   repository is present, and its card's `IsBookmarked` flag is `true` (FR-007).
4. `curl -X DELETE "http://localhost:<app-port>/api/repositories/<id>/bookmark"`. **Expect:** `204`.
5. Re-fetch `/api/hidden-gems?bookmarkedOnly=true`. **Expect:** the repository is absent again
   (TC-010-05).

### Edge case — bookmark idempotency
1. `curl -X POST "http://localhost:<app-port>/api/repositories/<id>/bookmark"` twice in a row for
   the same `id`.
2. **Expect:** both calls return `200` — no constraint-violation error on the repeat (the unique
   index on `Bookmark.RepositoryId` is respected without surfacing a 409/500).
3. `curl -X DELETE "http://localhost:<app-port>/api/repositories/<id>/bookmark"` for an `id` that
   was never bookmarked. **Expect:** `204`, not an error (TC-010-06).

### Edge case — topic filter and repos with no topics
1. `curl "http://localhost:<app-port>/api/hidden-gems?topic=<known-topic>"` where `<known-topic>`
   is a value present in at least one scored repo's `Topics`.
2. **Expect:** only repositories whose `Topics` contains that value are returned; repositories with
   an empty `Topics` array never match and the call never errors (TC-010-07).

### Regression-sensitive — sort uses the latest score, not the highest ever
1. Pick a repository with more than one `Score` row (re-scored after a re-crawl — see F-007's
   Edge case above), where the earlier row has a higher `TotalScore`/`CommitsPerWeek` than the most
   recent one.
2. `curl "http://localhost:<app-port>/api/hidden-gems?sort=Score&direction=Desc"` and
   `curl "http://localhost:<app-port>/api/hidden-gems?sort=Commits&direction=Desc"`.
3. **Expect:** both calls rank that repository using its latest (by `ComputedAtUtc`) score, not
   its historical peak — same class of bug F-008's `GenerateSummariesCommandHandler` had before its
   first-round fix (`docs/handoff.md` "Important context"). Automated coverage:
   `GetHiddenGemsQueryHandlerTests.Handle_MultipleScores_UsesLatestByComputedAtUtc_NotHighestEverTotalScore`
   (TC-010-08).

### Regression-sensitive — `FirstDiscoveredAtUtc` is set once, never overwritten
1. Trigger discovery for a repository not yet in the database.
   `psql -c 'SELECT "FirstDiscoveredAtUtc" FROM "Repositories" WHERE "GitHubId" = <id>;'`. **Expect:**
   a non-default timestamp close to "now".
2. Re-trigger discovery for the same repository after a delay (`LastCrawledAtUtc` advances).
3. **Expect:** `FirstDiscoveredAtUtc` is unchanged from step 1 — a repeatedly re-crawled old repo
   must never resurface as "Newest" on `/api/hidden-gems?sort=Newest`. Automated coverage:
   `DiscoverRepositoriesCommandHandlerTests.Handle_NewGitHubId_SetsFirstDiscoveredAtUtcToNow` and
   `Handle_ExistingGitHubId_NeverOverwritesFirstDiscoveredAtUtc` (TC-010-09).

### Edge case — pagination boundaries
1. With a seeded set of `pageSize + 1` matching *scored* repositories,
   `curl "http://localhost:<app-port>/api/hidden-gems?pageSize=5&page=1"`, then `page=2`, then a
   page far beyond the last (e.g. `page=999`).
2. **Expect:** page 1 returns a full page of 5, page 2 returns exactly 1 result, and the
   out-of-range page returns an empty result set — `200` with an empty array, never an error
   (TC-010-10).

**Not executed live in this Integration pass** — no `make up` stack was running in this
environment (same category of gap as Phase 1/2's runbook entries above); automated coverage for
every scenario above exists and passes (`GetHiddenGemsQueryHandlerTests` (including its
`TrendGrowth` cases), `GetCategoriesQueryHandlerTests`, `CreateBookmarkCommandHandlerTests`,
`DeleteBookmarkCommandHandlerTests`, all SQLite-backed against real EF Core query translation, not
mocked) — an operator should run this section's `curl`/`psql` steps at least once against a
freshly-seeded `make up` stack before relying on F-010 in anything resembling production.

---

## F-011 — Web Dashboard

Automated coverage for every scenario below lives in `src/frontend/src/**/*.spec.ts` (45 Vitest
specs as of the 2026-08-04 UI-polish round — see `docs/test-cases.md`'s F-011 section for the full
TC-011-01 through TC-011-16 scenario text). The steps here are the manual/live-browser equivalent,
useful for confirming the built bundle actually renders and behaves this way, not just that the
isolated component/unit tests pass.

### Happy path — required view navigates by default, filter/sort works end-to-end
1. `make up`, then open `http://localhost:8080/` in a browser.
2. **Expect:** lands on Hidden Gems by default — the dashboard's sole page. The `mat-toolbar` shows
   only the brand and a reserved (inert) search placeholder — there is no primary nav at all anymore
   (TC-011-01). (Originally four view entries plus a separate F-012 "Bookmarks" nav entry: Categories,
   Trending, and Discovery Feed were removed 2026-08-03 as distinct views; Bookmarks was removed the
   same day, once its dedicated view turned out to be redundant with Hidden Gems' own "Bookmarked
   only" filter; the resulting single-entry "Hidden Gems" nav was itself removed entirely on
   2026-08-04, once a nav with exactly one destination had nothing left to navigate between —
   operator: "remove the hidden gems tab, we only have one page".)
3. On Hidden Gems, select a language via the Language `mat-select`, narrow the star range slider,
   add a topic via the autocomplete, select a license. **Expect:** each selection renders as a
   removable chip; the grid re-fetches from `GET /api/hidden-gems` with matching
   `language[]`/`minStars`/`maxStars`/`topic[]`/`license[]` query params (TC-011-02).
4. Change the sort control and flip the direction icon button. **Expect:** results re-order to
   match (default sort is Score desc). Click "Clear all". **Expect:** chips disappear, grid returns
   to the unfiltered default-sorted set. **Expect also:** each card shows a score badge, an
   owner/discovered-date/star-count subtitle, and a footer with language/license chips plus an "Open
   on GitHub" link — the score breakdown and trend-growth chip both moved into the click-through
   detail dialog on 2026-08-04, no longer shown inline on the card (TC-011-02).

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
4. Visually inspect both snack-bar variants (added 2026-08-04 — a stale comment had claimed this
   styling already existed, but the actual CSS rules were never written until this pass). **Expect:**
   a dark rounded pill (not Material's stock gray rectangular bar) for the success/Undo case, and a
   distinct brick-brown pill for the error/Retry case, both with bold uppercase action text.

### Happy path — detail dialog's trend-growth chip
Retargeted 2026-08-04: the trend-growth chip moved off the card entirely into the click-through
detail dialog the same day (see the "card click opens the repository detail dialog" section below).
1. Open a repository's detail dialog (see below). **Expect:** its chip row renders a trend-growth
   chip with `trendGrowth`'s text — computed from that repository's own `Score` history, not a
   `TrendAggregate` category rollup (see F-010's own trend-growth step above) (TC-011-13).

**Removed 2026-08-03** — this section previously covered the standalone Trending view: "Load
Trending... expanding a trend's `mat-expansion-panel` lists its `contributingRepositories`..."
(TC-011-04). Decommissioned and merged into Hidden Gems per the operator's direction — the
replacement step is above. Also previously covered "Load Categories... click a tile..." (TC-011-05),
the Categories tile grid and its Category drill-down, decommissioned the same way; browsing by
category is unaffected in substance, since the existing Language filter on Hidden Gems already
filters on the same `Repository.PrimaryLanguage` value. The standalone Discovery Feed view was
likewise removed the same day, later on — its own "Load Discovery Feed..." steps have been folded
into the "required view navigates by default" section above rather than kept as a separate
walkthrough, since Hidden Gems now covers the same ground.

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
2. Resize the browser below 960px on Hidden Gems. **Expect:** the filter/sort bar collapses to a
   "Filters · N" button opening a `mat-sidenav` with the same controls; active filter chips stay
   visible inline (TC-011-09). There is no nav to collapse (removed entirely 2026-08-04 — see the
   "required view navigates by default" section above) — the sticky toolbar is unchanged at any
   width. Resize back above 960px. **Expect:** the filter bar returns to desktop layout with
   selection state preserved.
3. With the sidenav open at a narrow width, **expect:** the repository grid behind it — including
   every card's "Open on GitHub" link — is fully hidden and non-interactive, not visible/clickable
   through the opened panel. Regression check added 2026-08-04, operator-confirmed fixed, after a
   screenshot showed grid content painting on top of the opened sidenav (see
   `docs/handoff.md`'s "Narrow-viewport filter sidenav" entry for the root cause and fix).
4. **Removed 2026-08-03** — this step previously drilled into a category whose name needed URL
   encoding (TC-011-10); the Category drill-down route no longer exists (see the "detail dialog's
   trend-growth chip" section above for the current Categories/Trending removal notes).
5. Inspect the toolbar and filter-bar area. **Expect:** the disabled "Search (v2)" field is present
   and inert — clicking it does nothing (TC-011-11). (This step originally also checked a live nav
   entry — first a disabled "Bookmarks · F-012" ghost pill, later a live "Bookmarks" entry added by
   F-012, then a single remaining "Hidden Gems" entry once Bookmarks/Categories/Trending/Discovery
   Feed had all folded away — the nav itself was removed entirely 2026-08-04, so there is nothing
   nav-related left to check here at all, only the "Search (v2)" placeholder.)

### Happy path — card click opens the repository detail dialog (design brief §09)
Converted 2026-08-04 from a right-side `mat-drawer` to a centered `MatDialog` (operator: "i want the
overlay to show under the header. And i want this detail pane to be centered like a modal") — steps
below describe the dialog's actual current behavior, not the original drawer's.
1. On Hidden Gems, click a card anywhere except the bookmark toggle or the "Open on GitHub" link (the
   card's own "Why this score?" panel was removed the same day, so there's no third control to
   avoid). **Expect:** a centered dialog opens over the full page, under the sticky header, with a
   dimmed backdrop covering everything else — showing that repo's full untruncated **detailed**
   summary (`detailedSummaryContent`, distinct from the card's own short `summaryContent` since the
   2026-08-04 two-summary split), its topics as a chip list, its language/license chips and star/fork
   counts and trend-growth chip merged into the same dark header block as the title bar, an "Open on
   GitHub" link, and an always-expanded five-signal score breakdown in the dialog's own footer
   (TC-011-14).
2. Click the dialog's close (X) button. **Expect:** it closes and the page is interactive again.
   Reopen it and click the dimmed backdrop outside the dialog surface instead. **Expect:** same
   result (`MatDialog`'s default backdrop-click-to-close behavior).
3. Click the bookmark toggle, then click "Open on GitHub" on a card, in turn. **Expect:** each behaves
   as usual (toggle flips, link navigates) and neither also opens the detail dialog (TC-011-15).
4. Resize below 720px and reopen the dialog. **Expect:** it takes the full viewport width/height with
   square (non-rounded) corners, rather than its default desktop sizing (840px wide, capped at 90% of
   the viewport width and 85% of its height, with rounded corners and a visible shadow).

### Edge case — repo-card summary/footer spacing (operator UI feedback, no dedicated TC)
1. Load Hidden Gems with a card whose summary is long enough to wrap. **Expect:** up to 3 lines render
   before truncating (was 2) — a small visual check, not asserted by an automated test.
2. Inspect the divider between a card's body and its chip row. **Expect:** the chip row sits with
   noticeably more breathing room below the divider than before (`padding-top` 9px → 16px).

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

## F-012 — Bookmarking

The dedicated `/bookmarks` view this section originally walked through (`features/bookmarks/`, its
live nav entry, `GET /api/bookmarks`) was decommissioned 2026-08-03 — see `docs/changelog.md` and
`docs/test-cases.md`'s TC-012-01/02/03/04/06 (each marked Removed). Hidden Gems' pre-existing
"Bookmarked only" filter surfaces the identical set of repos, so that filter is now F-012's only
remaining manual-verification surface; bookmark create/toggle itself is F-011's own feature (see that
section above, "Happy path — bookmark toggle, optimistic UI, undo/retry").

### Regression-sensitive — "Bookmarked only" filter reflects current bookmark state (TC-012-05)
1. `make up`, then open `http://localhost:8080/` in a browser (lands on Hidden Gems).
2. Bookmark a repo via its card's bookmark toggle, then toggle the filter bar's "Bookmarked only"
   switch on. **Expect:** the repo appears in the (now-filtered) grid.
3. Un-bookmark that same repo from within the filtered view. **Expect:** it disappears from the
   current result set once the toggle's request completes — no stale "still shown" card from a
   cached prior fetch (no `RouteReuseStrategy` is registered, and this is a single view now, not a
   cross-view navigation, so there's no separate cache layer to introduce staleness).
4. Toggle "Bookmarked only" off. **Expect:** the full, unfiltered result set returns.

---

## F-013 — Digest Service

Automated coverage lives in `SendDigestCommandHandlerTests`/`SendDigestJobTests`
(`src/backend/tests/GitCrawler.Api.Tests/Features/Digest/SendDigest/`), matching TC-013-01 through
TC-013-04. The steps below are for a human/live-environment check of the same scenarios, plus
TC-013-05's live-SMTP-delivery check that no automated test can cover.

### Happy path — daily digest composed and sent (TC-013-01)
1. Seed several scored-and-summarized repositories with varying `TotalScore` (see F-005/F-007/F-008
   above), and let a `AggregateTrendsJob` run at least once (F-009) so at least one `TrendAggregate`
   row exists for today's period.
2. `appsettings.Development.json` already points `Smtp:Host`/`Smtp:FromAddress`/`Digest:RecipientEmail`
   at Mailpit (`make dev` starts it alongside Postgres, per `docs/setup.md` §3a — no credentials
   needed, mirroring `SmtpEmailSender`'s own "no auth header needed for a local service" precedent
   from `LmStudioRepositorySummarizer`), so a bare `dotnet watch run` needs no extra config here. Then
   either wait for the `send-digest` Hangfire RecurringJob to fire (`Hangfire:DigestCronSchedule`,
   default `0 6 * * *`) or trigger it manually from the Hangfire dashboard's "Recurring Jobs" tab
   (`/hangfire`).
3. **Expect:** an email arrives at the configured recipient listing the top-N (`Digest:TopN`, default
   10) highest-*current*-scored repos with their short summary, plus a trend-summary section sourced
   from today's `TrendAggregate` rows.

### Edge case — send failure is logged, not silently dropped (TC-013-02)
1. Point `Smtp:Host` at an unreachable host (or stop the local relay), then trigger `send-digest`.
2. **Expect:** the app logs an `Error`-level entry ("Failed to send the daily digest email to
   {Recipient}") with the underlying exception, and the Hangfire job itself still reports Succeeded
   (the failure is caught and logged inside `SendDigestCommandHandler`, not left to fail the job) —
   confirms FR-006's "failure to send is logged, not silently dropped" without crashing the host.

### Edge case — no eligible repos or trend data (TC-013-03)
1. Trigger `send-digest` against a fresh database (or one with no scored-and-summarized repos and no
   current-period `TrendAggregate` row).
2. **Expect:** the email still sends, with "No hidden gems to report today." / "No trend data
   available for the current period." placeholder lines — never a malformed/empty body, never a
   thrown exception.

### Manual — live SMTP delivery (TC-013-05)
1. **Manual (requires a running `make dev` stack — Mailpit, see `docs/setup.md` §3a):** repeat the
   Happy path above against a real bare `dotnet watch run`, then open `http://localhost:8025/`
   (Mailpit's web UI) and confirm the captured email is legible and complete (subject, top hidden
   gems, trend summaries) end-to-end, not just internally well-formed. Still not executable in the
   Integration Agent's own environment (no Docker access there) — see this runbook's "Known caveats"
   section below — but now runnable by an operator in under a minute via `make dev`, no real mailbox
   or SMTP relay account needed.

---

## F-014 — Observability

Automated coverage lives in `ObservabilityMiddlewareTests`
(`src/backend/tests/GitCrawler.Api.Tests/Infrastructure/Observability/`), matching TC-014-01 through
TC-014-03 — against local fake handler types standing in for the real pipeline stages/Web API queries
(see `ObservabilityHostFixture`'s own header comment for why), since the same production
`ObservabilityMiddleware`/`RecordsProcessedPolicy` classes are registered identically either way. The
steps below are for confirming the same wiring against the real pipeline stages live, plus TC-014-04's
manual stuck-run-diagnosability check.

### Happy path — every command/query stage emits structured stage-level metrics (TC-014-01)
1. `make up`, then watch `make logs` (or `docker compose logs -f app`) while a crawl-to-trend chain
   runs (F-005 through F-009), and while hitting a Web API query endpoint directly (e.g.
   `curl http://localhost:8080/api/hidden-gems`).
2. **Expect:** a `"Starting <MessageType> (<EnvelopeId>)"` line followed by a matching
   `"Completed <MessageType> (<EnvelopeId>) in <N>ms - OK, RecordsProcessed=<N>"` line for every
   command/query invocation — `DiscoverRepositoriesCommand`, `ComputeScoresCommand`,
   `GenerateSummariesCommand`, `AggregateTrendsCommand`, `SendDigestCommand`, and the
   `GetHiddenGemsQuery` HTTP call alike — confirming the middleware wraps every stage platform-wide,
   not just the scheduled pipeline jobs.

### Edge case — failures are captured per stage (TC-014-02)
1. Temporarily misconfigure a stage so its handler throws (e.g. an invalid `GitHub:Token` for
   `DiscoverRepositoriesCommandHandler`, or stop LM Studio mid-run for
   `GenerateSummariesCommandHandler`).
2. **Expect:** the corresponding `"Completed <MessageType> ... - FAILED, RecordsProcessed=0"` log line
   appears with the exception detail attached, and the stage's own pre-existing failure-handling
   behavior (retry, per-repo skip, etc.) is otherwise unaffected — the middleware observes and logs
   only, per its own "never alter control flow" design constraint.

### Regression-sensitive — additive to, not duplicating, the Hangfire dashboard (TC-014-03)
1. Compare the Hangfire dashboard (`/hangfire`, F-006) for a completed job run against the app logs
   for the same run.
2. **Expect:** the Hangfire dashboard shows job-level history (start/end time, succeeded/failed, retry
   count) for the top-level scheduled job only; the app logs additionally show per-invocation
   stage-level detail (`RecordsProcessed`, precise elapsed milliseconds) the dashboard has no
   equivalent for — including for Web API query handlers, which are never Hangfire jobs at all and so
   have no dashboard entry whatsoever.

### Manual — a stuck or rate-limited run is diagnosable from logs alone (TC-014-04)
1. **Manual/simulated (requires reproducing a rate-limited crawl, same trigger as TC-005-03):** while
   `DiscoverRepositoriesCommandHandler` is mid-wait inside its Polly rate-limit retry loop, read only
   the app logs (no debugger attached) and confirm a `"Starting DiscoverRepositoriesCommand"` line
   with no matching `"Completed"` line for an implausibly long duration, alongside that handler's own
   rate-limit warning logs, is enough to identify which stage is stuck and why. Not executable in the
   Integration Agent's environment (no live rate-limited GitHub session available) — see this
   runbook's "Known caveats" section below.

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
  F-011 gaps still open are the two noted inline in that section (Undo-click and "no layout shift"
  are implementation-reviewed but not independently automated-test-asserted — the third original gap,
  Trending's server-order rendering, no longer applies now that the Trending view itself is removed)
  and the pre-existing `daily-discovery-flow.mmd` staleness above — neither is new to this pass.
- F-012's Integration Agent environment did not run a live browser session (would require the full
  `make up`/Docker/Postgres stack, out of scope for that pass) — all 6 TC-012 scenarios above were
  verified via the automated Vitest suite (61/61 passing) plus direct code tracing, not a live
  click-through. An operator should run this section's steps against a real `make up` stack at least
  once, particularly step 4 of the "un-bookmarking removes the card" scenario (confirming the removal
  genuinely persisted server-side via the `BookmarkChangeApiService` DI-override path, not just local
  state) and the cross-view sync check, before treating F-012 as fully live-verified.
- **2026-08-04 documentation sync**: this runbook (and `docs/test-cases.md`) had fallen behind
  several rounds of direct, operator-directed UI/backend changes made the same day — stale references
  to the card's own "Why this score?" panel, a per-category trend chip, a right-side drawer instead
  of the current `MatDialog`, a bottom pill nav that no longer exists, and the `Summary.Content`
  column (renamed to `ShortContent`/`DetailedContent`) were all corrected in this pass. The
  narrow-viewport GitHub-link regression check (F-011's responsive-collapse section, step 3) is
  operator-confirmed fixed, not just applied — see `docs/handoff.md`'s "Narrow-viewport filter
  sidenav" entry.
- **Phase 4 (F-013/F-014) Integration pass**: this runbook had no F-013/F-014 sections at all before
  this pass — the new sections above were added from scratch, not corrected in place. Neither
  feature's live-environment steps were executed in the Integration Agent's environment (no live SMTP
  endpoint or live GitHub rate-limit condition available) — TC-013-05 and TC-014-04 remain Manual/
  not-yet-verified, same status as this doc's other pre-existing Manual scenarios (TC-002-01/02,
  TC-008-08). TC-013-01/02/03 and TC-014-01/02/03's automated-test-equivalent coverage (103/103
  backend tests passing, including 9 `SendDigestCommandHandlerTests`/`SendDigestJobTests` cases and 8
  `ObservabilityMiddlewareTests` cases) was confirmed instead. **Update (same day, operator-directed,
  not orchestrated)**: `make dev` now brings up Mailpit (a dev-only SMTP capture tool,
  `docker-compose.yml`'s `mailpit` service under the `dev` Compose profile) alongside Postgres, and
  `appsettings.Development.json` points `Smtp:Host`/`Digest:RecipientEmail` at it — TC-013-05 is now
  runnable by an operator via `make dev` + `http://localhost:8025/`, closing the "no live SMTP
  endpoint available" gap for anyone outside the Integration Agent's own sandboxed environment (which
  still can't run this, no Docker access there). TC-014-04 (a live rate-limited GitHub crawl) has no
  equivalent local fix and remains genuinely Manual. An operator should still run both live steps at
  least once — F-013's now-easy `make dev` + Mailpit check, and F-014's stuck-run log-only diagnosis —
  before relying on Phase 4 in production.

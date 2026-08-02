# Test Runbook: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-02
> Covers: Phase 0 (F-001, F-002, F-003), Phase 1 (F-004, F-005, F-006, F-007)

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
   (`http://localhost:8080/hangfire?key=<HANGFIRE_DASHBOARD_KEY>` → find the job → "Trigger now"),
   or wait for its scheduled run (`Hangfire:CrawlerCronSchedule`, default 3 AM daily).
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

### Happy path — dashboard reachable, access-controlled, recurring job registered
1. `make up`, then `curl -I "http://localhost:8080/hangfire?key=$HANGFIRE_DASHBOARD_KEY"` — expect
   `200`.
2. `curl -I "http://localhost:8080/hangfire"` (no key) and with a wrong key — expect a non-200
   (access denied, fails closed).
3. Open the dashboard in a browser (with the correct key) — expect the `discover-repositories`
   recurring job listed under "Recurring Jobs" with its configured cron expression.

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

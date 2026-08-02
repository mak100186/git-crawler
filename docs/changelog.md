# Changelog: GitHub Hidden Gems Discovery Platform

> Revision: 2
> Last updated: 2026-08-02

## Revision 2 — 2026-08-02 — Phase 1 complete: core data pipeline

**Features shipped:**
- **F-004** — Data Store schema (EF Core). `GitCrawlerDbContext` with five entities (`Repository`,
  `Score`, `Summary`, `TrendAggregate`, `Bookmark`), three migrations to date (`InitialCreate`,
  `AddCrawlerRawSignalFields`, `AddScoreStarCountSignal`). Hangfire's own job-storage tables are
  created separately by `UsePostgreSqlStorage` (its own `hangfire` schema, not EF-migrated) —
  documented on the DbContext so F-006 didn't duplicate schema setup.
- **F-005** — GitHub Crawler. `Features/Crawling/DiscoverRepositories/` — GraphQL-first discovery
  (`Octokit.GraphQL`) with a REST fallback (typed `HttpClient`) for contributor count; idempotent
  upsert by `Repository.GitHubId`. Implements the F-001 spike's §6 back-off strategy (GraphQL
  `RATE_LIMITED`/`resetAt`, REST `x-ratelimit-*`/`Retry-After`, generic exponential backoff
  otherwise) and §7 mitigation (7-day contributor-count caching cadence) for real, not just in
  documentation.
- **F-006** — Job Scheduler (Hangfire). `AddHangfire`/`UsePostgreSqlStorage`/`AddHangfireServer`
  wired into `Program.cs`; dashboard at `/hangfire` behind a fail-closed shared-secret filter
  (`Hangfire:DashboardAccessKey`/`HANGFIRE_DASHBOARD_KEY` — no auth system exists elsewhere in this
  single-operator v1). One recurring job (`discover-repositories`, daily by default via
  `Hangfire:CrawlerCronSchedule`) triggers the Crawler.
- **F-007** — Scoring Engine. `Features/Scoring/ComputeScores/` — pure computation (no external
  calls), five independently-weighted signals (license 18%, commits-per-week 27%, contributor
  count 22.5%, fork count 22.5%, star count 10% — star count added mid-flight per operator
  direction, weighted secondary to the PRD-committed four). Completes the pipeline chain F-006 left
  open: `DiscoverRepositoriesJob` now attaches `ComputeScoresJob` via Hangfire `ContinueJobWith`
  after each crawl.
- **Operator-directed infra change**: PostgreSQL's Compose volume switched from a named Docker
  volume to a bind mount at `./data/postgres`, so the database persists as visible host files
  across `docker compose down` (not just `-v`-survivable, actually inspectable/backup-able).

**Modules / files affected:**
- `src/backend/GitCrawler.Api/Data/` — new (`GitCrawlerDbContext`, 5 entities, 3 migrations).
- `src/backend/GitCrawler.Api/Features/Crawling/DiscoverRepositories/` — new (command/handler,
  `IGitHubDiscoveryClient`/`GitHubDiscoveryClient`, `RetryDelay`, `DiscoverRepositoriesJob`).
- `src/backend/GitCrawler.Api/Features/Scoring/ComputeScores/` — new (command/handler,
  `ScoringWeights`, `ComputeScoresJob`).
- `src/backend/GitCrawler.Api/Features/Diagnostics/HangfireDashboardAuthorizationFilter.cs` — new.
- `src/backend/GitCrawler.Api/Program.cs` — Hangfire wiring, EF Core DbContext registration +
  startup `Database.Migrate()`, new config bridges (`HANGFIRE_DASHBOARD_KEY`).
- `src/backend/GitCrawler.Api/appsettings.json` — new keys: `Hangfire:CrawlerCronSchedule`,
  `Hangfire:DashboardAccessKey`, `GitHub:DiscoveryPageSize`/`DiscoveryLookbackDays`/
  `DiscoveryMinimumStars`.
- `docker-compose.yml`, `.env.example`, `.gitignore` — Postgres bind-mount; `HANGFIRE_DASHBOARD_KEY`
  plumbed through to the container.
- `docs/test-cases.md` (v2) — TC-004 through TC-007 added, filling a gap where Phase 1 had shipped
  without corresponding test-case scenarios.
- 43 new xUnit tests across `src/backend/tests/GitCrawler.Api.Tests/Data/` and
  `Features/{Crawling,Scoring,Diagnostics}/` (up from 1 smoke test at Phase 0 close).

**Breaking changes:** None.

**Known gaps / follow-ups:**
- Live verification of three scenarios was not possible in the Integration Agent's environment
  (Docker unavailable there): a real migration run against a fresh PostgreSQL 18.4 instance, live
  Hangfire dashboard reachability, and a mid-run container-restart persistence check. Automated
  test coverage exists for the underlying logic in each case (see `docs/test-cases.md` TC-004-01/
  TC-004-02/TC-006-01/TC-006-03) — the live-infrastructure half is a residual gap for the operator
  to close with a real `make up` run before relying on this in production.
- `docs/diagrams/mmd/daily-discovery-flow.mmd` is now stale: it depicts the Scheduler triggering
  Scoring independently/in-parallel with the Crawler, but the actual (and intended) design is a
  single `RecurringJob` (Crawler only) chaining into Scoring via `ContinueJobWith` — flagged by
  Integration, needs a manual diagramming pass.
- The frontend `npm audit` finding from Phase 0 (6 moderate, dev-only) remains open, unchanged this
  phase.

**Smoke tests (see `docs/test-runbook.md` for full steps):**
1. **Happy path:** `make up`, then trigger the `discover-repositories` Hangfire job (dashboard or
   its daily schedule) against a `GITHUB_TOKEN`-configured environment — expect new `Repository`
   rows, followed automatically by a chained `ComputeScoresJob` run producing `Score` rows with all
   five signals populated.
2. **Edge case:** re-run discovery against already-known repositories — expect updates in place
   (no duplicate rows), and contributor-count REST calls skipped for repos fetched within the last
   7 days.
3. **Regression-sensitive:** restart the `app` container mid-crawl — expect Hangfire's
   PostgreSQL-backed job state to survive the restart with no duplicate or dropped work, per
   F-005's idempotent upsert design.

## Revision 1 — 2026-08-01 — Phase 0 complete

**Features shipped:**
- **F-001** — Spike: GitHub GraphQL rate-limit budget validation. Output-only (no code):
  `docs/spikes/f-001-github-graphql-rate-limit-budget.md`. Verdict: risk A1 resolved for
  1K-5K repos/day; conditionally resolved (mitigation needed) at the 100k+ scale-out target,
  where the REST contributor-count fallback (not the GraphQL discovery query) is the binding
  constraint.
- **F-002** — Spike: LM Studio inference throughput benchmark. Output-only (no code):
  `docs/spikes/f-002-lm-studio-throughput-benchmark.md`. Model identifier confirmed live
  (`google/gemma-4-e4b`) and the throughput benchmark itself executed live —
  **2.57-2.82s p95 per repo across three README sizes, Pass vs. NFR-001 with ~10x headroom**. But
  the live run also found `google/gemma-4-e4b` spends 65-86% of a 300-token output budget on
  internal reasoning before the visible summary, truncating it. A live comparison against 4
  already-downloaded alternatives (spike §10) led to a **final model swap: Llama 3.2 3B Instruct**
  (ADR-017, supersedes ADR-013) — faster (0.78-1.05s mean), zero reasoning waste, complete
  natural-stop output. Risk A2 resolved (Architecture §8) for the adopted model.
- **F-003** — Project scaffolding & `make up` skeleton (amended post-scaffold, see below). First
  application code in the repository.

**Modules / files affected:**
- `src/backend/` — new .NET 10 solution (`GitCrawler.sln`), `GitCrawler.Api` project (Wolverine,
  EF Core, Hangfire, Npgsql, Octokit.GraphQL prerelease, `DotNetEnv` 3.2.0 — new, added post-scaffold),
  vertical-slice example at `Features/Diagnostics/Ping/`, `tests/GitCrawler.Api.Tests/` (xUnit
  smoke test harness). `Program.cs` now loads `.env` (via `DotNetEnv`, walking up from the project
  directory) and bridges every flat `.env` name the app reads to its hierarchical config key:
  `GITHUB_TOKEN` → `GitHub:Token`, `LMSTUDIO_PORT` → `LmStudio:BaseUrl`, `LMSTUDIO_IDENTIFIER` →
  `LmStudio:Model`, and `POSTGRES_DB`/`POSTGRES_USER`/`POSTGRES_PASSWORD`/`POSTGRES_PORT` →
  `ConnectionStrings:Postgres` — so `dotnet run` outside Docker reads the same `.env` Docker
  Compose already does. All bridges live-verified both ways (bare `dotnet run` and `make up`) this
  session. The Postgres bridge deliberately carries no `"gitcrawler"`/`"5432"` fallback literals of
  its own (single-source-of-truth pass, same session) — it only fires when all four
  `POSTGRES_DB`/`USER`/`PASSWORD`/`PORT` vars are present, relying on `.env.example` as the sole
  place those defaults are defined. `ConnectionStrings:Postgres` is wired through but not yet
  consumed — no DbContext exists until F-004.
- `src/frontend/` — new Angular 22.1.0 workspace ("dashboard"), standalone components, Angular
  Material + CDK themed, Vitest test harness, angular-eslint wired.
- `docker-compose.yml` (repo root) — **`app` and `postgres:18.4` (pinned) only.** LM Studio is
  **not** a Compose service (ADR-016, amended post-scaffold) — it runs host-installed, since the
  operator already has it installed and running natively; containerizing a second copy would have
  been CPU-only and duplicative. Postgres `DB`/`USER`/`PASSWORD` and the app's `ConnectionStrings__Postgres`
  interpolation now both read from `.env` (`POSTGRES_DB`/`POSTGRES_USER`/`POSTGRES_PASSWORD`);
  Postgres's port is now published to the host (`POSTGRES_PORT`) for local DB clients and bare
  `dotnet run`. `LmStudio__Model` is now also set (from `LMSTUDIO_IDENTIFIER`) — previously only
  `LmStudio__BaseUrl` was. **Single-source-of-truth pass (same session):** every `${VAR:-default}`
  fallback that re-hardcoded a literal `.env.example` already defaults (`POSTGRES_DB`/`USER`/`PORT`,
  `LMSTUDIO_PORT`/`LMSTUDIO_IDENTIFIER`, including inside the Postgres healthcheck's `pg_isready`
  command) was replaced with `${VAR:?...}` — required, fails loudly pointing back at `.env.example`,
  same pattern `POSTGRES_PASSWORD`/`GITHUB_TOKEN` already used. `.env.example` is now the only
  place any of these defaults are spelled out.
- `Makefile` (new, repo root) — `make up`/`down`/`status`/`logs` single entrypoint: checks Docker,
  brings up Compose, checks/starts the host LM Studio server, loads the configured model
  (default `llama-3.2-3b-instruct`, ADR-017) via the `lms` CLI. Sources `.env` automatically
  (`include .env` + `export`) so values set there actually take effect here, not just in
  `docker-compose.yml`'s own separate `.env` handling. Live-verified end-to-end against the actual
  operator machine (Docker Desktop start, LM Studio detection, model load/unload all confirmed
  working, not just written) for both the original and final model pick. **Single-source-of-truth
  pass (same session):** removed the `?=` fallback defaults for `LMSTUDIO_PORT`/`LMSTUDIO_IDENTIFIER`/
  `LMSTUDIO_MODEL` (previously duplicating `.env.example`'s literals) and added a new `check-env`
  target (prerequisite of `up`/`check-lmstudio`/`load-model`) that fails fast with a clear message
  — pointing back at `.env.example` — if `.env` or any required variable is missing, instead of
  silently limping along on a guessed default. Verified both failure modes live (missing `.env`
  entirely, and a single missing variable within an otherwise-complete `.env`) before re-confirming
  the full happy path with another live `make up`/`make down` cycle. **PowerShell/cmd.exe
  compatibility fix (same session, discovered when the operator ran `make up` from a real
  PowerShell window and hit `'test' is not recognized as an internal or external command`):** GNU
  Make on Windows picks its recipe shell by searching the invoking process's `PATH` for `sh.exe` —
  that search only succeeds from a Git Bash session (which adds Git's own bin dirs to `PATH` on
  launch), not from a plain PowerShell/cmd.exe window, where it silently falls back to `cmd.exe`,
  which can't parse these recipes' Unix syntax. Fixed by forcing `SHELL` to Git for Windows'
  bundled `bash.exe` directly on Windows (`ifeq ($(OS),Windows_NT)`), unconditionally — deliberately
  no path-exists probe, since every shell-syntax-based probe attempted (`if exist`, a `where`-based
  one) broke on one side or the other of the cmd/sh divide, the probe itself needing to be written
  in the syntax of whichever shell is currently in effect, which is exactly what's unknown at that
  point. Reproduced the exact failure live via a `cmd.exe` subprocess launched with a minimal,
  Git-`bin`-free `PATH` matching the operator's actual persistent Windows `PATH` (confirmed via
  `[Environment]::GetEnvironmentVariable('PATH','Machine'/'User')`), confirmed the fix resolves it,
  then re-ran the full `make up`/`make down` cycle normally to confirm no regression. Also found and
  restored `.env`'s `POSTGRES_PASSWORD` (blanked at some point during this session's `check-env`
  failure-mode testing) back to its known-good value. **New `make health` target (same session,
  operator request):** unlike `make status` (which only reports whether the underlying
  processes/containers are running), `health` actually probes each component's own endpoint - app
  `/health`, app `/api/ping` (proving the Wolverine command bus round-trips, not just that the
  process is up), Postgres via `docker compose exec postgres pg_isready`, and LM Studio's
  `/v1/models` - printing every result (not stopping at the first failure) and exiting non-zero if
  anything failed, so it doubles as a script/CI gate. Live-verified both outcomes: ran it against
  the fully-up stack (all four OK), then against a torn-down one (`make down` - app/Postgres FAIL,
  LM Studio correctly still OK since `make down` deliberately leaves it running on the host), then
  restored the stack to running.
- `Dockerfile` (3-stage: Angular build → .NET publish → aspnet runtime), `.dockerignore`,
  `.env.example` (now also documents `LMSTUDIO_MODEL` and how to create/configure a GitHub PAT).
- `docs/setup.md` (new) — one-time local setup: prerequisites, GitHub PAT creation (fine-grained
  recommended, classic fallback), `.env` configuration, `make up` walkthrough.
- `docs/adr/ADR-016-lm-studio-host-installed-not-containerized.md` (new) — amends ADR-002 and
  ADR-007's deployment-topology framing; does not change the LM Studio engine choice itself.
- `docs/adr/ADR-017-llama-3.2-3b-instruct-summarization-model.md` (new) — supersedes ADR-013
  (now marked `SUPERSEDED BY ADR-017`); full model comparison and decision record.
- `CLAUDE.md` — rewritten from the stale "no source code yet" placeholder; documents `make up` as
  the canonical entrypoint for both the operator and future Claude Code sessions, plus build/test
  commands and where the governed architecture docs live.
- `docs/spikes/` (new) — F-001 and F-002 output; F-002 now includes §9 (Measured Results for
  Gemma 4 E4B, kept as historical data) and §10 (model comparison + final decision), with title,
  status header, and §7 verdict all updated to reflect the final pick is Llama 3.2 3B Instruct.
- `docs/test-cases.md` (new) — E2E/smoke scenarios for Phase 0; TC-003-04 updated for the
  `make up`-based topology.
- `docs/project-management.md` — F-001/F-002/F-003 → Done; F-002/F-008 AC updated for the final
  model pick; PM-004 closed; PM-005 closed by the model swap (not the `max_tokens` mitigation it
  was originally written around).
- `docs/architecture.md` — risk A2 (§8) marked Resolved; §3 Summarizer and §7 Technology
  Decisions updated to Llama 3.2 3B Instruct / ADR-017.

**Breaking changes:** None — this is the first code in the repository. (The mid-session pivot from
a containerized LM Studio to host-installed is a scope amendment within this same unreleased
revision, not a breaking change to anything previously shipped.)

**Known gaps / follow-ups (tracked in `docs/project-management.md` Open Items):**
- `npm audit` reports 6 moderate vulnerabilities in a frontend devDependency chain
  (`@angular/cli` → `@modelcontextprotocol/sdk` → `@hono/node-server`, Windows-only path
  traversal in a local dev-server adapter). No non-breaking fix exists; the available fix
  downgrades `@angular/cli` to 21.0.4, which would violate ADR-012's Angular 22 pin. Dev-only,
  not present in the production bundle — re-check when Angular tooling publishes a patched
  `@angular/cli` that doesn't require the downgrade.
- EF Core's `DbContext` and Hangfire's `AddHangfire`/`AddHangfireServer` are referenced but
  deliberately left unwired in `Program.cs` pending F-004 (Data Store schema) — wiring a
  live-DB-dependent startup path before the schema exists would make local verification silently
  depend on Postgres already being up.

**Smoke tests (see `docs/test-runbook.md` for full steps):**
1. **Happy path:** `make up` brings up `app` + `postgres:18.4` via Compose and checks/starts the
   host-installed LM Studio, loading the configured model; `make status` confirms all three
   reachable; `GET /` serves the Angular dashboard shell.
2. **Edge case:** `GET /api/ping` round-trips through Wolverine's command bus and returns a JSON
   status payload — proves the vertical-slice convention is wired end-to-end, not just present in
   source.
3. **Regression-sensitive:** a full clean rebuild (`dotnet clean` + removed `bin`/`obj`, `npm ci`
   fresh) reproduces an identical successful build — scaffolding must not depend on stale local
   state.

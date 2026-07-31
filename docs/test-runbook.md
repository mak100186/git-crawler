# Test Runbook: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-07-31
> Covers: Phase 0 (F-001, F-002, F-003)

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
   `tests/GitCrawler.Api.Tests/SmokeTests.cs` to pass (currently the only test — no feature logic
   exists yet to test substantively).
3. From `src/frontend/`, run `npm run build`. Expect 0 errors, production bundle emitted.
4. From `src/frontend/`, run `npm run lint`. Expect a clean pass.
5. From `src/frontend/`, run `npm run test -- --watch=false`. Expect `app.spec.ts`'s two specs to
   pass (component creates; Material toolbar renders the expected title).

### Regression-sensitive — clean rebuild from scratch
1. `docker compose down -v` (remove all containers/volumes).
2. Backend: delete `bin/`/`obj/` under `src/backend/`, then `dotnet restore && dotnet build &&
   dotnet test` from nothing. Expect an identical successful outcome to the first build — no
   dependency on cached NuGet state or a prior `dotnet restore`.
3. Frontend: delete `node_modules/` and `dist/` under `src/frontend/`, then `npm ci && npm run
   build`. Expect an identical successful outcome — no dependency on a prior `npm install`.
4. Re-run the Happy Path steps above against the freshly rebuilt images. Expect identical results.

### Known caveats to check when re-running this runbook later
- `postgres:18.4`'s data directory must be mounted at `/var/lib/postgresql` (not the older
  `/var/lib/postgresql/data` convention) — check `docker-compose.yml`'s inline comment if the
  Postgres container fails to start after a Postgres image update.
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

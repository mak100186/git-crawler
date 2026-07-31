# Test Cases: GitHub Hidden Gems Discovery Platform

> Status: ACTIVE
> Version: v1
> Last updated: 2026-07-31
> Covers: Phase 0 (F-001, F-002, F-003)
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

## Version History
| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v1 | 2026-07-31 | Initial draft covering Phase 0 (F-001, F-002, F-003) | Orchestrator Step 0.0 gap — no test-cases-doc existed at build handoff |

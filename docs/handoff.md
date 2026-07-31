# Handoff: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-01

## What was done

Phase 0 is complete, orchestrated end-to-end via `orchestrator-development-pattern`:

- **F-001** (Spike: GitHub GraphQL rate-limit budget validation) — `docs/spikes/f-001-github-graphql-rate-limit-budget.md`.
  PASS after 1 reviewer iteration. Verdict: risk A1 resolved at 1K-5K repos/day; conditionally
  resolved at the 100k+ scale-out target, where the REST contributor-count fallback (not the
  GraphQL discovery query ADR-004 anticipated) is the actual binding rate-limit constraint.
- **F-002** (Spike: LM Studio inference throughput benchmark) — `docs/spikes/f-002-lm-studio-throughput-benchmark.md`.
  PASS after 2 reviewer iterations (round 1 caught a non-functional benchmark script — a regex
  delimiter mismatch and a printf argument-count bug that would have silently produced zero or
  mislabeled output). Verdict: risk A2 explicitly left open — no live LM Studio access existed in
  this environment, so the deliverable is a runnable operator benchmark plan, not a measurement.
  See PM-004.
- **F-003** (Project scaffolding & Docker Compose skeleton) — PASS on the first attempt, and
  independently re-verified live by both the feature Reviewer and the Integration Agent (real
  `docker compose up`, all three containers healthy, HTTP round-trips confirmed). **Amended
  post-scaffold** (same session, before any commit): the operator clarified LM Studio is already
  installed and running natively on the target machine — containerizing a second copy (the
  original scaffold's `lmstudio/llmster-preview:cpu` service) was pure duplication and CPU-only.
  Replaced with ADR-016: LM Studio now runs host-installed; `docker-compose.yml` manages only
  `app` + `postgres`; a new `Makefile` (`make up`) orchestrates Docker + Compose + the host LM
  Studio server + model loading as one command. Live-verified end-to-end against the actual
  operator machine — Docker Desktop start, LM Studio server detection, and `google/gemma-4-e4b`
  load/unload via the `lms` CLI all confirmed working, not just written. This also incidentally
  confirmed the exact Gemma 4 E4B catalog identifier (`google/gemma-4-e4b`, see the F-002 spike's
  2026-08-01 addendum).
- **F-002's live throughput benchmark was then actually executed** (same session, operator
  request: "run that spike and update the results") — §3's full runnable methodology, for real,
  against `google/gemma-4-e4b`: 30 timed requests (10 per README size) plus the native
  `/api/v0` stats endpoint. Result: **2.57-2.82s p95 per repo, Pass vs. NFR-001 with ~10x
  headroom**; 123.7 tok/s measured (well above the spike's own estimation bracket). But every
  single response hit the 300-token `max_tokens` cap with `finish_reason: "length"` — the model
  spent 65-86% of that budget on an internal `reasoning_content` field before the visible summary,
  truncating output to 30-60 words. See `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9.
- **Model swap decided and executed** (same session, operator request: "use llama-3.2-3b-instruct,
  update docs. update spike and ADRs") — rather than just widen `max_tokens` as a workaround, 4
  already-downloaded alternatives were tested live against the identical truncated request
  (spike §10.1). All 4 produced complete, natural-stop output with zero reasoning-token waste.
  `llama-3.2-3b-instruct` was chosen (fastest: 0.78-1.05s mean, vs. `gemma-4-e4b`'s 2.57-2.82s) and
  given the same full n=10×3-size benchmark rigor as the original (spike §10.2). **ADR-017 (new)
  supersedes ADR-013.** Risk A2 remains **Resolved** in `docs/architecture.md` §8, now for the
  adopted model. PM-004 and PM-005 both closed — PM-005 by the model swap itself, not by the
  `max_tokens`-increase mitigation it was originally written around.
- **Integration** — full test suite, build, and security audit all green after one fix (a
  transitive `Microsoft.OpenApi 2.0.0` High-severity vulnerability, root-cause patched via a
  direct package reference to 2.11.0). One frontend `npm audit` finding (6 moderate, dev-only,
  Windows-only path-traversal in a devDependency chain) left flagged rather than force-fixed,
  since the only available fix downgrades `@angular/cli` and would violate ADR-012's Angular 22
  pin.
- **Reviewer-Integration** — PASS. Independently re-ran the security fix and the Docker stack,
  confirmed the F-002 documentation-drift finding was genuinely dual-recorded (not dropped
  between being found and being reported), and confirmed no test was disabled/loosened to force a
  green result.
- **Documentation drift found and resolved**: F-002's PMBook Acceptance Criteria wording read as
  an absolute ("confirmed available", "benchmarked", "marked resolved") that didn't match the
  Task Packet's own more permissive, already-agreed intent (operator-verification-needed is a
  valid terminal state for a spike with no live LM Studio access). Reworded in
  `docs/project-management.md` v11; added **PM-004** to track running the actual live benchmark
  before F-008.
- **Graphify** — ran over `src/` only (code-scoped, not docs). 168 nodes / 157 edges / 23
  communities. Mostly config-tree structure at this stage (Angular/`.NET` config, one vertical
  slice) since no feature logic exists yet — expected for a scaffold-only codebase. Outputs in
  `graphify-out/` (`graph.html`, `graph.json`, `GRAPH_REPORT.md`).
- New docs this session: `docs/test-cases.md` (Phase 0 E2E/smoke scenarios — none existed at
  handoff, drafted per the orchestrator's Step 0.0 gap check), `docs/changelog.md` (revision 1),
  `docs/test-runbook.md`, `docs/setup.md` (GitHub PAT creation/config + local setup walkthrough,
  new post-scaffold), `docs/adr/ADR-016-lm-studio-host-installed-not-containerized.md` (new),
  `docs/adr/ADR-017-llama-3.2-3b-instruct-summarization-model.md` (new, supersedes ADR-013).
  `CLAUDE.md` rewritten from its stale "no source code yet" placeholder — now documents `make up`
  as the canonical entrypoint for both the operator and future Claude Code sessions.

- **Single-source-of-truth cleanup for config defaults** (same session, operator request: "I want
  this model name to come from the env as well. single source of truth. scan for other places
  where this kind of improvement can be made") — found and removed every place a config default
  (`LMSTUDIO_MODEL`, `LMSTUDIO_IDENTIFIER`, `POSTGRES_DB`/`USER`/`PORT`, `LMSTUDIO_PORT`) was
  hardcoded a second time outside `.env.example`: the `Makefile`'s `?=` fallbacks, `docker-compose.yml`'s
  `${VAR:-default}` interpolations (including inside the Postgres healthcheck's `pg_isready`
  command), and `Program.cs`'s C# `?? "gitcrawler"`/`?? "5432"` bridge fallbacks. `.env.example` is
  now the only place any of these literals are defined; every consumer either reads `${VAR}`
  directly or fails loudly (`docker-compose.yml`'s `:?...`, the Makefile's new `check-env` target)
  if it's missing — the same pattern `POSTGRES_PASSWORD`/`GITHUB_TOKEN` already used, now applied
  consistently. `appsettings.json`'s own baseline defaults (`LmStudio:BaseUrl`/`Model`) were left
  alone deliberately — that's the .NET config system's own last-resort layer (for running without
  `.env` or Docker env vars at all, e.g. via an IDE), a different and legitimate mechanism, not the
  same copy-pasted-literal problem. Live-verified: both `check-env` failure modes (missing `.env`
  entirely, and a single missing variable), then a full `make up`/`make down` cycle re-confirming
  the happy path still works.

- **`make up` fixed to work from any Windows shell** (same session, operator report: ran `make up`
  from a real PowerShell window and hit `'test' is not recognized as an internal or external
  command`) — root cause: GNU Make on Windows only finds `sh.exe` on `PATH` from a Git Bash session
  (which augments `PATH` on launch), not from a plain PowerShell/cmd.exe window, so it silently
  fell back to `cmd.exe`, which can't parse the recipes' Unix syntax. Fixed by unconditionally
  forcing `SHELL` to Git for Windows' bundled `bash.exe` on Windows. Reproduced the operator's exact
  failure live (a `cmd.exe` subprocess with a minimal `PATH` matching their actual persistent
  Windows `PATH`) before and after the fix, then re-ran the full `make up`/`make down` happy path
  to confirm no regression. `CLAUDE.md`, `docs/setup.md`, and the Makefile's own header comment —
  which previously all stated "plain cmd.exe/PowerShell will not run it directly" — updated to
  match. Also found (unclear exact cause, likely from this session's own `check-env` failure-mode
  testing) and restored `.env`'s `POSTGRES_PASSWORD`, which had been blanked.

All of it is uncommitted in the working tree as of this handoff — the Orchestrator does not run
git commands; see **Commit Messages** below for what to run.

## Current state

The platform now has a working, self-hosted deployable skeleton:

| Layer | State |
|---|---|
| Backend | `.NET 10` solution at `src/backend/` builds clean; Wolverine wired with one vertical-slice example (`Features/Diagnostics/Ping/`); EF Core/Hangfire/Npgsql/Octokit.GraphQL referenced but not yet wired into runtime DI (deliberately, pending F-004's schema) |
| Config | `.env` now drives every config value in both `docker-compose.yml` and bare `dotnet run` — `Program.cs` loads it via `DotNetEnv` and bridges `GITHUB_TOKEN`, `LMSTUDIO_PORT`, `LMSTUDIO_IDENTIFIER`, and `POSTGRES_DB`/`POSTGRES_USER`/`POSTGRES_PASSWORD`/`POSTGRES_PORT` to their config keys (`GitHub:Token`, `LmStudio:BaseUrl`, `LmStudio:Model`, `ConnectionStrings:Postgres`); `docker-compose.yml` sources the same `.env` vars directly via `Section__Key` env vars, including a newly published Postgres host port (`POSTGRES_PORT`). All of this is live-verified both run modes this session: bare `dotnet run` (temporary debug output confirmed `LmStudio:BaseUrl`/`LmStudio:Model` matched `.env` directly, and the built `ConnectionStrings:Postgres` matched by length/suffix), and the full `make up` Docker stack (`docker exec ... printenv` confirmed `LmStudio__BaseUrl`/`LmStudio__Model`/`ConnectionStrings__Postgres` all resolved correctly, `/health`+`/api/ping`+LM Studio `/v1/models` all responded, and `localhost:5432` was reachable from the host via the new port publish). `ConnectionStrings:Postgres` is wired through but not yet consumed — no DbContext exists until F-004 |
| Frontend | Angular 22.1.0 workspace at `src/frontend/` builds clean; Angular Material themed; one smoke spec |
| Static asset integration | Angular production build copies into the backend host's `wwwroot/` via MSBuild targets on `Publish`; confirmed live |
| Docker Compose | `app` + `postgres:18.4` (pinned) only, both health-checked; entrypoint is `make up`, not `docker compose up` directly |
| LM Studio | Host-installed (ADR-016), not containerized; `Makefile` (and now `.env`, which it sources automatically) checks/starts it via the `lms` CLI and loads `llama-3.2-3b-instruct` (ADR-017, confirmed present on the target machine) — live-verified end-to-end this session, including a full model comparison and swap away from the original Gemma 4 E4B pin |
| Test harness | xUnit (backend) and Vitest (frontend) both wired and passing — smoke-level only, no feature logic to test substantively yet |
| Security | Backend: 0 vulnerable packages. Frontend: 6 moderate, dev-only, flagged not fixed (see PM tracking below) |

Nothing beyond scaffolding exists yet — no schema, no crawler, no scoring, no summarizer, no API
endpoints beyond the `/api/ping` diagnostic slice, no dashboard views.

## What's next

1. Invoke `orchestrator-development-pattern` again for **Phase 1** — Core data pipeline: ingest
   and score repositories.
2. Phase 1 features, in dependency order per `docs/project-management.md`'s Dependencies table:
   - **F-004** — Data Store schema (EF Core), depends on F-003 (now Done).
   - **F-005** — GitHub Crawler, depends on F-001 + F-004. Should reflect F-001's actual finding:
     the REST contributor-count fallback is the binding rate-limit constraint at scale, not the
     GraphQL query — design the back-off/retry logic (F-001 §6) around that, not just the GraphQL
     path.
   - **F-006** — Job Scheduler (Hangfire), depends on F-004.
   - **F-007** — Scoring Engine, depends on F-004 + F-005.
3. **F-008 (Summarizer) should target `llama-3.2-3b-instruct` per ADR-017**, not the original
   ADR-013 Gemma 4 E4B pin — PM-005 is closed, but F-008 shouldn't assume unlimited `max_tokens`
   headroom for the new model either (165/300 tokens used in testing — comfortable, not
   unlimited). No known truncation risk, just worth a sanity check once real summaries are being
   generated at scale.

## Important context

- **F-004 will need to wire EF Core's `DbContext` and Hangfire's `AddHangfire`/`AddHangfireServer`
  into `Program.cs`** — F-003 deliberately left both unwired since doing so before the schema
  exists would make local verification of F-003 silently depend on Postgres already being up.
  This isn't a gap to "fix" retroactively in F-003; it's F-004's actual job.
- **The frontend npm audit finding is a known, accepted gap, not an oversight** — re-check when
  Angular tooling publishes a patched `@angular/cli` release that doesn't require downgrading
  below Angular 22 (ADR-012's pin). Dev-only; not present in the production bundle or the
  deployed container.
- **`Octokit.GraphQL` is a prerelease (`0.4.0-beta`)** — no stable release exists yet. Re-check
  NuGet periodically; F-005 (Crawler) is the first feature that will actually exercise it.
- **LM Studio is host-installed, not containerized (ADR-016)** — `make up` will fail loudly at
  the `check-lmstudio` step if LM Studio isn't installed or its `lms` CLI isn't enabled (LM
  Studio → Settings → Developer). This is a hard prerequisite documented in `docs/setup.md`, not
  something the stack can silently work around.
- **Version-pin caveats carried from the original triage handoff — both now closed**: ADR-012
  (Angular 22) resolved correctly at scaffolding (confirmed: `@angular/cli` 22.1.2). ADR-013's
  original Gemma 4 E4B pin was live-verified (identifier + throughput both confirmed, 2026-08-01)
  but then **superseded by ADR-017** after the same live testing found a reasoning-token
  truncation problem — the platform now targets `llama-3.2-3b-instruct` instead. See
  `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9-§10 for the full trail.
- **`gemma-4-e4b` and other tested alternatives remain downloaded in LM Studio, just unused** —
  no cleanup was needed or performed; only the platform's default config (`Makefile`, `.env`)
  changed to point at `llama-3.2-3b-instruct`.
- **Open items not blocking Phase 1**: PM-001 (numeric success-metric targets) and PM-002
  (personalized discovery phasing) remain deferred to post-launch/post-v1. PM-003 (Docker Compose
  scaling ceiling) still has no defined trigger.
- **Docs are governed, not exempt** — this session's Integration and Reviewer-Integration passes
  both treated `docs/project-management.md` and `docs/architecture.md` as specs the code must
  satisfy, not side artifacts. Keep doing that in Phase 1: architecture-doc/PMBook drift is a
  FAIL-worthy Integration finding, not an FYI.

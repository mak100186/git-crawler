# Handoff: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-02

## What was done

Phase 1 (Core data pipeline) is complete, orchestrated end-to-end via `orchestrator-development-pattern`. Phase 0 (scaffolding, spikes) closed 2026-08-01 — see `docs/changelog.md` Revision 1 for that detail; this handoff focuses on Phase 1's operative state.

- **F-004** (Data Store schema, EF Core) — PASS on the first attempt. `GitCrawlerDbContext` with
  five entities (`Repository`, `Score`, `Summary`, `TrendAggregate`, `Bookmark`) under
  `src/backend/GitCrawler.Api/Data/`, three migrations to date (`InitialCreate`,
  `AddCrawlerRawSignalFields`, `AddScoreStarCountSignal`). Hangfire's own job-storage tables are
  created separately by `UsePostgreSqlStorage` at F-006 runtime (its own `hangfire` schema, not
  EF-migrated) — documented on the DbContext so F-006 didn't duplicate schema setup. Reviewer
  independently re-ran the full test suite and inspected the generated migration SQL by hand.
- **F-005** (GitHub Crawler) — PASS on the first attempt. `Features/Crawling/DiscoverRepositories/`
  — GraphQL-first discovery via `Octokit.GraphQL` with a typed-`HttpClient` REST fallback for
  contributor count; idempotent upsert by `Repository.GitHubId`. Genuinely implements (not just
  documents) the F-001 spike's §6 back-off strategy — GraphQL `RATE_LIMITED`/`resetAt`, REST
  `x-ratelimit-*`/`Retry-After`, generic exponential backoff (60s doubling, capped 30 min)
  otherwise — and §7 mitigation (7-day contributor-count freshness cache), since REST's
  contributor-count fallback, not the GraphQL query, is the binding rate-limit constraint at scale
  per that spike's finding. Reviewer independently reflected on the installed `Octokit.GraphQL`
  0.4.0-beta assembly to verify the `RATE_LIMITED` string-match heuristic's stated limitation was
  real, not assumed.
- **F-006** (Job Scheduler, Hangfire) — PASS on the first attempt. `AddHangfire`/
  `UsePostgreSqlStorage`/`AddHangfireServer` wired into `Program.cs` — the wiring F-003/F-004
  deliberately deferred. Dashboard at `/hangfire` is unauthenticated (updated 2026-08-02, same
  day, per operator request — the original fail-closed shared-secret query-key filter was removed
  after it also blocked the dashboard's own CSS/JS assets and stats-polling XHR, none of which
  carry the `?key=` query string forward). Removing the filter alone still left the dashboard
  401ing, since Hangfire's own default (`DashboardOptions.Authorization` unset) falls back to a
  `LocalRequestsOnlyAuthorizationFilter`, and Docker Desktop's proxy doesn't preserve `127.0.0.1`
  for a host-browser request through it — fixed by passing `Authorization = []` explicitly.
  Live-verified against the real `make up` stack (`curl` 401 → 200). See ADR-009 Consequences for
  both write-ups. One recurring job (`discover-repositories`, daily by default via
  `Hangfire:CrawlerCronSchedule`) triggers the Crawler; the `ContinueJobWith` attachment point for
  the next stage was left as a documented code comment (not a stub), per the scope note that only
  one real pipeline stage existed yet.
- **F-007** (Scoring Engine) — PASS after a mid-flight scope amendment (see below), following a
  clean PASS on the original four-signal scope. `Features/Scoring/ComputeScores/` — pure
  computation (Architecture §3 requires zero external calls here), reads `Repository`'s raw
  crawled fields and writes weighted `Score` rows. Also completes the pipeline chain F-006 left
  open: `DiscoverRepositoriesJob` now attaches `ComputeScoresJob` via Hangfire `ContinueJobWith`
  after each crawl (via an `IScoringContinuationLink` seam so the wiring is unit-testable without a
  live Hangfire server).
- **Operator-directed amendment mid-session**: "star count should also be part of the scoring for
  a repository." Routed back through a Developer amendment + full re-review rather than patched
  silently, since it changed F-007's already-reviewed scope. Legitimate extension, not scope creep
  — Architecture §3's Scoring Engine description already named "existing popularity/quality
  signals (per PRD Constraints)" as a fifth category beyond the PRD's committed four. Final
  weights: license 18%, commits-per-week 27%, contributor count 22.5%, fork count 22.5%, star
  count 10% (rebalanced from the original 20/30/25/25 by a uniform ×0.9 scale factor) — star count
  deliberately weighted below the smallest primary signal so it can move a score without ever
  dominating the four signals the PRD explicitly commits to. A new additive migration
  (`AddScoreStarCountSignal`) added the column; `InitialCreate` and `AddCrawlerRawSignalFields`
  were untouched.
- **Operator-directed infra change mid-session**: "the docker container for postgres should have a
  local folder mounted so that the db is persisted even across sessions." PostgreSQL's Compose
  volume changed from a named Docker volume (`postgres-data:`) to a bind mount at `./data/postgres`
  — the database now persists as visible, backup-able host files, not just Docker-managed opaque
  volume storage. `.gitignore` updated (`data/postgres/*`, keeping `.gitkeep`);
  `docs/test-runbook.md`'s clean-rebuild step corrected to note `docker compose down -v` no longer
  clears a bind mount's contents (`rm -rf data/postgres/*` needed for a truly fresh DB). Handled
  directly rather than through the full feature Task Packet apparatus, consistent with how Phase
  0's own ad hoc operator-directed infra fixes (Makefile SHELL, config single-source-of-truth) were
  handled.
- **Test-cases doc gap found and closed before Integration**: `docs/test-cases.md` only covered
  Phase 0 (v1) when Phase 1 features completed — flagged per the orchestrator's own Step 0.0 rule
  rather than silently proceeding. TC-004 through TC-007 drafted (v2) covering all four Phase 1
  features, including the five-signal independence check the star-count amendment required.
- **Integration** — PASS after one round of genuine fixes (not gamed): (1) an EF Core query
  (`OrderByDescending(...).FirstOrDefault()` inside a `Score`-selection projection) that SQLite (the
  test provider) can't translate over `DateTimeOffset` but Npgsql/Postgres has no such restriction
  against — rewritten to resolve client-side via `.Include()` + `.Max()`, same business logic,
  verified behaviorally equivalent; (2) a Hangfire test-helper defect (`JobCancellationToken.Null`
  is a genuine null reference in Hangfire.Core 1.8.24, not a null-object instance, tripping
  `PerformContext`'s null guard) — fixed with a real no-op `IJobCancellationToken`, confirmed the
  production code under test never reads that state. Final: 43/43 tests passing, 0 build warnings,
  0 vulnerable packages. Docker was unavailable in the Integration Agent's environment, so three
  live-infrastructure checks (fresh-Postgres migration, Hangfire dashboard reachability, mid-run
  restart persistence) were explicitly flagged as unresolved rather than silently skipped — see
  What's Next.
- **Reviewer-Integration** — PASS. Independently re-ran all quality gates (exact match: 43/0/0/0),
  reproduced the Hangfire `JobCancellationToken.Null` claim in an isolated console project, and
  confirmed the diagram-drift finding (below) was genuinely dual-recorded rather than dropped
  between being found and reported.
- **Documentation drift found**: `docs/diagrams/mmd/daily-discovery-flow.mmd` depicts the Scheduler
  triggering Scoring independently/in parallel with the Crawler — not what F-006/F-007 actually
  built (Crawler is the only `RecurringJob`; Scoring is chained via `ContinueJobWith`, not
  independently scheduled). Regenerating the diagram is outside any current agent's scope; flagged
  for a manual diagramming pass, noted in `docs/test-runbook.md`'s Known Caveats.
- **Graphify** — ran over `src/` only (`--update`, incremental), code-only fast path (all 32
  changed/new files were `.cs`/`.json`, no LLM semantic extraction needed). Graph grew from
  168 nodes/157 edges/23 communities (Phase 0 baseline) to **518 nodes/674 edges/48 communities**.
  Outputs refreshed in `graphify-out/` (`graph.html`, `graph.json`, `GRAPH_REPORT.md`). One stray
  duplicate cache directory (`src/backend/graphify-out/`, produced by AST extraction running with a
  different cwd than the prior Phase 0 run) was found and excluded going forward — `.gitignore`'s
  graphify-cache rule broadened from two hardcoded stray paths to a general `**/graphify-out/` /
  `!/graphify-out/` pattern so any future stray location is caught automatically.
- New docs this session: none (`docs/test-cases.md` v2 and `docs/test-runbook.md` extended
  in-place rather than as new files). `docs/changelog.md` Revision 2. `docs/project-management.md`
  v15+ (F-004 through F-007 → Done, F-007's row amended for star count, all inline).

All of it is uncommitted in the working tree as of this handoff — the Orchestrator does not run
git commands; see **Commit Messages** below (this response's final section) for what to run.

## Current state

The platform now has a working, self-hosted, end-to-end crawl-to-score pipeline:

| Layer | State |
|---|---|
| Data Store | PostgreSQL 18.4 via EF Core; 5 entities, 3 migrations; consumed at runtime (`Database.Migrate()` on startup) — no longer just wired-through-unused as at Phase 0 close |
| Crawler | GitHub GraphQL-first discovery + REST contributor-count fallback, idempotent upsert, rate-limit-aware retry, 7-day contributor-count caching — implemented as a Wolverine command/handler slice |
| Job Scheduler | Hangfire wired (`AddHangfire`/`UsePostgreSqlStorage`/`AddHangfireServer`), dashboard at `/hangfire` unauthenticated (operator decision, 2026-08-02), one daily recurring job (Crawler) chaining into Scoring via `ContinueJobWith` |
| Scoring Engine | Pure computation (no external calls), five independently-weighted signals (license, commits-per-week, contributor count, fork count, star count), scoring history preserved (multiple `Score` rows per repo over time) |
| Postgres persistence | Bind-mounted to `./data/postgres` (operator request) — survives `docker compose down`, backup-able as plain host files |
| Test harness | 43 xUnit tests (up from 1 smoke test at Phase 0 close), all passing; `dotnet list package --vulnerable` clean |
| Docs | `docs/test-cases.md` (v2) and `docs/test-runbook.md` cover Phase 0 + Phase 1; `docs/project-management.md` F-004–F-007 → Done |

Still nothing exists yet for: AI summarization (F-008), trend aggregation (F-009), the Web API
(F-010), or the dashboard beyond its Phase 0 shell (F-011). No frontend work happened this phase —
Phase 1 was backend-only per the PMBook's Dependencies table.

## What's next

1. Invoke `orchestrator-development-pattern` again for **Phase 2** — AI summarization and trend
   detection; the dashboard UX design brief (F-018) can run in parallel since it only needs the
   approved Functional Requirements, not a running API.
2. Phase 2 features, in dependency order per `docs/project-management.md`'s Dependencies table:
   - **F-008** — Summarizer, depends on F-002 (Done) + F-004 (Done) + F-007 (Done). Targets
     `llama-3.2-3b-instruct` per ADR-017, not the original ADR-013 Gemma 4 E4B pin. No known
     truncation risk at `max_tokens: 300` (165/300 tokens used in F-002's testing), but not
     unlimited headroom either — worth a sanity check once real summaries are generated at scale,
     per Phase 0's handoff carry-over note.
   - **F-009** — Trend Aggregator, depends on F-007 (Done) + F-008.
   - **F-018** — Dashboard UX design brief & Claude Designer handoff, no code dependency, can start
     immediately.
3. **Before relying on Phase 1 in anything resembling production**, close the residual
   live-infrastructure verification gap this session's Integration Agent flagged (Docker was
   unavailable in its environment): run this runbook's F-004 Happy Path (fresh-Postgres migration),
   F-006 Happy Path (dashboard reachability), and F-006 Regression-sensitive (mid-run restart)
   steps against a real `make up` stack at least once. Automated test coverage exists for the
   underlying logic in all three cases — what's unverified is the live infrastructure integration
   itself, not the business logic.
4. **`docs/diagrams/mmd/daily-discovery-flow.mmd` needs a manual diagramming pass** — it currently
   shows the Scheduler triggering Scoring independently of the Crawler, which doesn't match the
   actual `ContinueJobWith`-chained design. Not blocking Phase 2, but should be fixed before it
   misleads someone reading Architecture alongside the diagram.

## Important context

- **F-007's star-count amendment happened after its own Reviewer had already passed the original
  four-signal scope** — this is documented in `docs/project-management.md`'s F-007 row and in the
  changelog as a legitimate scope extension (Architecture §3 already gestured at "existing
  popularity/quality signals" as a category), not a defect fix. If a future session touches
  `ScoringWeights.cs`, the weight-rebalancing rationale (uniform ×0.9 scale on the original four to
  make room for star count at 10%) is documented in that file's header comment — don't re-derive
  it from scratch, and don't casually adjust weights without checking that comment first.
  Discovery/scoring weighting and thresholds throughout Phase 1 (crawl lookback days, minimum
  stars, scoring caps) were explicitly left as documented judgment calls, not specs handed down by
  the PRD — revisit them with real usage data once the platform has run for a while, per PM-001's
  broader "no baseline yet" framing.
- **The Postgres bind-mount change was operator-directed mid-session, applied directly rather than
  through the full Developer/Reviewer Task Packet cycle** — this matches how Phase 0 handled
  similarly small, well-scoped ad hoc infra requests (the Makefile `SHELL` fix, the config
  single-source-of-truth pass). If this pattern recurs, keep applying that same judgment: small,
  well-understood infra/config tweaks outside the numbered feature backlog don't need the full
  orchestration ceremony, but should still be flagged explicitly in the handoff and changelog, as
  done here.
- **Docker was unavailable in this session's Integration Agent environment** — this is a
  significant, explicitly-flagged verification gap, not a silent skip. See What's Next item 3.
  Don't assume Phase 1's live infrastructure paths (a real migration against fresh Postgres, the
  Hangfire dashboard rendering in a browser, restart-survival under Hangfire's Postgres-backed
  storage) are proven just because the automated test suite is green — the automated suite
  deliberately substitutes fakes/SQLite for exactly the parts that need a live stack to fully
  verify.
- **`Octokit.GraphQL` is still a prerelease (`0.4.0-beta`)** — now actually exercised by F-005 (no
  longer just referenced-but-unused as at Phase 0 close). Its `GraphQLException` type was confirmed
  (via reflection against the installed assembly, both by the F-005 Developer and independently by
  its Reviewer) to expose no structured `errors[]`/`Type` property — the `RATE_LIMITED` detection
  in `GitHubDiscoveryClient.cs` relies on a string match against the exception message as a result.
  If a future `Octokit.GraphQL` version changes that message text, this heuristic silently stops
  matching and falls through to generic exponential backoff instead of the more precise
  `resetAt`-based wait — not a crash, just a less-precise back-off. Re-check this if/when
  `Octokit.GraphQL` reaches a stable release.
- **The frontend `npm audit` finding from Phase 0 remains open, unchanged** — still blocked on an
  `@angular/cli` downgrade that would violate ADR-012's Angular 22 pin; re-check when a patched
  release exists that doesn't require the downgrade.
- **Version-pin caveats carried from earlier phases, still current**: LM Studio host-installed
  (ADR-016), `llama-3.2-3b-instruct` (ADR-017, supersedes ADR-013), PostgreSQL 18.4 (ADR-014),
  Angular 22 (ADR-012) — none of these changed this phase.
- **Open items not touched this phase**: PM-001 (numeric success-metric targets), PM-002
  (personalized discovery phasing), PM-003 (Docker Compose scaling ceiling trigger) all remain
  deferred exactly as they were at Phase 0 close.
- **Docs are governed, not exempt** — this session's Integration and Reviewer-Integration passes
  both treated `docs/project-management.md`, `docs/architecture.md`, `docs/test-cases.md`, and
  `docs/test-runbook.md` as specs the code must satisfy, catching and fixing one real drift
  (`test-runbook.md`'s stale "only test" claim) and correctly flagging one it couldn't fix itself
  (the daily-discovery-flow diagram). Keep doing this in Phase 2.

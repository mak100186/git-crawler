# Handoff: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-02

## What was done

Phase 2 (AI summarization and trend detection; dashboard UX design brief) is complete, orchestrated end-to-end via `orchestrator-development-pattern`. Phase 1 (core data pipeline) closed 2026-08-02 — see `docs/changelog.md` Revisions 1-4 for that detail; this handoff focuses on Phase 2's operative state.

- **F-008** (Summarizer) — PASS after one retry round. `Features/Summarization/GenerateSummaries/`
  — `GenerateSummariesCommandHandler` selects repos by latest (`ComputedAtUtc`-ordered, not
  highest-ever) `Score.TotalScore ≥ Summarization:MinimumScore` (default 40) without an existing
  `Summary` row, capped at `Summarization:BatchSize` (default 20); README fetched via GitHub REST
  (`GET /repos/{owner}/{repo}/readme`, reusing the Crawler's REST client, 404 handled gracefully);
  `IRepositorySummarizer`/`LmStudioRepositorySummarizer` call LM Studio's OpenAI-compatible
  `/v1/chat/completions` endpoint at `max_tokens: 300` (per ADR-017, no truncation risk at this
  model). Per-repo failures (README or LM Studio) are logged and skipped, not batch-aborting — no
  Polly pipeline, unlike ADR-018's Crawler pipeline, since LM Studio's local API has no rate-limit
  *signal* equivalent to retry against. `ComputeScoresJob` now attaches `GenerateSummariesJob` as
  chain link 3 via a new `ISummarizationContinuationLink` seam. **First-round Reviewer FAIL**: the
  original selection logic used `Scores.Max(s => s.TotalScore)` (highest-ever value) instead of the
  chronologically latest score, which could permanently summarize a repo off a historical peak it
  has since fallen below (Summaries are create-once, never regenerated). Fixed to
  `OrderByDescending(ComputedAtUtc).First().TotalScore`, matching `ComputeScoresCommandHandler`'s
  own established convention; a regression test now distinguishes the two semantics explicitly.
- **F-009** (Trend Aggregator) — PASS on the first attempt. `Features/Trends/AggregateTrends/` —
  rolls up repos with both a `Score` and a `Summary` into per-category (`Repository.PrimaryLanguage`,
  null excluded) trend rows, using each repo's latest `TotalScore`. Single-day period by default
  (`Trends:PeriodDays`, default 1). Persistence is **upsert-by-`(Category, PeriodStart, PeriodEnd)`**
  — a third distinct persistence pattern in this codebase, alongside `Score`'s append-history and
  `Summary`'s create-once, needed for NFR-003 idempotency (re-running the same period must not
  duplicate rows). `GenerateSummariesJob` now attaches `AggregateTrendsJob` as chain link 4 via a new
  `ITrendsContinuationLink` seam, completing the pipeline: **Crawler → Scoring → Summarizer → Trend
  Aggregator**, all four links chained via Hangfire `RecurringJob` + `ContinueJobWith`.
- **F-018** (Dashboard UX design brief) — PASS on the first attempt, no code. `docs/design-briefs/dashboard-ux-brief.md`
  specifies the Discovery Feed, Hidden Gems, Trending, and Categories layouts, FR-004 filter/sort and
  FR-007 bookmark interactions (the "list bookmarked" verb resolved as a filter toggle within the
  four required views, not a fifth view — that's F-012's scope), and an explicit Angular-Material-only
  constraint (ADR-011) with three genuine component gaps (infinite scroll, trend sparkline, skeleton
  loader) flagged with Material-native fallbacks rather than silently spec'd as custom widgets. The
  "handoff to Claude Designer" is a document handoff, not a tool invocation — the actual design pass
  and its review/approval against F-018's four acceptance criteria are a follow-up step outside this
  feature's own scope, still gating F-011.
- **Integration** — PASS on the first attempt (no fixes needed — format/build/64 tests/audit were
  already clean). Live E2E of the real F-008→F-009 chain was **not** executed: LM Studio's local
  server could not be started in the Integration Agent's environment (`lms server start` timed out).
  Recorded as TC-008-08 (Manual) in `docs/test-cases.md` and as a runbook caveat — see What's Next.
- **Reviewer-Integration** — PASS. Independently re-ran all quality gates (exact match: 64/0/0/0
  tests, 0 vulnerabilities) and confirmed no documentation-drift finding was dropped between being
  found and reported.
- **Documentation drift found and fixed this phase**: `docs/project-management.md`'s Phase 2 row was
  still `Planned` despite F-008/F-009/F-018 all being `Done` — corrected (v17). `docs/test-cases.md`
  extended to v4 with TC-008 (7 scenarios + 1 Manual), TC-009 (7 scenarios), TC-018 (3 scenarios).
  `docs/test-runbook.md` extended with F-008/F-009 sections (F-018 deliberately given none — a
  brief-vs-4-ACs review has no meaningful runbook steps beyond what TC-018 already specifies).
- **Documentation drift found, NOT fixed (carried over, out of scope for any current agent)**:
  `docs/diagrams/mmd/daily-discovery-flow.mmd` was already stale before this phase (showed Scoring as
  independently scheduled rather than `ContinueJobWith`-chained) and is now more stale — the real
  chain is a 4-link Crawler→Scoring→Summarizer→Trend Aggregator chain the diagram doesn't show at
  all. Also noticed (pre-existing, unrelated to this phase): `docs/architecture.md`'s Version History
  table has a duplicate/out-of-order `v12` row (the Polly/ADR-018 entry vs. the A2-risk-resolved
  entry) — fixing the numbering needs to know original intent, not guessed at here.
- **Graphify** — ran over `src/` (`--update`, incremental), code-only fast path (all 24 changed files
  were `.cs`/`.json`, no LLM semantic extraction needed). Graph grew from 518 nodes/674 edges/48
  communities (Phase 1) to **860 nodes/1223 edges/55 communities**. The incremental `--update` prune
  step initially missed 28 ghost nodes from three files genuinely deleted earlier this session
  (`RetryDelay.cs`, `HangfireDashboardAuthorizationFilter.cs`, its test) — the prune comparison used
  absolute Windows paths from the file-change detector against the graph's stored relative
  `source_file` paths, so nothing matched. Caught by spot-checking the report's "Surprising
  Connections" section (it still referenced `IRetryDelay`/`FakeRetryDelay`, both from a deleted
  file), fixed with a suffix-based path match, re-clustered, and re-verified zero stale hits remain.
  Community labels for all 55 communities generated from each community's dominant
  `Features/<Area>/<Operation>/` source folder (a fast heuristic given the scale — 55 communities —
  rather than hand-authoring each one) — reasonable for a small, cleanly-vertical-sliced codebase
  where folder structure already tracks feature boundaries closely. Outputs refreshed in
  `graphify-out/` (`graph.html`, `graph.json`, `GRAPH_REPORT.md`).
- New docs this phase: `docs/design-briefs/dashboard-ux-brief.md` (new, F-018),
  `docs/adr/ADR-018-polly-resilience-for-github-crawler.md` (new, carried in from the Polly/Hangfire
  fix committed at the start of this session, not a Phase 2 feature itself).
  `docs/project-management.md` v17, `docs/test-cases.md` v4, `docs/test-runbook.md` extended,
  `docs/changelog.md` Revision 5, `docs/handoff.md` (this file).

All of it is uncommitted in the working tree as of this handoff — the Orchestrator does not run
git commands; see **Commit Messages** in this session's final response for what to run.

---

## Phase 1 handoff (superseded by the above, kept for history)

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

All of it was uncommitted at the time this Phase 1 handoff was originally written — see
`docs/changelog.md` for the actual commit history since.

---

## Current state

The platform now has a working, self-hosted, end-to-end **crawl → score → summarize → aggregate
trends** pipeline — all four stages chained via Hangfire `RecurringJob` + `ContinueJobWith`:

| Layer | State |
|---|---|
| Data Store | PostgreSQL 18.4 via EF Core; 5 entities, 3 migrations; unchanged this phase |
| Crawler | GitHub GraphQL-first discovery + REST contributor-count fallback, Polly resilience pipeline (ADR-018); unchanged this phase (fixed earlier this session, see Revision 4) |
| Job Scheduler | Hangfire wired, dashboard at `/hangfire` unauthenticated; now chains **four** links: Crawler → Scoring → Summarizer → Trend Aggregator |
| Scoring Engine | Pure computation, five weighted signals; unchanged this phase |
| Summarizer | **New (F-008)**: LM Studio + Llama 3.2 3B Instruct via `IRepositorySummarizer`; selects top-scored (`Summarization:MinimumScore`, default 40), un-summarized repos (`Summarization:BatchSize`, default 20 per run); README fetched via GitHub REST; Summary rows create-once, never regenerated |
| Trend Aggregator | **New (F-009)**: rolls up scored+summarized repos by `PrimaryLanguage` into `TrendAggregate` rows; single-day period by default (`Trends:PeriodDays`); upsert-by-`(Category, PeriodStart, PeriodEnd)` for idempotency |
| Dashboard UX brief | **New (F-018)**: `docs/design-briefs/dashboard-ux-brief.md` — Discovery Feed/Hidden Gems/Trending/Categories layouts, filter/sort/bookmark interactions, Angular Material-only constraint; not yet handed to an actual design pass |
| Postgres persistence | Bind-mounted to `./data/postgres`; unchanged this phase |
| Test harness | 64 xUnit tests (up from 43 at Phase 1 close), all passing; `dotnet list package --vulnerable` clean |
| Docs | `docs/test-cases.md` (v4) and `docs/test-runbook.md` cover Phase 0-2; `docs/project-management.md` v17, F-008/F-009/F-018 → Done, Phase 2 → Done |

Still nothing exists yet for: the Web API (F-010), the dashboard beyond its Phase 0 shell (F-011),
or bookmarking (F-012). No frontend work happened this phase — Phase 2, like Phase 1, was
backend-only (plus one docs-only feature) per the PMBook's Dependencies table.

A live database check before Phase 2 started (operator request) found the Crawler's discovery
query is functioning but only ever surfaces very-high-star repos (18.7K-453K stars across all 1,002
discovered rows) — GitHub's GraphQL search has no explicit sort parameter and defaults to
"best-match" relevance, which correlates heavily with popularity, combined with the ~1,000-result
visibility cap. The operator reviewed this and explicitly decided **not** to treat it as a bug for
now ("leave as-is") — noted here so a future session doesn't have to re-discover it from scratch.
If discovery strategy is revisited later (e.g. star-range bracketing, REST search with explicit
sort, or random sampling), that's a change to `GitHubDiscoveryClient.BuildSearchQuery()` (F-005),
not anything Phase 2 touched.

## What's next

1. Invoke `orchestrator-development-pattern` again for **Phase 3** — Web API, Dashboard, and
   Bookmarking (F-010, F-011, F-012). F-011 (dashboard) depends on both F-010 (API) and F-018 (UX
   brief, Done) — but F-018's brief still needs an actual design pass and approval before F-011
   implementation begins per its own acceptance criteria; that review/approval step hasn't happened
   yet and isn't something an orchestrator run can do on its own (it requires a human or a
   dedicated design tool in the loop).
2. **Close the live-E2E verification gap this session's Integration Agent flagged**: LM Studio
   could not be started in its environment (`lms server start` timed out), so the real F-008→F-009
   chain (actual README fetch + actual LM Studio inference + actual trend rollup against live data)
   was never exercised end-to-end — only via SQLite-backed unit/handler tests. Run this runbook's
   F-008 and F-009 Happy Path steps against a real `make up` stack with LM Studio actually running
   at least once before relying on summaries/trends in anything resembling production.
3. **`docs/diagrams/mmd/daily-discovery-flow.mmd` still needs a manual diagramming pass** — flagged
   at Phase 1 close, still unaddressed, and now more stale: it doesn't show the Summarizer or Trend
   Aggregator links at all. Not blocking Phase 3, but should be fixed before it misleads someone
   reading Architecture alongside the diagram.
4. **`docs/architecture.md`'s Version History table has a duplicate/out-of-order `v12` row**
   (noticed this phase, pre-existing from earlier this session's Polly/ADR-018 work) — fixing the
   numbering needs to know original intent; flagged rather than guessed at.
5. Once real summaries/trends exist at scale, sanity-check `max_tokens: 300` isn't clipping longer
   real-world READMEs the way the F-002 spike's synthetic test content couldn't reveal (165/300
   tokens used in that spike — comfortable, not proven unlimited) — carried over from Phase 1's
   handoff, still open.

## Important context

- **The "hidden gems only surface mega-popular repos" finding (see Current State) was raised and
  explicitly accepted by the operator before Phase 2 started** — don't re-flag it as a fresh
  discovery in a future session without checking here first; it's a known, accepted-as-is state,
  not an open item.
- **F-008's max-vs-latest Score bug (see What Was Done) is exactly the kind of subtle correctness
  issue this codebase's Reviewer step exists to catch** — the codebase now has *two* documented
  instances of "latest by time, not highest-ever value" being the correct semantics for resolving a
  repo's current `Score` (`ComputeScoresCommandHandler`, and now `GenerateSummariesCommandHandler`
  and `AggregateTrendsCommandHandler`, both of which got it right from the start in Phase 2 having
  learned from F-008's first-round mistake). If a future feature also needs "this repo's current
  score," follow the same `OrderByDescending(ComputedAtUtc).First()` pattern, not `Max(TotalScore)`.
- **Three distinct persistence patterns now coexist in this codebase, by design**: `Score` = append
  history (one row per re-score, never updated), `Summary` = create-once (never regenerated once
  created), `TrendAggregate` = upsert-by-key (`Category`/`PeriodStart`/`PeriodEnd`, updated in place
  on re-run). Each is deliberate and documented at its own handler — don't assume one pattern
  generalizes to a new stage without checking that stage's own idempotency/history requirements.
- **The graphify `--update` incremental prune has a path-format gap** (see What Was Done) — its
  ghost-node-pruning comparison needs the deleted-file list and the graph's stored `source_file`
  values in the same path format (both relative, or both absolute+same-separator). This session hit
  it because the incremental file-change detector returns absolute Windows paths while the graph
  stores relative forward-slash paths. If a future `--update` run silently reports "no drift" after
  a file deletion, verify by grep'ing `graphify-out/graph.json` for the deleted filename directly
  rather than trusting the prune step's own "no ghost nodes" claim.
- **Version-pin caveats carried from earlier phases, still current**: LM Studio host-installed
  (ADR-016), `llama-3.2-3b-instruct` (ADR-017), PostgreSQL 18.4 (ADR-014), Angular 22 (ADR-012).
- **Open items not touched this phase**: PM-001, PM-002, PM-003 remain deferred exactly as before.
- **Docs are governed, not exempt** — this phase's Integration and Reviewer-Integration passes again
  treated the PMBook/Architecture/test-cases/test-runbook as specs the code must satisfy, catching
  and fixing the Phase 2 status drift and closing the test-cases/runbook gaps, while correctly
  flagging the two drift items (diagram, Architecture version-history) it couldn't fix itself. Keep
  doing this in Phase 3.

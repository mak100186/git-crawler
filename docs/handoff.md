# Handoff: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-02

## What was done

**F-010 (Web API) is Done**, run as a standalone, single-feature slice of Phase 3 via
`orchestrator-development-pattern` — the operator deliberately scoped this run to F-010 alone rather
than the full phase (F-010, F-011, F-012), so F-011 and F-012 remain `Planned` and Phase 3 itself
stays `Planned` until they complete too. Phase 2 (AI summarization/trend detection/UX brief) closed
2026-08-02 — see "Phase 2 handoff" below for that detail; this section covers F-010's operative state.

- **F-010** (Web API) — PASS on the first Developer/Reviewer attempt; Integration PASS (121/121
  tests, 0 vulnerabilities, ran twice for stability); Reviewer-Integration PASS after one
  self-corrected round (see below). `Features/{Repositories,Trends,Categories,Bookmarks}/` — 8 new
  Wolverine command/query slices (ADR-015): `GetDiscoveryFeed`, `GetHiddenGems`, `GetTrending`,
  `GetCategories`, `GetCategoryRepositories`, `CreateBookmark`, `DeleteBookmark`, `ListBookmarks`,
  each with its own endpoint dispatching via `IMessageBus.InvokeAsync` (matching the existing
  `PingEndpoint` pattern). A shared *internal* helper, `Features/Repositories/RepositoryCardQuery.cs`
  (plain class, not a Wolverine message), implements one filter/sort/paginate contract reused by
  Discovery Feed, Hidden Gems, and Category drill-down — avoiding tripling that logic while keeping
  each Wolverine slice boundary intact per ADR-015.
- **Two schema gaps closed as additive migrations** (same precedent as F-007's
  `AddScoreStarCountSignal`), both bundled into F-010 rather than deferred: `Repository.Topics`
  (`text[]` via EF Core's primitive-collections feature — GitHub topics were never crawled before
  this feature despite being in FR-004's filter scope; F-005's `GitHubDiscoveryClient` now fetches
  `repositoryTopics(first: 10)` alongside the existing discovery query) and
  `Repository.FirstDiscoveredAtUtc` (set once on first insert, never updated on re-crawl — this is
  what Discovery Feed's default "Newest" sort orders by; `LastCrawledAtUtc` would have been wrong
  since it advances on every re-crawl). **Neither column is backfilled for pre-existing rows**
  (`Topics` defaults to `{}`, `FirstDiscoveredAtUtc` defaults to `-infinity`) — tracked as new open
  item PM-006, see What's Next.
- **Two data-model decisions resolved before implementation, not re-derived by the Developer**:
  "Categories" stayed `TrendAggregate.Category`-derived (i.e. `Repository.PrimaryLanguage`), not
  GitHub-topic-derived, since F-009 already shipped that semantic and reopening it was out of scope;
  Trending's "contributing repos" for a trend are computed at query time (has both `Score` and
  `Summary`, latest `TotalScore`, matching `PrimaryLanguage`) rather than stored, mirroring
  `AggregateTrendsCommandHandler`'s own membership criteria exactly — `TrendAggregate` has no
  repo-level FK by design (see its own header comment).
- **Hidden Gems exposes FR-005's full weighted signal breakdown**, not just a total — each of the
  five signals (license/commits-per-week/contributor count/fork count/star count) alongside
  `ScoringWeights`' exact constants (18%/27%/22.5%/22.5%/10%) and `TotalScore`. Both Hidden Gems and
  Discovery Feed's `Score`/`Commits` sorts use each repo's *latest* `Score` by `ComputedAtUtc`, not
  `Max(TotalScore)` — the same class of correctness rule F-008 first got wrong and then fixed in
  Phase 2 (see Important Context below), applied correctly from the start here.
- **Bookmark create/delete are idempotent by design**: a double-create never throws the unique-index
  constraint violation (`Bookmark.RepositoryId`); a delete of a nonexistent bookmark never errors.
  Both documented in-code as deliberate choices, not oversights.
- **Reviewer** — PASS on the first pass. Independently re-ran the full test suite (121/121, matching
  the Developer's own report), cross-checked the score-breakdown weights against the literal
  `ScoringWeights.cs` constants, and verified the migration's generated SQL applies cleanly with
  correct defaults for both new NOT NULL columns against a non-empty table.
- **Integration** — PASS on the first attempt (no code fixes needed — format/build/tests/audit were
  already clean). Found and fixed one real documentation-drift gap: `docs/test-runbook.md` had no
  F-010 section despite 8 new user-facing endpoints — authored one, cross-referencing every scenario
  to its actual passing test. Flagged (not fixed, out of its scope) that no live `make up` stack was
  available to walk the new endpoints end-to-end over real HTTP — same category of gap Phase 1/2's
  own Integration passes disclosed for their live-infrastructure checks.
- **Reviewer-Integration — initially FAILed on a misattribution, then self-corrected to PASS**: it
  found `docs/test-cases.md` had also changed (+80 lines: a new `## F-010 — Web API` section,
  TC-010-01 through TC-010-10) and assumed the Integration Agent had silently authored and hidden
  that work, since Integration's own report only listed `docs/test-runbook.md` as touched. In fact
  the Orchestrator (not Integration) wrote the TC-010 section directly, *before* dispatching
  Integration, per this skill's own Step 0.0 ("Test Cases Doc — quality review... have the
  Orchestrator draft the missing scenarios") — the same gap-closure pattern already used for
  Phase 1/2 (see those sections below). When the Orchestrator flagged this, Reviewer-Integration
  independently re-read the skill's actual Step 0.0 and Documentation Drift Check text (rather than
  taking the correction at face value) and confirmed `test-cases-doc` was never in the Integration
  Agent's Documentation Drift scope in the first place — reversed its own verdict to PASS. Worth
  remembering for future sessions: when the Orchestrator pre-drafts test-cases-doc content itself,
  say so explicitly in the Integration Agent's prompt in a way that also reaches Reviewer-Integration,
  not just Integration — this ambiguity is cheap to prevent up front.
- **Documentation drift found and fixed this run**: `docs/test-cases.md` extended to v5 with TC-010
  (10 scenarios covering filter/sort/paginate, score breakdown, trend membership parity, categories,
  bookmark CRUD + idempotency, topic filtering, and the two schema-specific regressions). PMBook
  F-010 row updated `Planned` → `Done` with implementation annotations (v18); a new PM-006 open item
  added for the unbackfilled schema columns.
- **Graphify** — ran over `src/backend` only (`--update`, incremental), code-only fast path (all 34
  new/changed files were `.cs`, no LLM semantic extraction needed). Graph grew from 860 nodes/1223
  edges/55 communities (Phase 2) to **1154 nodes/1754 edges/78 communities**. The incremental diff
  initially reported 17 `src/frontend` files as "deleted" — verified false: they still exist on disk,
  the false-positive was purely a scope mismatch (an earlier run's saved manifest was built from a
  wider scan root than this backend-scoped run). Deletion pruning was deliberately skipped for those
  17 files to avoid destroying legitimate frontend graph nodes, and the manifest was re-saved scoped
  correctly to `src/backend` so a future backend-only `--update` won't repeat the false report.
  Community labels generated from each community's dominant `Features/<Area>/<Operation>/` source
  folder, same heuristic as Phase 1/2. Outputs refreshed in `graphify-out/` (`graph.html`,
  `graph.json`, `GRAPH_REPORT.md`).
- New/changed docs this run: `docs/project-management.md` v18, `docs/test-cases.md` v5,
  `docs/test-runbook.md` (new F-010 section), `docs/handoff.md` (this file). `docs/changelog.md` not
  yet bumped as of this edit — see Commit Messages in this session's final response.

All of it is uncommitted in the working tree as of this handoff — the Orchestrator does not run git
commands; see **Commit Messages** in this session's final response for what to run.

---

## Phase 2 handoff (superseded by the above, kept for history)

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
trends** pipeline, plus a JSON Web API surfacing it — all four pipeline stages still chained via
Hangfire `RecurringJob` + `ContinueJobWith`, with the Web API a separate, request-driven layer on top:

| Layer | State |
|---|---|
| Data Store | PostgreSQL 18.4 via EF Core; 5 entities, 4 migrations (added `AddRepositoryTopicsAndFirstDiscoveredAt` this run) |
| Crawler | GitHub GraphQL-first discovery + REST contributor-count fallback, Polly resilience pipeline (ADR-018); **extended this run** to also fetch `repositoryTopics` (capped 10/repo) and set `FirstDiscoveredAtUtc` once on first insert |
| Job Scheduler | Hangfire wired, dashboard at `/hangfire` unauthenticated; chains **four** links: Crawler → Scoring → Summarizer → Trend Aggregator; unchanged this run |
| Scoring Engine | Pure computation, five weighted signals; unchanged this run |
| Summarizer | LM Studio + Llama 3.2 3B Instruct via `IRepositorySummarizer`; unchanged this run |
| Trend Aggregator | Rolls up scored+summarized repos by `PrimaryLanguage` into `TrendAggregate` rows; unchanged this run |
| **Web API** | **New (F-010)**: `Features/{Repositories,Trends,Categories,Bookmarks}/` — 8 Wolverine slices serving Discovery Feed, Hidden Gems, Trending, Categories (+ drill-down), and bookmark create/list/delete; shared filter/sort/paginate contract (language/star-range/topic/license facets, 4 sort fields, pagination); no auth (single-operator v1 posture) |
| Dashboard UX brief | `docs/design-briefs/dashboard-ux-brief.md` — Discovery Feed/Hidden Gems/Trending/Categories layouts, filter/sort/bookmark interactions, Angular Material-only constraint; **the Claude Designer pass has since happened and been operator-reviewed, all three flagged gaps resolved** (PMBook F-018 row, v19) — F-011 is design-ready, unchanged by this run |
| Postgres persistence | Bind-mounted to `./data/postgres`; unchanged this run |
| Test harness | 121 xUnit tests (up from 64 at Phase 2 close), all passing; `dotnet list package --vulnerable` clean |
| Docs | `docs/test-cases.md` (v5) and `docs/test-runbook.md` cover Phase 0-2 + F-010; `docs/project-management.md` v18, F-010 → Done, Phase 3 still `Planned` (F-011/F-012 not started) |

Still nothing exists yet for: the dashboard beyond its Phase 0 shell (F-011) or bookmarking's
dedicated view (F-012) — F-010 unblocks both, but neither has been implemented. No frontend
implementation work happened this run — F-010 was backend-only, per its own Task Packet scope.

A live database check before Phase 2 started (operator request) found the Crawler's discovery
query is functioning but only ever surfaces very-high-star repos (18.7K-453K stars across all 1,002
discovered rows) — GitHub's GraphQL search has no explicit sort parameter and defaults to
"best-match" relevance, which correlates heavily with popularity, combined with the ~1,000-result
visibility cap. The operator reviewed this and explicitly decided **not** to treat it as a bug for
now ("leave as-is") — noted here so a future session doesn't have to re-discover it from scratch.
If discovery strategy is revisited later (e.g. star-range bracketing, REST search with explicit
sort, or random sampling), that's a change to `GitHubDiscoveryClient.BuildSearchQuery()` (F-005),
not anything F-010 touched.

## What's next

1. **F-011 (Web Dashboard) is next** — its two dependencies (F-010 API, F-018 UX brief) are both
   Done and the UX design has been reviewed/approved (PMBook F-018 row, v19 — the "Ink Header"
   direction with deep-olive second accent, `Dashboard Design.dc.html` + `dashboard-handoff.md`,
   judged implementation-ready). Invoke `orchestrator-development-pattern`
   scoped to F-011 the same way this run was scoped to F-010 alone, or resume the full Phase 3 loop
   to pick up F-011 then F-012 in dependency order — operator's call.
2. **PM-006 (new this run)**: F-010's two new `Repository` columns have no backfill for pre-existing
   rows — `Topics` self-heals on the next re-crawl (daily per F-006's schedule), but
   `FirstDiscoveredAtUtc` does not (it's set-once by design) and will permanently sort old repos as
   "oldest" under Discovery Feed's default Newest sort. Decide before F-011 ships a Newest sort a
   user will actually look at: a one-time backfill script, or accept as a permanent v1 wrinkle.
3. **Close the live-E2E verification gap for F-010's endpoints**: no `make up` stack was available in
   this run's environment, so the 8 new endpoints were validated via handler tests against a real
   SQLite-provider `DbContext` (including the `Topics` array-overlap query translation), not via a
   live HTTP walkthrough. Run the new F-010 section of `docs/test-runbook.md` against a real stack at
   least once before relying on it in production — same category of gap Phase 1/2 also disclosed for
   their own live-infrastructure checks.
4. **Close the live-E2E verification gap from Phase 2, still open**: LM Studio could not be started
   in that Integration Agent's environment, so the real F-008→F-009 chain (actual README fetch +
   actual LM Studio inference + actual trend rollup against live data) was never exercised
   end-to-end — only via SQLite-backed unit/handler tests. Run this runbook's F-008 and F-009 Happy
   Path steps against a real `make up` stack with LM Studio actually running at least once.
5. **`docs/diagrams/mmd/daily-discovery-flow.mmd` still needs a manual diagramming pass** — flagged
   at Phase 1 close, still unaddressed: it doesn't show the Summarizer or Trend Aggregator links, and
   now also doesn't show the Web API as a separate consumer of the Data Store. Not blocking F-011,
   but should be fixed before it misleads someone reading Architecture alongside the diagram.
6. **`docs/architecture.md`'s Version History table has a duplicate/out-of-order `v12` row**
   (noticed in Phase 2, still unaddressed) — fixing the numbering needs to know original intent;
   flagged rather than guessed at.
7. Once real summaries/trends exist at scale, sanity-check `max_tokens: 300` isn't clipping longer
   real-world READMEs the way the F-002 spike's synthetic test content couldn't reveal — carried over
   from Phase 1's handoff, still open.

## Important context

- **When the Orchestrator pre-drafts test-cases-doc scenarios itself (Step 0.0), say so explicitly
  in a way that reaches every downstream sub-agent, not just Integration** — this run's
  Reviewer-Integration initially FAILed the whole Integration Output because it saw
  `docs/test-cases.md` had changed and assumed Integration was hiding that work, when actually the
  Orchestrator wrote it before Integration even started (see What Was Done). It self-corrected by
  reading the skill's own text directly, but the ambiguity was avoidable — future sessions should
  make this ownership explicit to both Integration *and* Reviewer-Integration up front.
- **F-010 bundled two additive schema columns rather than deferring them** (`Repository.Topics`,
  `Repository.FirstDiscoveredAtUtc`) — same judgment-call pattern as F-007's mid-flight star-count
  amendment: a feature's own Acceptance Criteria genuinely required data the schema didn't yet
  capture, so closing the gap in the same feature (with a clear "why," an additive migration, and a
  documented backfill caveat — see PM-006) beat either silently shipping incomplete filtering or
  opening a whole separate feature for one column.
- **The "hidden gems only surface mega-popular repos" finding (see Current State) was raised and
  explicitly accepted by the operator before Phase 2 started** — don't re-flag it as a fresh
  discovery in a future session without checking here first; it's a known, accepted-as-is state,
  not an open item.
- **The "latest by time, not highest-ever value" `Score` rule now has a third and fourth correct
  application**: `GetHiddenGemsQueryHandler` and `GetDiscoveryFeedQueryHandler`'s `Score`/`Commits`
  sorts both got this right from the start in F-010 (following `ComputeScoresCommandHandler`'s
  original convention and the F-008 first-round mistake that established why it matters — see the
  Phase 2 handoff section below for that history). If a future feature also needs "this repo's
  current score," follow `OrderByDescending(ComputedAtUtc).First()`, never `Max(TotalScore)`.
- **Four distinct persistence patterns now coexist in this codebase, by design**: `Score` = append
  history (one row per re-score, never updated), `Summary` = create-once (never regenerated once
  created), `TrendAggregate` = upsert-by-key (`Category`/`PeriodStart`/`PeriodEnd`, updated in place
  on re-run), and now `Repository.FirstDiscoveredAtUtc` = set-once-on-insert-only (a single field
  within an otherwise-mutable entity, not a whole row's persistence style, but the same "never
  overwrite after first write" discipline as `Summary`). Each is deliberate and documented at its own
  handler — don't assume one pattern generalizes to a new stage without checking that stage's own
  idempotency/history requirements.
- **`TrendAggregate` has no repo-level FK, by design** — F-010's Trending endpoint computes
  "contributing repos" for a trend at query time (matching `PrimaryLanguage`, has both `Score` and
  `Summary`) rather than storing the relationship, deliberately mirroring
  `AggregateTrendsCommandHandler`'s own write-side membership criteria so the two never drift apart.
  If a future feature needs the same "which repos are in trend X" answer, reuse this same
  recomputation approach rather than adding a stored FK that could desync from F-009's own logic.
- **The graphify `--update` scope must stay pinned to `src/backend`** — this run's incremental diff
  initially misreported 17 still-present `src/frontend` files as deleted, because an earlier run's
  saved manifest had been built from a wider scan root. Verified false (files still on disk),
  deletion pruning was skipped for them, and the manifest was re-saved scoped correctly to
  `src/backend` — a future `--update` run scoped the same way should not repeat this. If it does,
  verify against the actual filesystem before trusting the prune step's "deleted" list, the same
  discipline Phase 2's handoff already established for the inverse (under-pruning) failure mode.
- **Version-pin caveats carried from earlier phases, still current**: LM Studio host-installed
  (ADR-016), `llama-3.2-3b-instruct` (ADR-017), PostgreSQL 18.4 (ADR-014), Angular 22 (ADR-012).
- **Open items**: PM-001, PM-002, PM-003 remain deferred exactly as before; new PM-006 (schema
  backfill, see What's Next #2).
- **Docs are governed, not exempt** — this run's Integration and Reviewer-Integration passes again
  treated the PMBook/test-cases/test-runbook as specs the code must satisfy, catching and fixing the
  test-runbook gap, correctly attributing the test-cases-doc authorship once challenged, and flagging
  the live-E2E gap it couldn't close itself. Keep doing this for F-011.

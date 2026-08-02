# Project Management: GitHub Hidden Gems Discovery Platform

> Status: ACTIVE
> Version: v17
> Last updated: 2026-08-02
> PRD: docs/prd.md (built against v4)
> Architecture: docs/architecture.md (built against v13)

## Phases & Milestones

| Phase | Goal | Status |
|-------|------|--------|
| Phase 0 | De-risk the two open feasibility questions (A1, A2) and stand up the deployable skeleton | Done |
| Phase 1 | Core data pipeline: ingest and score repositories | Done |
| Phase 2 | AI summarization and trend detection; dashboard UX design brief runs in parallel | Done |
| Phase 3 | Dashboard, API, and bookmarking | Planned |
| Phase 4 | Daily digest and observability | Planned |
| Phase 5 | Security, reliability, and scalability hardening | Planned |

## Feature Backlog

| ID | Feature | Phase | Priority | Status | Acceptance Criteria |
|----|---------|-------|----------|--------|---------------------|
| F-001 | Spike: GitHub GraphQL rate-limit budget validation | 0 | Must | Done | Point-cost model measured against a simulated 1K-5K repos/day discovery query; documented budget and back-off strategy; risk A1 marked resolved or a mitigation adopted. |
| F-002 | Spike: LM Studio inference throughput benchmark | 0 | Must | Done | Model identifier confirmed available in LM Studio's catalog — **confirmed 2026-08-01: `google/gemma-4-e4b` (spike §2 addendum)**; benchmark methodology executed live 2026-08-01 (spike §9.2), Pass vs. NFR-001 with ~10x headroom (spike §9.6); risk A2 resolved (spike §7/§9.5, Architecture §8). **Live run also found `gemma-4-e4b` truncates output on reasoning-token overhead (spike §9.4) — this led to a live comparison against 4 alternatives (spike §10) and a final model swap to `llama-3.2-3b-instruct` (ADR-017, supersedes ADR-013), which passes both throughput (0.78-1.05s mean, spike §10.2) and completeness (zero reasoning waste) checks.** |
| F-003 | Project scaffolding & Docker Compose skeleton | 0 | Must | Done | .NET 10 solution (all projects targeting net10.0), with Wolverine added and a vertical-slice folder convention established, and Angular 22 CLI project (standalone components; Angular Material + CDK added, verified compatible with Angular 22, and themed) both build; key .NET package dependencies (EF Core, Hangfire, Wolverine, GitHub API client, Npgsql) confirmed compatible with .NET 10 and PostgreSQL 18; Angular build output is copied into the ASP.NET Core host's static file root as part of the build/Docker image; Docker Compose brings up the app container (API + served dashboard) and `postgres:18.4` (pinned tag, not `latest`) together, health-checked; **LM Studio runs host-installed, not containerized (ADR-016 — amended 2026-08-01, operator already runs it natively) — a `Makefile` (`make up`) checks Docker, brings up Compose, checks/starts the host LM Studio server, and loads the configured model; `make status` confirms all three (app, postgres, LM Studio) are reachable.** |
| F-004 | Data Store schema (EF Core) | 1 | Must | Done | Migrations exist for repositories, scores, summaries, trend aggregates, bookmarks, and Hangfire job storage tables; applies cleanly to a fresh PostgreSQL 18.4 instance. **Hangfire's own storage tables are created by Hangfire.PostgreSql at F-006 runtime (its own `hangfire` schema), not by an EF Core migration — documented on `GitCrawlerDbContext` so F-006 doesn't duplicate schema setup.** |
| F-005 | GitHub Crawler | 1 | Must | Done | Implemented as a Wolverine command/handler slice (ADR-015); discovers new/updated repos via GraphQL (REST fallback for unsupported fields); writes raw metadata to the Data Store; respects the rate-limit budget from F-001 (FR-001). **§6/§7 mitigations from the F-001 spike genuinely implemented: rate-limit-aware retry (GraphQL `RATE_LIMITED`/`resetAt`, REST `x-ratelimit-*`/`Retry-After`, generic exponential backoff otherwise) and contributor-count caching (7-day freshness window) to keep the REST fallback — the actual binding constraint at scale — sustainable.** |
| F-006 | Job Scheduler (Hangfire) | 1 | Must | Done | Pipeline stages trigger on schedule via `RecurringJob` + `ContinueJobWith` chaining, each invoking the target stage's Wolverine command (ADR-015); a mid-run container restart resumes without duplicating or dropping work; dashboard reachable. **v1 has one real chain link (daily `RecurringJob` → Crawler via `DiscoverRepositoriesJob`); the `ContinueJobWith` attachment point for F-007 is documented in code, not stubbed. Dashboard is unauthenticated (operator decision, 2026-08-02) — a shared-secret query-string filter was tried first but broke the dashboard's own CSS/JS/stats requests (see ADR-009 Consequences), and no auth system exists elsewhere in this single-operator v1 to replace it with.** |
| F-007 | Scoring Engine | 1 | Must | Done | Implemented as a Wolverine command/handler slice (ADR-015); computes a hidden-gem score from license presence/type, commits-per-week, contributor count, fork count, and star count as independently identifiable, weighted inputs (star count added post-signoff per operator direction, weighted secondary to the primary four per Architecture §3's "existing popularity/quality signals"); scores persisted per repo (FR-002, FR-005). **Pipeline chain completed: `DiscoverRepositoriesJob` now attaches `ComputeScoresJob` via Hangfire `ContinueJobWith` after each crawl, so crawl → score runs end-to-end per Architecture §3's Job Scheduler dependency ordering.** |
| F-008 | Summarizer (LM Studio + Llama 3.2 3B Instruct) | 2 | Must | Done | Implemented as a Wolverine command/handler slice (ADR-015) calling `IRepositorySummarizer`, which calls LM Studio's local API running Llama 3.2 3B Instruct (ADR-017, supersedes ADR-013); generates a structured summary for top-scored repos without one; meets the throughput bar validated in F-002 (FR-003). **`Features/Summarization/GenerateSummaries/` — selects repos by latest (by `ComputedAtUtc`) `Score.TotalScore ≥ Summarization:MinimumScore` (default 40, judgment call) without an existing `Summary` row (create-once, never regenerated), capped at `Summarization:BatchSize` (default 20); README fetched via GitHub REST (`GET /repos/{owner}/{repo}/readme`, reusing the Crawler's REST client, 404 handled gracefully); per-repo failures (README or LM Studio) logged and skipped, not batch-aborting. `ComputeScoresJob` now attaches `GenerateSummariesJob` as chain link 3 via `ISummarizationContinuationLink`, completing the crawl → score → summarize chain.** |
| F-009 | Trend Aggregator | 2 | Must | Done | Implemented as a Wolverine command/handler slice (ADR-015); rolls up scored + summarized repos into technology/framework/ecosystem trend summaries on schedule; persisted for the dashboard and digest to consume (FR-008). **`Features/Trends/AggregateTrends/` — category = `Repository.PrimaryLanguage` (null-language repos excluded); counts repos with both a `Score` and a `Summary`, using each repo's latest (by `ComputedAtUtc`) `TotalScore`; single-day period by default (`Trends:PeriodDays`, default 1); upsert-by-`(Category, PeriodStart, PeriodEnd)` — a third distinct persistence pattern alongside Score's append-history and Summary's create-once, needed for NFR-003 idempotency. `GenerateSummariesJob` now attaches `AggregateTrendsJob` as chain link 4 via `ITrendsContinuationLink`, completing crawl → score → summarize → aggregate trends end-to-end.** |
| F-010 | Web API | 3 | Must | Planned | Endpoints organized as Wolverine command/query slices, one per operation (ADR-015); serve Discovery Feed, Hidden Gems, Trending, and Categories queries with filter/sort by language, star range, topic, and license; bookmark create/list/delete endpoints (FR-004, FR-007). |
| F-011 | Web Dashboard (Angular + Angular Material) | 3 | Must | Planned | Discovery Feed, Hidden Gems, Trending, and Categories render as distinct views, built with Angular Material components, backed by F-010; filter/sort controls work end-to-end; Angular build served correctly as static assets from the ASP.NET Core host (FR-009). |
| F-012 | Bookmarking | 3 | Must | Planned | User can bookmark a repo from the dashboard and revisit it later from a dedicated bookmarks view; persisted via F-010 (FR-007). |
| F-013 | Digest Service | 4 | Should | Planned | Implemented as a Wolverine command/handler slice (ADR-015); composes and sends a daily email with top hidden gems and trend summaries via SMTP; failure to send is logged, not silently dropped (FR-006). |
| F-014 | Observability | 4 | Should | Planned | Structured logging/metrics implemented as a Wolverine middleware (ADR-015) wrapping every command/query platform-wide, emitting records processed, duration, and failures per stage — beyond what the Hangfire dashboard (F-006) already covers — enough to diagnose a stuck or rate-limited run without a debugger (NFR-005). |
| F-015 | Security hardening | 5 | Must | Planned | GitHub API token loaded from environment/secrets config, never logged or committed; a repo-wide secret-scan check passes (NFR-002). |
| F-016 | Reliability/idempotency pass | 5 | Must | Planned | Every pipeline stage re-run against partially-completed state produces no duplicate records; GitHub API failures retry with backoff instead of aborting the run (NFR-003). |
| F-017 | Scalability: indexing & partitioning strategy | 5 | Should | Planned | Query plans for the dashboard's core filter/sort paths remain performant against a seeded dataset sized toward the 100k+ repos / 1M+ records target (NFR-004). |
| F-018 | Dashboard UX design brief & Claude Designer handoff | 2 | Must | Done | A written design brief (authored by Claude) covers: the Discovery Feed, Hidden Gems, Trending, and Categories layouts; filter/sort and bookmark interactions (FR-004, FR-007, FR-009); and an explicit constraint that all UI is composed from Angular Material components (ADR-011) with no custom/non-Material widgets. Brief is handed to Claude Designer; the resulting UX design is reviewed and approved before F-011 implementation begins. **`docs/design-briefs/dashboard-ux-brief.md` — bookmark "list" interaction (FR-007) resolved as a "bookmarked only" filter toggle within the four required views rather than a fifth dedicated view (that's F-012's scope); three genuine Material component gaps (infinite scroll, trend sparkline, skeleton loader) flagged explicitly with Material-native fallbacks. Handoff to Claude Designer is a document handoff, not a tool invocation — the actual design pass and its review/approval are a follow-up step outside this feature's scope, still gating F-011.** |

(IDs must be stable — never renumber once assigned)

## Dependencies

| Item | Depends On | Blocks | Notes |
|------|-----------|--------|-------|
| F-004 | F-003 | F-005, F-006, F-007 | Schema needs the scaffolded solution/containers first |
| F-005 | F-001, F-004 | F-007 | Crawler design should reflect the validated rate-limit budget |
| F-006 | F-004 | F-016 | Hangfire persistent storage lives in the Data Store schema |
| F-007 | F-004, F-005 | F-008, F-009, F-013 | Scoring needs raw metadata already ingested |
| F-008 | F-002, F-004, F-007 | F-009 | Summarizer targets the throughput bar validated in F-002; summarizes top-scored repos |
| F-009 | F-007, F-008 | F-010, F-013 | Trend rollup reads scored + summarized repos |
| F-010 | F-004, F-007, F-008, F-009 | F-011, F-012 | API surfaces the full pipeline's output |
| F-011 | F-010, F-018 | F-012 | Dashboard is built against both the API and the approved UX design |
| F-018 | — | F-011 | Can proceed independently of backend work — needs only the approved Functional Requirements (Architecture §5), not a running API |
| F-012 | F-010, F-011 | — | Bookmarking spans both the API and dashboard |
| F-013 | F-007, F-009 | — | Digest needs top gems + trend aggregates |
| F-014 | F-005, F-006, F-007, F-008, F-009 | — | Instrumented once the stages it observes exist; can proceed incrementally alongside them |
| F-015 | F-003 | — | Hardens the token/secrets handling introduced in scaffolding |
| F-016 | F-005, F-006, F-007, F-008, F-009 | — | Idempotency pass across all pipeline stages |
| F-017 | F-004 | — | Indexing strategy applied to the schema from F-004 |

## Out of Scope (confirmed in PRD)

**Deferred future enhancements:**
- Personalized discovery via user-defined interest profiles (see Open Items PM-002)
- Advanced personalization/recommendation engine
- GitHub account integration (OAuth-based personalization)
- Trend forecasting (predictive, vs. v1's descriptive trend detection)
- Semantic/intent-based search
- Repository-to-repository comparison

**Excluded by product identity:**
- Social features (comments, following, shared workspaces)
- Team workspaces / multi-user collaboration
- Browser extensions
- Delivery via Teams, Slack, Discord, or RSS

**Scoped down:**
- Bulk repository cloning (selective cloning only, via the GitHub API path)

## Open Items

| ID | Item | Owner | Due |
|----|------|-------|-----|
| PM-001 | Resolve PRD Q2: set numeric success-metric targets after an initial post-launch usage baseline | Maxx | Post-launch |
| PM-002 | Resolve PRD Q3: decide whether personalized discovery enters an early post-MVP phase | Maxx | After v1 ships |
| PM-003 | Define the revisit trigger/threshold for outgrowing single-node Docker Compose (Architecture risk A5) | Maxx | — |
| ~~PM-004~~ | ~~Run the live LM Studio throughput benchmark; confirm model availability; resolve risk A2~~ — **Closed 2026-08-01**: model confirmed (`google/gemma-4-e4b`), throughput measured (2.57-2.82s p95, Pass), risk A2 resolved. See `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9. | Maxx | Closed |
| ~~PM-005~~ | ~~F-008 (Summarizer) implementation must not reuse the F-002 spike's `max_tokens: 300` setting unmodified — `google/gemma-4-e4b` spends 65-86% of that budget on internal reasoning, truncating output~~ — **Closed 2026-08-01, resolved by model swap, not by adjusting `max_tokens`**: live comparison against 4 alternatives (spike §10) found `llama-3.2-3b-instruct` produces complete, natural-stop output with zero reasoning-token waste and is ~3x faster. ADR-017 supersedes ADR-013. F-008 still shouldn't assume unlimited headroom at `max_tokens: 300` for the new model (165/300 tokens used in testing — comfortable, not unlimited), but there's no known truncation risk requiring special handling. | Maxx | Closed |

## Version History
| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v1 | 2026-07-28 | Initial draft | — |
| v2 | 2026-07-28 | F-003 and F-011 updated for Angular dashboard (was Blazor Server) | Ripple from Architecture v4 |
| v3 | 2026-07-28 | F-004, F-006, and F-014 updated for Hangfire (was Quartz.NET); F-006 now also covers dashboard access control | Ripple from Architecture v5 |
| v4 | 2026-07-28 | F-003 acceptance criteria pinned to .NET 10, plus a package-compatibility check | Ripple from Architecture v6 |
| v5 | 2026-07-28 | F-003 and F-011 updated for Angular Material (ADR-011); F-003 now also pins the Angular version in package.json at scaffolding time | Ripple from Architecture v7 |
| v6 | 2026-07-28 | F-003 acceptance criteria pinned to Angular 22 specifically (was "latest stable") | Ripple from Architecture v8 |
| v7 | 2026-07-28 | Added F-018 (Dashboard UX design brief & Claude Designer handoff, Phase 2, Must); F-011 now depends on F-018 in addition to F-010 | Triage edit |
| v8 | 2026-07-28 | F-002, F-003, F-004, and F-008 updated to pin Gemma 4 E4B (ADR-013) and PostgreSQL 18.4 (ADR-014) | Ripple from Architecture v9 |
| v9 | 2026-07-28 | F-003, F-005 through F-010, F-013, and F-014 updated for Vertical Slice + CQRS via Wolverine (ADR-015), not MediatR | Ripple from Architecture v10 |
| v10 | 2026-07-28 | Status → ACTIVE | Gate approval |
| v11 | 2026-07-31 | Phase 0 complete: F-001, F-002, F-003 → Done. F-002's Acceptance Criteria reworded to explicitly allow "unverifiable in this environment, operator plan provided" as a valid terminal state (matches the Task Packet's original intent, closes a doc-drift finding raised by the Reviewer-Integration gate). Added PM-004 (run the live LM Studio benchmark before F-008) to track the resulting open verification. Phase 0 status → Done. | Orchestrator Finalization, Integration Agent doc-drift finding |
| v12 | 2026-08-01 | LM Studio changed from a Docker Compose container to host-installed (ADR-016) — F-003's AC updated to describe the new `Makefile`-orchestrated topology (Compose for app+postgres, Makefile for LM Studio). F-002's AC updated: model identifier now confirmed live (`google/gemma-4-e4b`) via `lms ls`/`lms load` against the operator's actual install; PM-004 narrowed to just the still-open throughput benchmark. | Operator correction: LM Studio already installed on the target machine, no container needed |
| v13 | 2026-08-01 | F-002's live throughput benchmark executed for real against `google/gemma-4-e4b` — 2.57-2.82s p95 per repo, Pass vs. NFR-001. PM-004 closed. New PM-005 added: F-008 must address a reasoning-token budget-truncation finding from the live run rather than reusing the spike's `max_tokens: 300` setting; F-008's AC updated accordingly. Architecture risk A2 → Resolved (Architecture v12). | F-002 spike executed live per operator request |
| v14 | 2026-08-01 | Summarization model changed from Gemma 4 E4B to Llama 3.2 3B Instruct, following a live comparison against 4 alternatives (spike §10) — ADR-017 (new) supersedes ADR-013. F-002 and F-008 AC updated; PM-005 closed by the model swap (not by the `max_tokens` mitigation it was originally written around). Architecture v13. | Operator: "use llama-3.2-3b-instruct, update docs. update spike and ADRs" |
| v15 | 2026-08-02 | Phase 1 complete: F-004, F-005, F-006, F-007 → Done (each PASS on first Developer/Reviewer attempt). F-007's AC amended mid-flight to add star count as a fifth weighted signal (operator direction, post-signoff) and to note the completed F-006→F-007 `ContinueJobWith` chain. F-004's AC annotated with the Hangfire-schema-separation clarification. F-005's AC annotated confirming the F-001 spike's §6/§7 mitigations were genuinely implemented, not just documented. F-006's AC annotated with the actual one-chain-link v1 state and the dashboard's shared-secret access-control mechanism. Phase 1 status → Done. | Orchestrator Finalization, Integration Agent + Reviewer-Integration both PASS |
| v16 | 2026-08-02 | F-006's dashboard access control removed: the shared-secret `?key=` filter broke the dashboard's own static assets and live stats polling (neither carry the query string forward), so it was dropped entirely rather than special-cased — no auth system exists elsewhere in this single-operator v1 to replace it with. F-006's AC and ADR-009's Consequences updated accordingly. | Operator: "remove the auth for hangfire" |
| v17 | 2026-08-02 | Phase 2 complete: F-008, F-009, F-018 → Done. All three verified against the actual implementation (not just the Orchestrator's self-reported annotations) — annotations checked out accurate. Phase 2 status → Done. | Integration Agent (Phase 2) |

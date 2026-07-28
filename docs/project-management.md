# Project Management: GitHub Hidden Gems Discovery Platform

> Status: ACTIVE
> Version: v10
> Last updated: 2026-07-28
> PRD: docs/prd.md (built against v4)
> Architecture: docs/architecture.md (built against v10)

## Phases & Milestones

| Phase | Goal | Status |
|-------|------|--------|
| Phase 0 | De-risk the two open feasibility questions (A1, A2) and stand up the deployable skeleton | Planned |
| Phase 1 | Core data pipeline: ingest and score repositories | Planned |
| Phase 2 | AI summarization and trend detection; dashboard UX design brief runs in parallel | Planned |
| Phase 3 | Dashboard, API, and bookmarking | Planned |
| Phase 4 | Daily digest and observability | Planned |
| Phase 5 | Security, reliability, and scalability hardening | Planned |

## Feature Backlog

| ID | Feature | Phase | Priority | Status | Acceptance Criteria |
|----|---------|-------|----------|--------|---------------------|
| F-001 | Spike: GitHub GraphQL rate-limit budget validation | 0 | Must | Planned | Point-cost model measured against a simulated 1K-5K repos/day discovery query; documented budget and back-off strategy; risk A1 marked resolved or a mitigation adopted. |
| F-002 | Spike: LM Studio inference throughput benchmark | 0 | Must | Planned | Gemma 4 E4B's exact model identifier/quantization confirmed available in LM Studio's catalog; benchmarked for summary generation time per repo; result compared against NFR-001's seconds-per-repo target; risk A2 marked resolved, or ADR-013/NFR-001 revisited if the model is unavailable or underperforms. |
| F-003 | Project scaffolding & Docker Compose skeleton | 0 | Must | Planned | .NET 10 solution (all projects targeting net10.0), with Wolverine added and a vertical-slice folder convention established, and Angular 22 CLI project (standalone components; Angular Material + CDK added, verified compatible with Angular 22, and themed) both build; key .NET package dependencies (EF Core, Hangfire, Wolverine, GitHub API client, Npgsql) confirmed compatible with .NET 10 and PostgreSQL 18; Angular build output is copied into the ASP.NET Core host's static file root as part of the build/Docker image; Docker Compose brings up the app container (API + served dashboard), `postgres:18.4` (pinned tag, not `latest`), and LM Studio together; health check confirms all three are reachable. |
| F-004 | Data Store schema (EF Core) | 1 | Must | Planned | Migrations exist for repositories, scores, summaries, trend aggregates, bookmarks, and Hangfire job storage tables; applies cleanly to a fresh PostgreSQL 18.4 instance. |
| F-005 | GitHub Crawler | 1 | Must | Planned | Implemented as a Wolverine command/handler slice (ADR-015); discovers new/updated repos via GraphQL (REST fallback for unsupported fields); writes raw metadata to the Data Store; respects the rate-limit budget from F-001 (FR-001). |
| F-006 | Job Scheduler (Hangfire) | 1 | Must | Planned | Pipeline stages trigger on schedule via `RecurringJob` + `ContinueJobWith` chaining, each invoking the target stage's Wolverine command (ADR-015); a mid-run container restart resumes without duplicating or dropping work; dashboard reachable and access-controlled (NFR-003). |
| F-007 | Scoring Engine | 1 | Must | Planned | Implemented as a Wolverine command/handler slice (ADR-015); computes a hidden-gem score from license presence/type, commits-per-week, contributor count, and fork count as independently identifiable, weighted inputs; scores persisted per repo (FR-002, FR-005). |
| F-008 | Summarizer (LM Studio + Gemma 4 E4B) | 2 | Must | Planned | Implemented as a Wolverine command/handler slice (ADR-015) calling `IRepositorySummarizer`, which calls LM Studio's local API running Gemma 4 E4B; generates a structured summary for top-scored repos without one; meets the throughput bar validated in F-002 (FR-003). |
| F-009 | Trend Aggregator | 2 | Must | Planned | Implemented as a Wolverine command/handler slice (ADR-015); rolls up scored + summarized repos into technology/framework/ecosystem trend summaries on schedule; persisted for the dashboard and digest to consume (FR-008). |
| F-010 | Web API | 3 | Must | Planned | Endpoints organized as Wolverine command/query slices, one per operation (ADR-015); serve Discovery Feed, Hidden Gems, Trending, and Categories queries with filter/sort by language, star range, topic, and license; bookmark create/list/delete endpoints (FR-004, FR-007). |
| F-011 | Web Dashboard (Angular + Angular Material) | 3 | Must | Planned | Discovery Feed, Hidden Gems, Trending, and Categories render as distinct views, built with Angular Material components, backed by F-010; filter/sort controls work end-to-end; Angular build served correctly as static assets from the ASP.NET Core host (FR-009). |
| F-012 | Bookmarking | 3 | Must | Planned | User can bookmark a repo from the dashboard and revisit it later from a dedicated bookmarks view; persisted via F-010 (FR-007). |
| F-013 | Digest Service | 4 | Should | Planned | Implemented as a Wolverine command/handler slice (ADR-015); composes and sends a daily email with top hidden gems and trend summaries via SMTP; failure to send is logged, not silently dropped (FR-006). |
| F-014 | Observability | 4 | Should | Planned | Structured logging/metrics implemented as a Wolverine middleware (ADR-015) wrapping every command/query platform-wide, emitting records processed, duration, and failures per stage — beyond what the Hangfire dashboard (F-006) already covers — enough to diagnose a stuck or rate-limited run without a debugger (NFR-005). |
| F-015 | Security hardening | 5 | Must | Planned | GitHub API token loaded from environment/secrets config, never logged or committed; a repo-wide secret-scan check passes (NFR-002). |
| F-016 | Reliability/idempotency pass | 5 | Must | Planned | Every pipeline stage re-run against partially-completed state produces no duplicate records; GitHub API failures retry with backoff instead of aborting the run (NFR-003). |
| F-017 | Scalability: indexing & partitioning strategy | 5 | Should | Planned | Query plans for the dashboard's core filter/sort paths remain performant against a seeded dataset sized toward the 100k+ repos / 1M+ records target (NFR-004). |
| F-018 | Dashboard UX design brief & Claude Designer handoff | 2 | Must | Planned | A written design brief (authored by Claude) covers: the Discovery Feed, Hidden Gems, Trending, and Categories layouts; filter/sort and bookmark interactions (FR-004, FR-007, FR-009); and an explicit constraint that all UI is composed from Angular Material components (ADR-011) with no custom/non-Material widgets. Brief is handed to Claude Designer; the resulting UX design is reviewed and approved before F-011 implementation begins. |

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

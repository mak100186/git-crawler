# Architecture: GitHub Hidden Gems Discovery Platform

> Status: APPROVED
> Version: v13
> Last updated: 2026-08-01
> PRD: docs/prd.md (built against v4)

## 1. System Context

The platform is a self-hosted (Docker Compose) system operated by a single engineer. It has three
external dependencies:

- **GitHub API** (GraphQL primary, REST fallback) — the sole source of repository discovery and
  metadata; no bulk repository cloning in v1.
- **Local LLM runtime** (LM Studio) — runs host-installed on the same machine (not containerized,
  ADR-016) and serves AI-generated repository summaries; no external AI vendor credential exists
  in v1 (ADR-001, ADR-007, ADR-016).
- **Email provider (SMTP)** — outbound channel for the daily digest; no inbound integration.

A single user type interacts with the system directly: the software engineer / engineering leader
browsing the dashboard and receiving the digest. There is no team, organization, or multi-tenant
concept in v1 (per PRD Non-Goals).

![System Context](diagrams/img/system-context.png)

## 2. High-Level Architecture

The platform is a modular .NET 10 monolith (ADR-010) — one deployable process hosting the Web API
and serving the Angular dashboard's static build (ADR-008), plus background components for
crawling, scoring, summarization, and trend aggregation — all orchestrated by an in-process job
scheduler (ADR-009). It is packaged as two Docker Compose services — the app container and
PostgreSQL (ADR-003) — plus a host-installed local LLM runtime (ADR-001) that Compose does not
manage; a `Makefile` orchestrates bringing both up together (ADR-016). This is a modular monolith
rather than microservices deliberately: a solo operator (per PRD constraints) benefits from one
deployable unit and one codebase far more than from independently-scalable services, and nothing
in the PRD's volume targets (1K-5K/day, scaling to 100k+) requires service-level horizontal
scaling yet (see ADR-002 consequences for the revisit trigger). The dashboard is a separate
Angular codebase from the .NET backend (ADR-008) but is still built into and served from the same
deployable process, so this split doesn't add a second runtime container.

Internally, every backend component — the Web API and each of the five background pipeline stages
— is organized as Vertical Slice Architecture, with a Wolverine command/query and its handler as
the unit of a slice, instead of a shared service/repository layer (ADR-015). Hangfire (ADR-009)
invokes a Wolverine command per pipeline stage rather than calling a service method directly.

Each pipeline stage (crawl → score → summarize → aggregate trends → send digest) is a distinct
component with a narrow responsibility, triggered on its own schedule but sharing the same
PostgreSQL data store as its integration point — components don't call each other directly, they
read/write shared state, which keeps each stage independently testable and restart-safe.

## 3. Components

### Crawler / Ingestion
- **Responsibility:** Discover new/updated repositories from GitHub matching baseline discovery
  criteria (age, activity, exclusions) and fetch the metadata fields the Scoring Engine needs.
- **Inputs:** Scheduler trigger; GitHub API responses.
- **Outputs:** Raw repository metadata records written to the Data Store.
- **Dependencies:** GitHub API (GraphQL-first, REST fallback — ADR-004); GitHub API token.
- **Technology:** .NET background service, GitHub GraphQL/REST clients; implemented as a Wolverine
  command/handler slice (ADR-015).

### Scoring Engine
- **Responsibility:** Compute the "hidden gem" score per repository from independently-weighted
  signals: license presence/type, commits-per-week activity, contributor count, fork count, and
  existing popularity/quality signals (per PRD Constraints).
- **Inputs:** Raw repository metadata from the Data Store.
- **Outputs:** Computed score + per-signal breakdown, written to the Data Store.
- **Dependencies:** Data Store.
- **Technology:** .NET library, pure computation (no external calls) — keeps scoring
  deterministic and independently unit-testable; implemented as a Wolverine command/handler slice
  (ADR-015).

### Summarizer
- **Responsibility:** Generate a concise, structured AI summary for repositories that clear the
  scoring threshold, from README/manifest content.
- **Inputs:** Repository content (README, manifest files) for top-scored repos without a summary.
- **Outputs:** Structured summary written to the Data Store.
- **Dependencies:** Local LLM Runtime, via the `IRepositorySummarizer` abstraction (ADR-001).
- **Technology:** .NET service calling LM Studio's local (OpenAI-compatible) API (ADR-007),
  running the Llama 3.2 3B Instruct model (ADR-017, supersedes ADR-013); implemented as a
  Wolverine command/handler slice (ADR-015).

### Trend Aggregator
- **Responsibility:** Roll up scored/summarized repositories into technology/framework/ecosystem
  trend summaries (Goal 3).
- **Inputs:** Scored + summarized repositories from the Data Store.
- **Outputs:** Trend aggregate records written to the Data Store.
- **Dependencies:** Data Store.
- **Technology:** .NET background service, scheduled batch job; implemented as a Wolverine
  command/handler slice (ADR-015).

### Digest Service
- **Responsibility:** Compose and send the daily email digest of top hidden gems and trends.
- **Inputs:** Top-scored repos + trend aggregates from the Data Store.
- **Outputs:** Outbound digest email.
- **Dependencies:** Data Store; Email Provider (SMTP).
- **Technology:** .NET background service, SMTP client; implemented as a Wolverine
  command/handler slice (ADR-015).

### Web API
- **Responsibility:** Serve the Dashboard's queries (feed, hidden gems, trending, categories,
  filter/sort) and handle bookmark writes, as a self-contained JSON API with no server-rendered
  view dependencies.
- **Inputs:** HTTP requests from the Dashboard.
- **Outputs:** JSON responses; bookmark writes to the Data Store.
- **Dependencies:** Data Store.
- **Technology:** ASP.NET Core minimal API; also serves the Angular build's static assets from the
  same process (ADR-008); endpoints organized as Wolverine command/query slices, one per operation
  (ADR-015).

### Web Dashboard
- **Responsibility:** Present the Discovery Feed, Hidden Gems, Trending, and Categories views;
  filter/sort by language, star range, topic, license; bookmark management.
- **Inputs:** User interaction; JSON data from the Web API.
- **Outputs:** Rendered UI; bookmark/filter HTTP requests to the Web API.
- **Dependencies:** Web API (same-origin HTTP, static files served from the same process).
- **Technology:** Angular 22 SPA (ADR-008, ADR-012), standalone components, UI built with Angular
  Material (ADR-011).

### Job Scheduler
- **Responsibility:** Trigger each pipeline stage on its schedule, in dependency order (crawl
  before score, score before summarize, summarize before trend rollup, trend rollup before
  digest), recover in-flight/misfired jobs after a restart, and expose run history/failures for
  operator monitoring.
- **Inputs:** Configured recurring schedules and stage-continuation chain.
- **Outputs:** Triggers to Crawler, Scoring Engine, Summarizer, Trend Aggregator, Digest Service;
  dashboard view of job state.
- **Dependencies:** Data Store (persistent job storage).
- **Technology:** Hangfire (`RecurringJob` + `ContinueJobWith`) with PostgreSQL-backed storage and
  its built-in monitoring dashboard (ADR-009); each trigger invokes a Wolverine command on the
  target stage (ADR-015).

### Data Store
- **Responsibility:** Durable storage for repository metadata, scores, summaries, trend
  aggregates, bookmarks, and Hangfire job state.
- **Inputs:** Writes from every other component.
- **Outputs:** Reads for every other component.
- **Dependencies:** None (leaf component).
- **Technology:** PostgreSQL 18.4, accessed via EF Core (ADR-003, ADR-014).

## 4. Data Flow

The primary use case is the daily discovery run, from scheduled crawl through to a user browsing
the dashboard and receiving the digest.

![Daily Discovery Flow](diagrams/img/daily-discovery-flow.png)

## 5. Functional Requirements

| ID | Requirement | Source (PRD story) | Priority |
|----|-------------|-------------------|----------|
| FR-001 | Discover new/updated repositories via the GitHub API on a recurring schedule | US-1 | Must |
| FR-002 | Compute a hidden-gem score per repository from weighted signals | US-1, US-4 | Must |
| FR-003 | Generate a structured AI summary per top-scored repository | US-2 | Must |
| FR-004 | Filter and sort repositories by language, star range, topic, and license | US-3 | Must |
| FR-005 | Expose license, commits-per-week, contributor count, and fork count as identifiable, independently-weighted scoring inputs | US-4 | Must |
| FR-006 | Compose and send a daily email digest of top hidden gems and trends | US-5 | Should |
| FR-007 | Allow a user to save/bookmark a repository and revisit it later | US-6 | Must |
| FR-008 | Aggregate discoveries into technology/framework/ecosystem trend summaries | US-7 | Must |
| FR-009 | Present Discovery Feed, Hidden Gems, Trending, and Categories as distinct dashboard views | US-8 | Must |

## 6. Non-Functional Requirements

| ID | Category | Requirement | Notes |
|----|----------|-------------|-------|
| NFR-001 | Performance | Summary generation completes on the order of seconds per repository; dashboard interactions feel interactive (target p95 page response < 2s) | Formalizes PRD's "Responsiveness assumption"; local LLM throughput (ADR-001) is the primary risk to this — see A2 |
| NFR-002 | Security | GitHub API token stored via environment/secrets configuration, never committed or logged; no AI provider credential exists in v1 since summarization is local (ADR-001) | Formalizes PRD's Security assumption |
| NFR-003 | Reliability | Every pipeline stage is idempotent and resumable after a container restart mid-run; GitHub API failures retry with backoff rather than aborting the run | Enabled by Hangfire's persistent job storage (ADR-009) |
| NFR-004 | Scalability | Schema and indexing support 100k+ repositories and 1M+ analysis records without a redesign | Formalizes PRD's processing volume assumption; partitioning/archiving strategy is an open risk — see A5 |
| NFR-005 | Observability | Each pipeline stage emits structured logs and stage-level metrics (records processed, duration, failures) so a solo operator can diagnose a stuck or rate-limited run without attaching a debugger | Not sourced from an explicit PRD line item; job-level piece (history, retries, failures) now covered by the Hangfire dashboard (ADR-009) — F-014 still needed for stage-level detail (e.g. per-signal scoring breakdowns) the dashboard doesn't capture |

## 7. Technology Decisions

| Concern | Choice | ADR | Rationale |
|---------|--------|-----|-----------|
| AI summarization backend | Local/self-hosted LLM via `IRepositorySummarizer` | ADR-001 | Avoids per-call cost at target volume; keeps repository content on self-hosted infra |
| Local LLM runtime engine | LM Studio | ADR-007 | Operator preference; OpenAI-compatible local API keeps the `IRepositorySummarizer` integration small |
| Local LLM runtime deployment | Host-installed, not containerized; reached via `host.docker.internal` | ADR-016 | Operator already runs LM Studio natively; a container would duplicate it, lose GPU/Metal acceleration, and depend on an unstable preview image |
| Summarization model | Llama 3.2 3B Instruct (loaded in LM Studio) | ADR-017 (supersedes ADR-013) | F-002's live benchmark found the original pin (Gemma 4 E4B) truncated output on reasoning-token overhead despite passing throughput; this model was chosen from a live comparison against 4 alternatives — fastest, zero reasoning waste, complete output |
| Deployment / hosting | Docker Compose (app + PostgreSQL), self-hosted/on-prem; `Makefile` bridges to the host-installed LLM runtime | ADR-002, ADR-016 | Matches solo, no-fixed-deadline project profile; single-entrypoint `make up` keeps the "everything comes up together" operator experience despite the split topology |
| Primary data store | PostgreSQL via EF Core | ADR-003 | Relational access pattern fits filter/sort/join-heavy queries; no license cost |
| Data store version | PostgreSQL 18.4 (pinned image tag) | ADR-014 | Operator preference; pinned tag avoids silent upgrade drift |
| GitHub API access strategy | GraphQL-first, REST fallback | ADR-004 | Minimizes rate-limit consumption per repository at 1K-5K/day scaling to 100k+ |
| Web dashboard framework | Angular SPA, served as static assets from the Web API process | ADR-008 | Matches operator's standing frontend preference; keeps single-deployable-process footprint (ADR-002) |
| Frontend framework version | Angular 22, standalone components | ADR-012 | Operator preference; pinned explicitly rather than resolved at scaffolding |
| Dashboard UI component library | Angular Material (`@angular/material` + `@angular/cdk`) | ADR-011 | Free, MIT-licensed, maintained by the Angular team; no built-vs-buy tradeoff for a solo developer |
| Job scheduling / orchestration | Hangfire, persistent job storage + built-in dashboard | ADR-009 | Survives restarts mid-pipeline; built-in dashboard directly serves NFR-005 for a solo operator |
| Runtime / SDK version | .NET 10 | ADR-010 | Operator preference; single version across all backend components |
| Internal code organization | Vertical Slice Architecture + CQRS via Wolverine, applied to the Web API and all five background pipeline stages | ADR-015 | Operator preference; explicitly not MediatR (which is moving toward commercial licensing) — Wolverine stays fully open-source |

## 8. Open Questions & Risks

| ID | Question / Risk | Impact | Owner | Resolved? |
|----|----------------|--------|-------|-----------|
| A1 | GitHub GraphQL rate-limit budget (point-cost model) has not been validated against the 1K-5K/day discovery volume, or the 100k+ scale-out target | High | Maxx | No |
| A2 | ~~Local LLM inference throughput/model choice is unproven against the seconds-per-repo target in NFR-001~~ — **Resolved 2026-08-01**: `google/gemma-4-e4b` measured at 2.57-2.82s p95 per repo (Pass), but found to truncate output on reasoning-token overhead; superseded by `llama-3.2-3b-instruct` (ADR-017), measured at 0.78-1.05s mean per repo with complete output. See `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9-§10 | High | Maxx | **Yes** |
| A3 | (Carried from PRD Q2) Numeric success-metric targets remain `[TBD]` pending an initial usage baseline post-launch | Low | Maxx | No |
| A4 | (Carried from PRD Q3) Whether personalized discovery lands in an early post-MVP phase is still undecided — affects PMBook phase sequencing, not v1 architecture | Medium | Maxx | No |
| A5 | Docker Compose (ADR-002) caps horizontal scaling; no defined trigger yet for when 100k+ repos / 1M+ records would force revisiting single-node deployment | Medium | Maxx | No |

## Version History
| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v1 | 2026-07-28 | Initial draft | — |
| v2 | 2026-07-28 | Local LLM runtime engine pinned to LM Studio (was illustrative "e.g. Ollama" in ADR-001); added ADR-007 and a Technology Decisions row for the runtime engine | Triage edit |
| v3 | 2026-07-28 | Status → APPROVED | Gate approval |
| v4 | 2026-07-28 | Web dashboard framework changed from Blazor Server to Angular SPA (ADR-008 supersedes ADR-005); dashboard now a separate codebase built to static assets and served from the same process | Triage edit |
| v5 | 2026-07-28 | Job scheduling changed from Quartz.NET to Hangfire (ADR-009 supersedes ADR-006) for its built-in monitoring dashboard against NFR-005; Job Scheduler component, NFR-003/NFR-005 notes, and Technology Decisions table updated | Triage edit |
| v6 | 2026-07-28 | Pinned runtime/SDK version to .NET 10 (ADR-010, new — nothing previously specified a version); also fixed a stale ADR-006 citation in §2 that should have read ADR-009 after the v5 edit | Triage edit |
| v7 | 2026-07-28 | Added Angular Material as the dashboard UI component library (ADR-011, new, free/MIT); noted Angular's own version is latest-stable-pinned-at-scaffolding rather than a fixed number, pending operator confirmation | Triage edit |
| v8 | 2026-07-28 | Pinned frontend framework version to Angular 22 (ADR-012, new) — replaces the "latest stable, resolved at scaffolding" placeholder from v7 | Triage edit |
| v9 | 2026-07-28 | Pinned summarization model to Gemma 4 E4B (ADR-013, new) and data store version to PostgreSQL 18.4 (ADR-014, new); neither had a version pinned previously | Triage edit |
| v10 | 2026-07-28 | Adopted Vertical Slice Architecture + CQRS via Wolverine (ADR-015, new), applied to the Web API and all five background pipeline stages, not MediatR; §2, all §3 component Technology lines, and Technology Decisions table updated | Triage edit |
| v11 | 2026-08-01 | LM Studio changed from a Docker Compose container to a host-installed native app, reached via `host.docker.internal` and orchestrated alongside Compose by a new `Makefile` (ADR-016, new — amends ADR-002 and ADR-007's deployment-topology framing, doesn't change the engine choice); §1, §2, and Technology Decisions table updated | Operator correction post-F-003 (LM Studio already installed on the target machine) |
| v12 | 2026-08-01 | Risk A2 marked Resolved (§8) — live benchmark run against `google/gemma-4-e4b`, 2.57-2.82s p95 per repo vs. NFR-001's target, see `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9 | F-002 spike executed live |
| v13 | 2026-08-01 | Summarization model changed from Gemma 4 E4B to Llama 3.2 3B Instruct (ADR-017, new, supersedes ADR-013) — the original pin passed throughput but truncated output on reasoning-token overhead; §3 Summarizer, §7 Technology Decisions, and §8 risk A2 updated | Live model comparison, operator decision: "use llama-3.2-3b-instruct" |

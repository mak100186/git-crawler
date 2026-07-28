# Architecture: GitHub Hidden Gems Discovery Platform

> Status: APPROVED
> Version: v3
> Last updated: 2026-07-28
> PRD: docs/prd.md (built against v4)

## 1. System Context

The platform is a self-hosted (Docker Compose) system operated by a single engineer. It has three
external dependencies:

- **GitHub API** (GraphQL primary, REST fallback) — the sole source of repository discovery and
  metadata; no bulk repository cloning in v1.
- **Local LLM runtime** (LM Studio) — runs alongside the platform's own containers and serves
  AI-generated repository summaries; no external AI vendor credential exists in v1 (ADR-001,
  ADR-007).
- **Email provider (SMTP)** — outbound channel for the daily digest; no inbound integration.

A single user type interacts with the system directly: the software engineer / engineering leader
browsing the dashboard and receiving the digest. There is no team, organization, or multi-tenant
concept in v1 (per PRD Non-Goals).

![System Context](diagrams/img/system-context.png)

## 2. High-Level Architecture

The platform is a modular .NET monolith — one deployable process hosting the Web API and Blazor
Server dashboard (ADR-005), plus background components for crawling, scoring, summarization, and
trend aggregation — all orchestrated by an in-process job scheduler (ADR-006). It is packaged as a
set of Docker Compose services: the app container, PostgreSQL (ADR-003), and the local LLM runtime
container (ADR-001). This is a modular monolith rather than microservices deliberately: a solo
operator (per PRD constraints) benefits from one deployable unit and one codebase far more than
from independently-scalable services, and nothing in the PRD's volume targets (1K-5K/day, scaling
to 100k+) requires service-level horizontal scaling yet (see ADR-002 consequences for the revisit
trigger).

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
- **Technology:** .NET background service, GitHub GraphQL/REST clients.

### Scoring Engine
- **Responsibility:** Compute the "hidden gem" score per repository from independently-weighted
  signals: license presence/type, commits-per-week activity, contributor count, fork count, and
  existing popularity/quality signals (per PRD Constraints).
- **Inputs:** Raw repository metadata from the Data Store.
- **Outputs:** Computed score + per-signal breakdown, written to the Data Store.
- **Dependencies:** Data Store.
- **Technology:** .NET library, pure computation (no external calls) — keeps scoring
  deterministic and independently unit-testable.

### Summarizer
- **Responsibility:** Generate a concise, structured AI summary for repositories that clear the
  scoring threshold, from README/manifest content.
- **Inputs:** Repository content (README, manifest files) for top-scored repos without a summary.
- **Outputs:** Structured summary written to the Data Store.
- **Dependencies:** Local LLM Runtime, via the `IRepositorySummarizer` abstraction (ADR-001).
- **Technology:** .NET service calling LM Studio's local (OpenAI-compatible) API (ADR-007).

### Trend Aggregator
- **Responsibility:** Roll up scored/summarized repositories into technology/framework/ecosystem
  trend summaries (Goal 3).
- **Inputs:** Scored + summarized repositories from the Data Store.
- **Outputs:** Trend aggregate records written to the Data Store.
- **Dependencies:** Data Store.
- **Technology:** .NET background service, scheduled batch job.

### Digest Service
- **Responsibility:** Compose and send the daily email digest of top hidden gems and trends.
- **Inputs:** Top-scored repos + trend aggregates from the Data Store.
- **Outputs:** Outbound digest email.
- **Dependencies:** Data Store; Email Provider (SMTP).
- **Technology:** .NET background service, SMTP client.

### Web API
- **Responsibility:** Serve the Dashboard's queries (feed, hidden gems, trending, categories,
  filter/sort) and handle bookmark writes.
- **Inputs:** HTTP requests from the Dashboard.
- **Outputs:** JSON responses; bookmark writes to the Data Store.
- **Dependencies:** Data Store.
- **Technology:** ASP.NET Core minimal API, same process as the Dashboard (ADR-005).

### Web Dashboard
- **Responsibility:** Present the Discovery Feed, Hidden Gems, Trending, and Categories views;
  filter/sort by language, star range, topic, license; bookmark management.
- **Inputs:** User interaction; data from the Web API.
- **Outputs:** Rendered UI; bookmark/filter requests.
- **Dependencies:** Web API (same process).
- **Technology:** Blazor Server (ADR-005).

### Job Scheduler
- **Responsibility:** Trigger each pipeline stage on its schedule, in dependency order (crawl
  before score, score before summarize, summarize before trend rollup, trend rollup before
  digest), and recover in-flight/misfired jobs after a restart.
- **Inputs:** Configured cron schedules and job dependency graph.
- **Outputs:** Triggers to Crawler, Scoring Engine, Summarizer, Trend Aggregator, Digest Service.
- **Dependencies:** Data Store (persistent job store).
- **Technology:** Quartz.NET with a PostgreSQL-backed persistent job store (ADR-006).

### Data Store
- **Responsibility:** Durable storage for repository metadata, scores, summaries, trend
  aggregates, bookmarks, and Quartz.NET job state.
- **Inputs:** Writes from every other component.
- **Outputs:** Reads for every other component.
- **Dependencies:** None (leaf component).
- **Technology:** PostgreSQL, accessed via EF Core (ADR-003).

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
| NFR-003 | Reliability | Every pipeline stage is idempotent and resumable after a container restart mid-run; GitHub API failures retry with backoff rather than aborting the run | Enabled by Quartz.NET's persistent job store (ADR-006) |
| NFR-004 | Scalability | Schema and indexing support 100k+ repositories and 1M+ analysis records without a redesign | Formalizes PRD's processing volume assumption; partitioning/archiving strategy is an open risk — see A5 |
| NFR-005 | Observability | Each pipeline stage emits structured logs and stage-level metrics (records processed, duration, failures) so a solo operator can diagnose a stuck or rate-limited run without attaching a debugger | Not sourced from an explicit PRD line item, but required in practice for a multi-stage async pipeline with no dedicated ops team |

## 7. Technology Decisions

| Concern | Choice | ADR | Rationale |
|---------|--------|-----|-----------|
| AI summarization backend | Local/self-hosted LLM via `IRepositorySummarizer` | ADR-001 | Avoids per-call cost at target volume; keeps repository content on self-hosted infra |
| Local LLM runtime engine | LM Studio | ADR-007 | Operator preference; OpenAI-compatible local API keeps the `IRepositorySummarizer` integration small |
| Deployment / hosting | Docker Compose, self-hosted/on-prem | ADR-002 | Matches solo, no-fixed-deadline project profile; simplifies co-locating the local LLM runtime |
| Primary data store | PostgreSQL via EF Core | ADR-003 | Relational access pattern fits filter/sort/join-heavy queries; no license cost |
| GitHub API access strategy | GraphQL-first, REST fallback | ADR-004 | Minimizes rate-limit consumption per repository at 1K-5K/day scaling to 100k+ |
| Web dashboard framework | Blazor Server | ADR-005 | Single C# stack for a solo maintainer; keeps data access server-side |
| Job scheduling / orchestration | Quartz.NET, persistent job store | ADR-006 | Survives restarts mid-pipeline; expresses stage dependency ordering explicitly |

## 8. Open Questions & Risks

| ID | Question / Risk | Impact | Owner | Resolved? |
|----|----------------|--------|-------|-----------|
| A1 | GitHub GraphQL rate-limit budget (point-cost model) has not been validated against the 1K-5K/day discovery volume, or the 100k+ scale-out target | High | Maxx | No |
| A2 | Local LLM inference throughput/model choice is unproven against the seconds-per-repo target in NFR-001 — needs a benchmarking spike before committing to a specific model | High | Maxx | No |
| A3 | (Carried from PRD Q2) Numeric success-metric targets remain `[TBD]` pending an initial usage baseline post-launch | Low | Maxx | No |
| A4 | (Carried from PRD Q3) Whether personalized discovery lands in an early post-MVP phase is still undecided — affects PMBook phase sequencing, not v1 architecture | Medium | Maxx | No |
| A5 | Docker Compose (ADR-002) caps horizontal scaling; no defined trigger yet for when 100k+ repos / 1M+ records would force revisiting single-node deployment | Medium | Maxx | No |

## Version History
| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v1 | 2026-07-28 | Initial draft | — |
| v2 | 2026-07-28 | Local LLM runtime engine pinned to LM Studio (was illustrative "e.g. Ollama" in ADR-001); added ADR-007 and a Technology Decisions row for the runtime engine | Triage edit |
| v3 | 2026-07-28 | Status → APPROVED | Gate approval |

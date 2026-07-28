# Handoff: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-07-28

## What was done

Idea triage is complete, end to end:

- **PRD** (`docs/prd.md`, APPROVED, v4) — problem, goals, non-goals, 8 user stories, success
  metrics, constraints. 3 open questions carried forward (Q1 resolved during Architecture; Q2/Q3
  deliberately deferred).
- **Architecture** (`docs/architecture.md`, APPROVED, v10) — self-hosted .NET 10 modular monolith,
  Vertical Slice + CQRS via Wolverine platform-wide, backed by 15 ADRs in `docs/adr/` covering
  every non-trivial technology choice.
- **PMBook** (`docs/project-management.md`, ACTIVE, v10) — 6 phases, 18 backlog items (F-001
  through F-018), dependency graph, Out of Scope list mirroring the PRD's Non-Goals, and 3 open
  items (PM-001–PM-003).

All of it is committed to git (`main`, 3 commits this session: architecture triage finalization,
PMBook gate closure). Working tree is clean.

## Current state

No source code exists yet. This is purely a planning/architecture handoff — nothing has been
scaffolded, built, or deployed. The full technology stack is decided and version-pinned:

| Layer | Choice |
|---|---|
| Backend runtime | .NET 10 (ADR-010) |
| Backend pattern | Vertical Slice Architecture + CQRS via Wolverine, not MediatR (ADR-015) |
| Job scheduling | Hangfire, persistent storage + built-in dashboard (ADR-009) |
| Data store | PostgreSQL 18.4, pinned image tag (ADR-003, ADR-014) |
| AI summarization | Local, via LM Studio running Gemma 4 E4B (ADR-001, ADR-007, ADR-013) |
| Frontend | Angular 22, standalone components (ADR-008, ADR-012) |
| UI components | Angular Material (ADR-011) |
| Deployment | Docker Compose, self-hosted/on-prem (ADR-002) |

## What's next

1. Invoke `orchestrator-development-pattern`, pointed at `docs/project-management.md`.
2. Start with Phase 0 — no interdependencies between its three items, so they can run in any order
   or in parallel:
   - **F-001** — Spike: GitHub GraphQL rate-limit budget validation (de-risks A1)
   - **F-002** — Spike: LM Studio inference throughput benchmark for Gemma 4 E4B (de-risks A2)
   - **F-003** — Project scaffolding & Docker Compose skeleton
3. F-004 onward follows the Dependencies table in the PMBook.
4. **F-018** (Dashboard UX design brief & Claude Designer handoff) needs a written brief from
   Claude at that point in the build — see its acceptance criteria in the PMBook. It blocks F-011
   (dashboard implementation).

## Important context

- **F-001 and F-002 are load-bearing spikes, not busywork** — they exist specifically to validate
  two High-impact risks (Architecture §8, A1/A2) before the rest of the pipeline is built against
  unverified assumptions. If either spike fails, the relevant ADR (ADR-004 for A1, ADR-013 for A2)
  or NFR-001 should be revisited before proceeding, per each item's acceptance criteria.
- **Version-pin caveats:** ADR-012 (Angular 22) and ADR-013 (Gemma 4 E4B) were pinned to the
  operator's explicit instruction, but I could not independently verify either identifier against
  current reality — both postdate what I can confirm from training data. F-002 already carries the
  job of confirming Gemma 4 E4B's exact identifier in LM Studio's catalog; when F-003 scaffolds the
  Angular project, confirm Angular 22 is actually what `ng new` resolves to (or install it
  explicitly) before treating the pin as validated.
- **Open items not blocking build:** PM-001 (numeric success-metric targets) and PM-002
  (personalized discovery phasing) are explicitly deferred to post-launch/post-v1 — don't let them
  stall Phase 0–5 work. PM-003 (Docker Compose scaling ceiling) has no defined trigger yet; revisit
  only if volume actually approaches the 100k+ repos / 1M+ records target.
- **Docs are governed, not exempt** — PRD/Architecture/PMBook/ADRs live in git and go through the
  same review as code per the idea-triage and orchestrator rules; don't treat them as side
  artifacts once build starts.

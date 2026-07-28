# ADR-006: Quartz.NET for Job Scheduling

> Status: SUPERSEDED BY ADR-009
> Date: 2026-07-28
> Architecture: docs/architecture.md (v1)

## Context

The pipeline (crawl → score → summarize → aggregate trends → send digest) runs as a sequence of
scheduled, dependent jobs, self-hosted in Docker Compose (ADR-002), that must survive a container
restart mid-run without duplicating work or silently dropping a stage as volume grows toward
100k+ repos.

## Decision

Job scheduling and orchestration across the pipeline stages is implemented with Quartz.NET, using
its persistent job store (backed by the PostgreSQL data store, ADR-003) so schedules and
in-flight/misfired job state survive a restart.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Plain `BackgroundService` + a cron-parsing library (e.g. Cronos) | Simpler to start with, but provides no built-in persistence or misfire handling — a container restart mid-crawl would need bespoke recovery logic that Quartz.NET already provides. |
| Hangfire | Comparable feature set to Quartz.NET for this use case, but its dashboard/UI and job-type model are oriented around fire-and-forget/queued jobs rather than Quartz's cron-triggered, dependency-ordered scheduling, which is the closer fit for this pipeline's fixed daily stages. |

## Consequences

- Job state (schedule, last-run, misfire recovery) persists in PostgreSQL, so a restart during the
  daily run resumes correctly instead of silently skipping a stage.
- Adds a dependency and a small amount of configuration (job store setup) beyond a plain hosted
  service, which is justified by the reliability requirement (NFR-003) at growing volume.
- Pipeline stage ordering (crawl → score → summarize → aggregate → digest) is expressed as
  Quartz trigger/job dependencies, keeping the orchestration logic in one place rather than
  scattered across ad-hoc timers.

## Related

- Architecture section: 3. Components → Job Scheduler; 6. Non-Functional Requirements (NFR-003)
- Supersedes: none

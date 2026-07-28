# ADR-009: Hangfire for Job Scheduling

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v5)

## Context

ADR-006 picked Quartz.NET on the premise that Hangfire's job model doesn't fit the pipeline's
cron-triggered, dependency-ordered stages (crawl → score → summarize → aggregate trends → send
digest) as well as Quartz's trigger graph. That premise undersold Hangfire: `RecurringJob` covers
the cron-scheduling need and `BackgroundJob.ContinueJobWith` covers stage chaining, so both
libraries can express this pipeline. The operator has flagged a concrete, previously
underweighted differentiator: Hangfire ships a built-in monitoring dashboard (job history,
retries, failures, currently-executing state), which directly serves NFR-005 (Observability) —
a solo operator diagnosing a stuck or rate-limited run without a debugger — where Quartz.NET has
no first-party equivalent.

## Decision

Job scheduling and orchestration across the pipeline stages is implemented with Hangfire, using
its PostgreSQL-backed persistent storage (ADR-003) for job state, and its built-in dashboard is
exposed (behind the platform's own access controls) as the primary tool for monitoring pipeline
runs.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Quartz.NET (ADR-006) | Superseded by this ADR — its trigger-graph model is not meaningfully better suited to this pipeline than Hangfire's `RecurringJob` + `ContinueJobWith`, and it has no built-in monitoring UI, which Hangfire provides for free against NFR-005. |
| Plain `BackgroundService` + a cron-parsing library (e.g. Cronos) | Still ruled out for the same reason as in ADR-006: no built-in persistence, misfire handling, or monitoring — all bespoke work that both Quartz.NET and Hangfire already solve. |

## Consequences

- Job state (schedule, last-run, retry/failure history) persists in PostgreSQL, so a restart
  during the daily run resumes correctly instead of silently skipping a stage (same guarantee
  ADR-006 provided).
- The Hangfire dashboard gives the solo operator job-level observability (NFR-005) out of the
  box, reducing — though not eliminating — the custom logging/metrics work F-014 still needs for
  stage-level detail the dashboard doesn't capture (e.g. per-signal scoring breakdowns).
- Pipeline stage ordering is expressed as `RecurringJob` schedules plus `ContinueJobWith`
  continuations rather than Quartz's trigger-dependency graph — a different idiom, not a
  capability loss, but code written against Quartz's API in F-006 would need to be rewritten
  against Hangfire's.
- The dashboard endpoint itself becomes a surface that needs access control in the self-hosted
  deployment (ADR-002) — it must not be exposed unauthenticated.

## Related

- Architecture section: 3. Components → Job Scheduler; 6. Non-Functional Requirements (NFR-005)
- Supersedes: ADR-006

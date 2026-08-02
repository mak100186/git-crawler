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
exposed unauthenticated as the primary tool for monitoring pipeline runs (see Consequences —
access control was tried and reverted).

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
- **Tried and reverted (2026-08-02):** a shared-secret `?key=` query-string filter
  (`HangfireDashboardAuthorizationFilter`) initially gated the dashboard, fail-closed by default.
  Hangfire applies whatever `IDashboardAuthorizationFilter` is configured to *every* request under
  `/hangfire`, including the dashboard's own CSS/JS assets and its stats-polling XHR — none of
  which carry the page's `?key=` query string forward (relative URLs don't inherit it), so the
  filter also denied those, leaving the dashboard unstyled and its live stats erroring. Removed
  entirely rather than special-cased further: this is a single-operator, self-hosted diagnostic
  tool with no other auth system in the app, so the operator's own network boundary (don't publish
  the port beyond localhost/a trusted network) is the access control, not an in-app filter.
- **Gotcha found while removing the filter above:** simply omitting `DashboardOptions` from
  `UseHangfireDashboard` does *not* make the dashboard unauthenticated — Hangfire's own default
  (`DashboardOptions.Authorization` unset) falls back to a `LocalRequestsOnlyAuthorizationFilter`,
  and Docker Desktop's port-publishing proxy doesn't preserve `127.0.0.1` as the apparent remote
  address for a host-browser request through it (the exact reason a loopback check was rejected
  as an alternative to the shared-secret filter in the first place, above). `Program.cs` passes
  `Authorization = []` explicitly to actually disable the check.

## Related

- Architecture section: 3. Components → Job Scheduler; 6. Non-Functional Requirements (NFR-005)
- Supersedes: ADR-006

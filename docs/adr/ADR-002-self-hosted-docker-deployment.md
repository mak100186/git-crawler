# ADR-002: Self-Hosted Deployment via Docker Compose

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v1)

## Context

The PRD frames this as a solo project with no fixed external deadline and no team/multi-tenant
requirement. The system has several independently-schedulable components (crawler, scoring,
summarizer, trend aggregator, digest, web API/dashboard) plus a local LLM runtime (ADR-001), all of
which need to run together and be operable by one person without dedicated platform-ops time.

## Decision

The platform is deployed as a set of Docker containers orchestrated via Docker Compose on
self-hosted/on-prem infrastructure, rather than a managed cloud platform.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Azure (App Service / Container Apps + managed DB) | Adds cloud billing and IAM/config surface disproportionate to a solo, no-deadline project; also pulls against ADR-001's local-inference decision, since a managed cloud host complicates running a local LLM runtime alongside the app. |
| Kubernetes (self-hosted or managed) | Orchestration complexity (manifests, ingress, secrets management, cluster ops) is unjustified for a single-operator deployment at current scale; revisit only if horizontal scaling needs outgrow Compose. |

## Consequences

- Full control over infra and no cloud vendor cost, at the cost of the operator owning uptime,
  backups, and OS/patching themselves.
- Docker Compose caps how far this scales horizontally; if the 100k+ repos / 1M+ analysis records
  growth path eventually requires multi-node scaling, this decision should be revisited (a new ADR
  superseding this one, not an edit to it).
- All components (API, crawler, scoring, summarizer, DB) must be containerized from the start,
  which keeps local dev and self-hosted prod environments identical. (Amended by ADR-016: the LLM
  runtime specifically is host-installed, not containerized, since the operator already runs LM
  Studio natively — a `Makefile` orchestrates bringing both the containerized and host-installed
  pieces up together instead.)

## Related

- Architecture section: 2. High-Level Architecture; 7. Technology Decisions
- Amended by: ADR-016 (LLM runtime is host-installed, not containerized; all other components remain containerized as decided here)
- Supersedes: none

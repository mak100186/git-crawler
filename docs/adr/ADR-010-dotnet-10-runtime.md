# ADR-010: .NET 10 as the Runtime/SDK Version

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v6)

## Context

The repository's `.gitignore` has implied a .NET stack since the initial scaffold, but no
Architecture decision has pinned a specific runtime/SDK version. Every backend component (Crawler,
Scoring Engine, Summarizer, Trend Aggregator, Digest Service, Web API, Job Scheduler) targets the
same runtime, so this is a single, once-per-solution decision rather than a per-component one.

## Decision

All .NET components target .NET 10.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| .NET 8 (previous LTS) | Was still a reasonable choice for longer-term support stability, but the operator's explicit preference is .NET 10; as a solo greenfield project with no legacy dependency pinned to .NET 8, there's no compatibility reason to stay behind. |

## Consequences

- All backend components, Docker base images, and CI/build tooling must target .NET 10
  consistently — no mixed-version components.
- Library/package compatibility (EF Core, Hangfire, GitHub API client libraries) must be verified
  against .NET 10 during scaffolding (F-003); any library lagging in .NET 10 support becomes a
  blocker to flag early rather than discovered mid-feature.
- Support lifecycle (LTS vs STS status, end-of-support date) for .NET 10 should be checked directly
  against Microsoft's published .NET support policy at build time, since that detail can shift and
  isn't restated here.

## Related

- Architecture section: 2. High-Level Architecture; 7. Technology Decisions
- Supersedes: none

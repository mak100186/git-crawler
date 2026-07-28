# ADR-012: Angular 22 as the Frontend Framework Version

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v8)

## Context

ADR-008 established Angular as the dashboard SPA framework but left the specific major version
unpinned ("latest stable, resolved at scaffolding"). The operator has since specified Angular 22
directly.

## Decision

The dashboard targets Angular 22, using its standalone-components architecture (no NgModules).

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Leave unpinned, resolve "latest stable" at F-003 scaffolding time | Was the prior default in ADR-008/architecture v7, but the operator has an explicit version preference, so pinning it now removes ambiguity for scaffolding rather than deferring it. |
| An older Angular LTS version | No stability or compatibility reason surfaced to prefer an older version for a greenfield project with no legacy Angular codebase to reconcile with. |

## Consequences

- F-003 scaffolding targets Angular 22 explicitly in `package.json`, rather than "whatever `ng new`
  resolves to at the time" — removes drift risk between this document and the actual scaffolded
  project.
- Angular Material (ADR-011) and Angular CDK versions used must be compatible with Angular 22 —
  verified during scaffolding (F-003), same as the .NET 10 package-compatibility check (ADR-010).
- If Angular releases newer majors before scaffolding actually happens, this ADR would need to be
  revisited (a new ADR superseding this one) rather than silently drifting to a different version.

## Related

- Architecture section: 3. Components → Web Dashboard; 7. Technology Decisions
- Supersedes: none

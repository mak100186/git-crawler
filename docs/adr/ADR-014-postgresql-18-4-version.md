# ADR-014: PostgreSQL 18.4 as the Data Store Version

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v9)

## Context

ADR-003 established PostgreSQL as the data store engine but left the version unpinned. F-003
(Docker Compose scaffolding) needs an explicit image tag rather than a floating `latest`, and F-004
(schema/migrations) needs a target version to develop against.

## Decision

The Data Store runs PostgreSQL 18.4, pinned as the `postgres:18.4` image tag in Docker Compose
(ADR-002).

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| `postgres:latest` (floating tag) | Risks an unplanned major-version upgrade on a routine `docker compose pull`, which is worse for a solo-operated system than deliberately bumping a pinned version when ready. |
| PostgreSQL 17 (previous major) | No compatibility constraint favors staying behind; the operator specified 18.4 directly for this greenfield project. |

## Consequences

- Docker Compose pins `postgres:18.4` explicitly rather than a floating tag — upgrades become a
  deliberate, tracked change (a new ADR superseding this one), not silent drift.
- The EF Core provider (Npgsql) version used must be verified compatible with PostgreSQL 18 during
  F-003 scaffolding.
- Schema/migration work in F-004 targets PostgreSQL 18 feature/behavior baseline.

## Related

- Architecture section: 3. Components → Data Store; 7. Technology Decisions
- Builds on: ADR-003
- Supersedes: none

# ADR-003: PostgreSQL as Primary Data Store

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v1)

## Context

The system needs a single relational store for repo metadata, computed scores, summaries, trend
aggregates, and per-user bookmarks, sized to reach 100k+ repositories and 1M+ analysis records
without a redesign, running inside the self-hosted Docker Compose deployment (ADR-002) at no
license cost.

## Decision

PostgreSQL is the primary data store, accessed from .NET via EF Core.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| SQL Server | No meaningful capability advantage here and carries licensing cost/complexity for a self-hosted solo deployment that PostgreSQL avoids entirely. |
| Document store (e.g. MongoDB) | Repo metadata, scores, and trend aggregates are relational/tabular by nature (join-heavy queries for filter/sort by language, stars, topic, license); a document model would fight the access pattern rather than help it. |

## Consequences

- Well-supported by EF Core migrations, keeping schema evolution straightforward as scoring
  signals or entities change.
- Indexing/partitioning strategy must be planned deliberately as volume grows toward 100k+
  repos / 1M+ records (see NFR-004); this is a known follow-up, not solved by the choice alone.
- Runs as a standard container in the same Docker Compose stack as the app services (ADR-002),
  keeping local dev and prod topology identical.

## Related

- Architecture section: 3. Components → Data Store; 6. Non-Functional Requirements (NFR-004)
- Supersedes: none

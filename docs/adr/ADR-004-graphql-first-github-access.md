# ADR-004: GraphQL-First GitHub API Access

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v1)

## Context

The PRD constrains discovery to the GitHub API (REST and/or GraphQL) rather than bulk cloning, at a
volume of 1,000-5,000 repos/day scaling toward 100k+. GitHub enforces separate rate-limit budgets
and cost models for REST vs GraphQL, and the fields the scoring engine needs (license, contributor
count, fork count, commit recency) span multiple REST endpoints per repo but can mostly be fetched
in a single GraphQL query.

## Decision

The Crawler uses the GitHub GraphQL API as the primary access path for bulk discovery and metadata
fetch, falling back to REST only for specific data GraphQL doesn't expose (e.g. certain
commit-activity statistics endpoints).

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| REST-only | Requires multiple round-trips per repo to assemble the fields the scoring engine needs (license, contributors, forks, activity), burning rate-limit budget faster at 1K-5K repos/day and worse at 100k+ scale. |
| GraphQL-only | Some data the scoring/crawling pipeline may need is only available via specific REST endpoints (e.g. certain statistics endpoints return 202-and-compute-async behavior with no GraphQL equivalent); a hard GraphQL-only rule would force awkward workarounds. |

## Consequences

- Fewer API calls per repo materially improves rate-limit headroom as volume scales, which is the
  main reason for this choice.
- The Crawler component owns two client code paths (GraphQL primary, REST fallback) instead of
  one, which is a small ongoing maintenance cost.
- GitHub's GraphQL rate-limit model (point-cost-based, not request-count-based) needs to be
  understood and budgeted for explicitly — flagged as an open risk (A2) pending a rate-limit
  budget spike.

## Related

- Architecture section: 3. Components → Crawler / Ingestion; 8. Open Questions & Risks (A2)
- Supersedes: none

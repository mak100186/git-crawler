# Project Management: GitHub Hidden Gems Discovery Platform

> Status: ACTIVE
> Tranche: v2
> Version: v40
> Last updated: 2026-08-07
> PRD: docs/prd.md (built against v8)
> Architecture: docs/architecture.md (built against v30)

## Closed Tranches

| Tranche | Phases | Status | Archive |
|-------|--------|--------|---------|
| v1 | Phase 0-5 | Done | [docs/archive/v1-mvp/project-management.md](archive/v1-mvp/project-management.md) |

## Phases & Milestones

| Phase | Goal | Status |
|-------|------|--------|

(none yet — Tranche v2 phases are added during PMBook triage; numbering continues from Phase 6, the next integer after v1's highest)

## Feature Backlog

| ID | Feature | Phase | Priority | Status | Acceptance Criteria |
|----|---------|-------|----------|--------|---------------------|

(none yet — v2 feature IDs continue from F-019, the next integer after v1's highest. IDs must be stable — never renumber once assigned)

## Dependencies

| Item | Depends On | Blocks | Notes |
|------|-----------|--------|-------|

(none yet)

## Out of Scope (confirmed in PRD)

**Deferred future enhancements:**
- Personalized discovery via user-defined interest profiles (see Open Items PM-002)
- Advanced personalization/recommendation engine
- GitHub account integration (OAuth-based personalization)
- Trend forecasting (predictive, vs. v1's descriptive trend detection)
- Semantic/intent-based search
- Repository-to-repository comparison

**Excluded by product identity:**
- Social features (comments, following, shared workspaces)
- Team workspaces / multi-user collaboration
- Browser extensions
- Delivery via Teams, Slack, Discord, or RSS

**Scoped down:**
- Bulk repository cloning (selective cloning only, via the GitHub API path)

(carried forward from v1 unchanged — the PRD itself wasn't touched by this closeout; any of these graduating into v2 scope happens via normal PRD Phase 1 triage, not here)

## Open Items

| ID | Item | Owner | Due |
|----|------|-------|-----|
| PM-001 | Resolve PRD Q2: set numeric success-metric targets after an initial post-launch usage baseline | Maxx | Post-launch |
| PM-002 | Resolve PRD Q3: decide whether personalized discovery enters an early post-MVP phase | Maxx | After v1 ships |
| PM-003 | Define the revisit trigger/threshold for outgrowing single-node Docker Compose (Architecture risk A5). PM-008's authoritative `make seed-perf` measurement at 100k repos / 1M scores shows the unfiltered Score/Commits sort path takes 4.8–4.9s (exceeding NFR-001's 2s budget by ~2.5×) — the query-performance half of the scale-out trigger is now concretely measured, not theoretical. The remaining unevaluated half is horizontal scaling (bigger instance vs. service split), which Architecture risk A5 still carries as unresolved. When repository count approaches 100k in production, PM-008's denormalization action fires first (query performance), followed by this item's horizontal-scaling decision (infrastructure capacity). | Maxx | At scale-out |
| PM-007 | `src/frontend`'s devDependency tree has security vulnerabilities reachable via `@modelcontextprotocol/sdk` → `@angular/cli` (5 findings: 4 moderate, 1 high — `@hono/node-server` path traversal, `hono` ReDoS, `fast-uri` host-confusion). Confirmed dev-only (`npm audit --omit=dev` → 0; production dependency tree clean) and unrelated to any feature's own diff. The only fix (`npm audit fix --force`) forces a breaking `@angular/cli` major-version downgrade — needs an explicit human decision on toolchain compatibility risk before it's applied. | Maxx | — |
| PM-008 | F-017's unfiltered Score/Commits sort path is bounded by match count (not page size) and measured at 4.8–4.9s at the 100k-repo / 1M-score target scale (2026-08-07) — exceeding NFR-001's 2s interactive budget by ~2.5×. The correlated-subquery sort key evaluates for every matching repository before LIMIT applies. Filtered queries and Newest/Stars sorts are all well within budget. First action when repository count approaches 100k in production: denormalize `LatestTotalScore`/`LatestCommitsPerWeek` columns on `Repository` (maintained by `ComputeScoresCommandHandler`, backfilled, indexed — ADR-worthy, touches F-007's write path; `docs/archive/v1-mvp` Architecture v29 partitioning-strategy section records this). Tied to risk A5 and PM-003. | Maxx | Before/at scale-out |

(PM-004, PM-005, PM-006 closed during v1 — see [docs/archive/v1-mvp/project-management.md](archive/v1-mvp/project-management.md) for their resolution history)

## Version History

Full history through v39 (v1 draft through MVP closeout): see [docs/archive/v1-mvp/project-management.md](archive/v1-mvp/project-management.md).

| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v40 | 2026-08-07 | **Tranche closeout**: Tranche v1 (Phase 0-5, features F-001–F-018, all Done) archived verbatim to `docs/archive/v1-mvp/project-management.md`. Phases & Milestones and Feature Backlog reset empty for Tranche v2 (next Phase starts at 6, next feature ID at F-019). Open items PM-001, PM-002, PM-003, PM-007, PM-008 carried forward as still-unresolved; PM-004/PM-005/PM-006 stay closed in the archive, not carried. `handoff.md` closed out the same way in the same pass. `prd.md`/`architecture.md` untouched — living docs, not archived. | Tranche closeout |

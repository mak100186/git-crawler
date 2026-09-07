# Idea Book: GitHub Hidden Gems Discovery Platform

> Status: a queue, not a backlog. Nothing here has been triaged — no PRD, Architecture, PMBook row
> or ADR exists for any entry until `idea-triage` has been run on it and the user has approved each
> gate. Entries are written by `idea-discovery` (`/idea-discovery`) and only on explicit instruction.
> Last updated: 2026-09-08

Each entry is a short referential pointer: the idea, its concrete hook in this codebase (an ADR, a
class, a measured number), and a link to its research note in [docs/stash/](stash/). The detail lives
in the note, never here. Entries are updated in place when an idea changes — never appended twice
with a contradictory verdict.

The queue this feeds is Tranche v2 (see [docs/project-management.md](project-management.md), whose
Feature Backlog is empty pending triage). Before proposing anything, check the PRD's Non-Goals for
what was **declined** versus merely **deferred** — the two are not the same.

---

## Open

- **Personalized discovery via user-defined interest profiles** (PRD Goal 4) — let a user weight the
  scoring signals to their own interests instead of everyone seeing the same globally-ranked list.
  Hook: the scoring engine already exposes license, commits-per-week, contributors and forks as
  independently-weighted inputs (a v1 PRD product commitment), so per-user weighting is a weighting
  layer over an existing seam, not a rewrite of `ComputeScoresCommandHandler`. Committed to Tranche
  v2 by decision on 2026-09-08 (PM-002 / PRD Q3), so this is a scoping commitment awaiting triage
  rather than an open question of whether to do it. **No stash note** — this entry came from a PMBook
  decision, not from `/idea-discovery`, so nothing has verified the scoring-seam claim against the
  code yet. Run `/idea-discovery` on it before `/idea-triage` if that verification matters.
  Related but still out of scope: recommendation ("because you liked X") and GitHub OAuth
  personalization, both of which the PRD defers on their own grounds.

## Adopted

(none yet — an idea moves here once `idea-triage` has taken it through to a PMBook row; keep the
stash link so the evidence trail survives)

## Rejected

(none yet — rejected ideas stay recorded, with the grounds, so the same idea is not re-litigated in
six months)

# ADR-011: Angular Material as the Dashboard UI Component Library

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v7)

## Context

ADR-008 established Angular as the dashboard framework but didn't specify a UI component library
for building the Discovery Feed, Hidden Gems, Trending, and Categories views, filter/sort controls,
and bookmarking UI. The operator wants a Material Design component set and asked whether it carries
a cost.

## Decision

The dashboard uses Angular Material (`@angular/material` + `@angular/cdk`) as its UI component
library.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| PrimeNG | Broader component set, but a separate design language from Material and a separate theming system to learn/maintain for a solo developer, with no PRD requirement favoring it over Material. |
| Tailwind CSS + Angular CDK (headless) | More visual control with no imposed design language, but every component (tables, menus, date pickers, etc.) has to be built by hand — more work for a solo developer than adopting a maintained component set. |

## Consequences

- No license cost — Angular Material is MIT-licensed and maintained by the Angular team itself, so
  there's no "free tier vs. paid tier" distinction to track.
- Dashboard UI is constrained to Material Design conventions; acceptable since the PRD has no
  competing visual-identity requirement.
- Adds `@angular/material` and `@angular/cdk` as dependencies, plus a Material theme configuration
  step during scaffolding (F-003).

## Related

- Architecture section: 3. Components → Web Dashboard; 7. Technology Decisions
- Supersedes: none

# Design Brief: Web Dashboard UX (Discovery Feed, Hidden Gems, Trending, Categories)

> Status: DRAFT — pending Claude Designer review
> Feature: F-018
> Date: 2026-08-02
> Architecture: docs/architecture.md (v13), §3 Web Dashboard, §5 Functional Requirements
> PRD: docs/prd.md (v4), US-3, US-6, US-7, US-8
> ADRs: ADR-008 (Angular SPA), ADR-011 (Angular Material)

## 1. Purpose and Scope

This brief specifies the information architecture, layout structure, interaction behavior, and
component choice for the Web Dashboard's four required views — Discovery Feed, Hidden Gems,
Trending, Categories (FR-009) — plus the filter/sort (FR-004) and bookmark (FR-007) interactions
that apply across them. It is written for a UI designer (referred to in this pipeline as "Claude
Designer") to turn into the actual visual/interaction design that F-011 (Web Dashboard
implementation) will be built against.

**Out of scope for this brief** (left to the Designer, or to a later feature):
- Visual system — color palette, typography, spacing tokens, iconography style. This brief names
  Material components and their states, not their look.
- Concrete API endpoint shapes/payloads — F-010 (Web API) has not been built yet. Each view's
  section below states what data it needs and what actions it triggers, not routes or JSON shapes.
- A dedicated "Bookmarks" view — FR-009 names exactly four views (Discovery Feed, Hidden Gems,
  Trending, Categories); a standalone bookmarks-management view is F-012's concern (Phase 3, "a
  dedicated bookmarks view" per its own acceptance criteria), not one of the four this brief covers.
  §6 below specifies how bookmarking surfaces *within* the four required views so FR-007's "list"
  behavior isn't left unaddressed, and flags the boundary explicitly rather than silently
  encroaching on F-012's scope.
- Free-text/semantic search — not in FR-004, and explicitly a v1 non-goal per the PRD.

## 2. Hard Constraint: Angular Material Only (ADR-011)

Every UI element specified in this brief is composed from actual `@angular/material` /
`@angular/cdk` components — no custom or non-Material widgets. Where Material doesn't offer an
out-of-the-box equivalent for something a "hidden gems" discovery dashboard would typically want
(e.g., skeleton loading placeholders, sparkline trend charts), this brief either substitutes a
Material-idiomatic alternative or flags the gap explicitly for the Designer to resolve — it does
not silently spec a bespoke component. See §7 for the consolidated gap list.

This constraint is inherited directly from ADR-011 ("Dashboard UI is constrained to Material
Design conventions; acceptable since the PRD has no competing visual-identity requirement") and is
restated here because it's the one constraint the Designer cannot trade off against visual
preference.

## 3. Global Shell and Navigation

- **App shell:** `mat-toolbar` (top bar, app title/logo) + `mat-sidenav-container` wrapping the
  routed view content. On narrow viewports the Designer decides whether the nav collapses into a
  `mat-sidenav` (CDK `BreakpointObserver`-driven) or a bottom `mat-tab-nav-bar` — both are
  Material-idiomatic; this brief doesn't fix the breakpoint behavior, only that navigation must
  never depend on a non-Material menu widget.
- **Primary navigation:** a `mat-tab-nav-bar` (or `mat-toolbar` + `mat-button` row bound to router
  links, Designer's choice) with exactly four entries, matching FR-009's required views in a fixed
  order: **Discovery Feed → Hidden Gems → Trending → Categories**. Discovery Feed is the default
  landing route.
- **Global filter/sort bar** (§5) is anchored below the primary navigation and is visible on
  Discovery Feed, Hidden Gems, and the drilled-into-category repo list (see §4.4); it does not
  apply to the Trending view's top-level trend list (trends aren't filterable/sortable by the same
  facets as repos — see §4.3).
- **Cross-view states**, used consistently everywhere a list of repositories is rendered (Discovery
  Feed, Hidden Gems, Categories drill-down):
  - *Loading:* `mat-progress-bar` (indeterminate) pinned under the filter bar during a refresh;
    `mat-progress-spinner` centered in the content area on first load.
  - *Empty (no results after filtering):* a centered `mat-card` with a `mat-icon`, a one-line
    message ("No repositories match these filters"), and a `mat-button` that clears all active
    filter chips (§5.3).
  - *Error (data fetch failed):* a centered `mat-card` with a `mat-icon`, an error message, and a
    `mat-button` labeled "Retry". Transient action failures (e.g., a bookmark write that fails) use
    `mat-snack-bar` instead of blocking the view — see §6.2.

## 4. View Specifications

### 4.1 Discovery Feed

**Purpose (US-1, US-8):** the default landing view — every repository that has cleared baseline
discovery criteria, most-recently-discovered first, so the user sees what's new without having to
ask for it.

**Layout:** a responsive grid of `mat-card` items (one card per repository), using CSS
grid/flexbox for the grid layout itself (layout is not a "widget" — the cards inside it are the
Material components). Below the grid, a `mat-paginator` — chosen over infinite scroll because
`mat-paginator` is a real Material component with page-size control and explicit page state,
where infinite scroll has no first-class Material equivalent (see §7 gap G1).

**Per-repository card content:**
- Repo name + owner/org (card title/subtitle via `mat-card-header`).
- Primary language, as a `mat-chip` (single, non-removable — informational, not a filter control
  here).
- Star count and license identifier, as plain text or small `mat-chip`s alongside the language chip.
- AI-generated summary snippet (FR-003/US-2), truncated to 2–3 lines in `mat-card-content`. Until
  F-008 ships, or for a repo whose summary hasn't been generated yet, render a "Summary pending"
  placeholder in the same slot rather than an empty card — the layout must not visually jump once
  summaries start populating.
- Discovered/last-updated date (relative, e.g., "2 days ago") in `mat-card-subtitle` or footer.
- Bookmark toggle button (§6.1) in `mat-card-actions`.
- A "View on GitHub" external link (`mat-button` with an icon) in `mat-card-actions`.

**Sort default:** discovery date, descending (newest first) — this is what makes it a "feed" as
distinct from Hidden Gems. The sort control (§5.2) lets the user override this per FR-004, but the
view's own identity is defined by its default ordering, not by a restricted signal set.

### 4.2 Hidden Gems

**Purpose (US-1, US-4):** the scored subset of the catalog — repositories that clear the
hidden-gem scoring threshold, ranked by score, so the user can trust that a high position reflects
the weighted signals (license, commits/week, contributor count, fork count, star count per
Architecture §3 Scoring Engine), not raw popularity.

**Layout:** same card-grid structure as Discovery Feed (§4.1) — reusing the card component keeps
the two views visually consistent while differing in content and default order — but each card
additionally carries:
- The computed hidden-gem score, rendered prominently (e.g., as a large numeral or a `mat-chip`
  with emphasis — visual treatment is the Designer's call, but it must be scannable at a glance
  since ranking is the point of this view).
- A collapsed **per-signal breakdown**, using `mat-expansion-panel` on each card ("Why this
  score?"). Expanding it reveals the five weighted inputs (license presence/type, commits per
  week, contributor count, fork count, star count) each as a labeled row — this directly serves
  FR-005's requirement that these be "identifiable, independently-weighted inputs," not a black-box
  number.

**Sort default:** score, descending. Same `mat-paginator` pattern as §4.1.

### 4.3 Trending

**Purpose (US-7):** aggregated technology/framework/ecosystem trend summaries (FR-008) — lets a
user see where the ecosystem is moving without reading every individual repository.

**Layout:** a vertical list of `mat-card` items, one per trend (e.g., "MCP-related repos",
"emerging Angular tooling"), ordered by growth/strength (strongest trend first) — exact ranking
logic is a Trend Aggregator (F-009) concern, this brief only specifies that the UI presents
whatever order the API returns, it does not re-sort client-side.

**Per-trend card content:**
- Trend/category label (`mat-card-title`).
- A growth indicator — since Material has no built-in sparkline/chart component, this brief flags
  it as a gap (see §7 G2) rather than speccing a bespoke chart. As a Material-only fallback, use a
  `mat-chip` stating the direction/magnitude in text (e.g., "+18% this week") until the Designer
  resolves whether a chart is worth introducing a charting library for (a decision outside this
  brief's scope — ADR-011 governs the *component* library, not whether a separate charting
  dependency is later justified).
- Repository count contributing to the trend.
- A `mat-expansion-panel` ("Repositories in this trend") that, when expanded, lists the
  contributing repos using the same repo-row content as §4.1 (name, language, stars, bookmark
  toggle) — reuses the existing card/row content spec rather than inventing a new one.

**No filter/sort bar** on the top-level trend list (§3) — trends aren't filterable by
language/star/topic/license the way repositories are. The expanded repo list within a trend card
is a fixed, ranked sub-list, not independently filterable in this view; a user who wants to
filter/sort those repos does so from Discovery Feed or Hidden Gems.

### 4.4 Categories

**Purpose (US-8):** lets a user browse the catalog by technology/topic rather than by feed order
or score, as an entry point into the same repository data.

**Layout — two levels:**
1. **Category grid:** a grid of `mat-card` tiles, one per category/topic (e.g., "Rust", "MCP
   servers", "Agent tooling"), each showing the category label, a `mat-icon`, and a repository
   count. Tiles are clickable (`mat-card` with a click handler, or wrapped in a router link) —
   Material doesn't have a distinct "tile button" component; a clickable `mat-card` is the
   idiomatic pattern here, not a gap.
2. **Category detail (drill-down):** clicking a tile navigates to a filtered repository list that
   reuses the Discovery Feed's card-grid + `mat-paginator` layout (§4.1), pre-filtered to that
   category/topic. The global filter/sort bar (§5) is shown here, with the topic filter
   pre-populated and removable like any other active filter chip — the user can narrow further
   (e.g., add a license filter) without leaving the drill-down.

**Why reuse Discovery Feed's list component:** Categories is fundamentally "Discovery Feed scoped
to one topic," not a distinct data shape — building a second repo-list layout would violate the
brief's own Material-only, no-speculative-flexibility spirit for no product benefit.

## 5. Filter and Sort Interactions (FR-004)

Applies to Discovery Feed, Hidden Gems, and the Categories drill-down (§4.4). Facets per FR-004:
language, star range, topic, license.

### 5.1 Filter controls

Housed in a filter bar/panel below the primary navigation (§3) — a `mat-expansion-panel` or a
persistent `mat-toolbar`-row, Designer's choice, as long as it doesn't hide the currently-active
filter chips (§5.3, which are always visible regardless of whether the panel is expanded).

| Facet | Control | Why this component |
|-------|---------|---------------------|
| Language | `mat-select` with `multiple` | Bounded, known set of values (languages present in the catalog) — a standard multi-select fits without needing free text. |
| Star range | `mat-slider` with dual thumb (`matSliderStartThumb`/`matSliderEndThumb` range slider) | Material's range slider is a first-class component for a bounded numeric range; a pair of `mat-form-field` number inputs (min/max) is an acceptable fallback if the Designer finds the slider's precision insufficient for the actual star-count spread — both are Material-native, this brief doesn't mandate one over the other. |
| Topic | `mat-chip-grid` (selected topics as removable chips) + `mat-autocomplete` (type-ahead to add) | Topics are a larger, less bounded set than language — autocomplete-to-add plus chip-grid-to-remove is Material's standard pattern for multi-value tag input. |
| License | `mat-select` with `multiple` | Same reasoning as language — bounded, known set (SPDX identifiers). |

### 5.2 Sort control

A single `mat-select` (or `mat-button-toggle-group` if the Designer prefers visible options over a
dropdown) offering: discovery date (newest first — Discovery Feed default), hidden-gem score
(Hidden Gems default), star count, commits per week. Paired with a direction toggle
(`mat-icon-button` flipping an ascending/descending arrow icon).

### 5.3 Active filter feedback

Every active filter (each selected language, the star range if narrowed from full, each selected
topic, each selected license) renders as a removable `mat-chip` in a `mat-chip-set` between the
filter controls and the result list — clicking a chip's remove icon clears that one filter without
reopening the control it came from. A "Clear all" `mat-button` appears in the same row when at
least one filter is active.

### 5.4 Interaction flow

1. User opens/adjusts a filter control (§5.1) → the corresponding chip(s) appear/update in §5.3
   immediately (optimistic UI — the control's own state is the source of truth for what's
   selected).
2. The result list (§4.1/§4.2/§4.4) reflects the new filter set — loading state (§3) shown while
   the (as-yet-undesigned) query is in flight, then the grid/paginator updates.
3. Changing sort (§5.2) re-orders the current filtered set the same way.
4. Filters and sort are independent of each other and composable — there is no state where setting
   one disables another.

## 6. Bookmark Interactions (FR-007)

### 6.1 Bookmark toggle (create/delete)

Every repository card, wherever it appears (Discovery Feed, Hidden Gems, a category drill-down,
and the expanded repo list inside a Trending card — §4.3), carries a bookmark toggle: a
`mat-icon-button` showing a filled or outlined bookmark/star icon depending on current state, with
a `matTooltip` ("Add bookmark" / "Remove bookmark"). This is one control, not two — toggling it
is both the create and delete action (FR-007's "create" and "delete" collapse into a single
idempotent toggle from the user's perspective).

**Flow:**
1. User clicks the toggle → icon state flips immediately (optimistic).
2. A `mat-snack-bar` confirms the action ("Added to bookmarks" / "Removed from bookmarks") with an
   "Undo" action button that reverses the toggle.
3. If the underlying write fails, the icon reverts to its prior state and the snack-bar message
   changes to an error variant ("Couldn't save bookmark — try again") — this is the one case in
   this brief where a `mat-snack-bar` communicates failure rather than confirmation, chosen over a
   blocking dialog because a failed bookmark toggle shouldn't interrupt browsing.

### 6.2 Bookmark "list" (FR-007's third verb)

FR-007 requires create, list, and delete. Delete is covered by the toggle (§6.1); create likewise.
"List" — seeing what's bookmarked — is addressed two ways within this brief's four-view scope,
without introducing the dedicated bookmarks view that belongs to F-012:
- Every card's bookmark toggle (§6.1) always reflects current bookmark state, so a user scanning
  any of the four views can see which repos they've already saved.
- A "Bookmarked only" filter toggle (a `mat-slide-toggle` or a chip-style `mat-button-toggle`,
  Designer's choice) is available alongside the filter bar (§5) on Discovery Feed and Hidden Gems,
  letting the user narrow either view to just their bookmarks — this reuses the existing
  filter/list machinery instead of building a new surface.
- A full bookmarks-management experience (e.g., bulk-remove, notes, a bookmarks-specific empty
  state and its own navigation entry) is explicitly deferred to F-012, which already commits to "a
  dedicated bookmarks view" in its own acceptance criteria. This brief does not design that view —
  doing so would be scope creep past F-018's four named layouts (AC1).

## 7. Gaps for the Designer to Resolve

Consolidated from the component choices above — cases where Material has no first-class
out-of-the-box answer, called out explicitly rather than silently specced as a custom widget:

| ID | Gap | Where it appears | Material-only fallback used in this brief |
|----|-----|-------------------|----------------------------------------------|
| G1 | No first-class infinite-scroll component | Discovery Feed, Hidden Gems, Categories drill-down (§4.1, §4.2, §4.4) | `mat-paginator` instead of infinite scroll |
| G2 | No sparkline/chart component | Trending view growth indicator (§4.3) | Text-based `mat-chip` stating direction/magnitude; Designer to decide if a separate charting dependency is worth introducing later (outside ADR-011's scope) |
| G3 | No skeleton-loader component | Cross-view loading state (§3) | `mat-progress-spinner` (first load) / `mat-progress-bar` (refresh) instead of content-shaped skeletons |

None of these gaps require a non-Material widget to ship F-011 — each has a working
Material-native fallback specified above. They're flagged so the Designer can decide, with full
visibility, whether the fallback is good enough or worth a future ADR-level conversation about
supplementing Angular Material (which ADR-011 did not rule out, only chose Material over
alternatives as the *default* library).

## 8. Handoff Note

This brief is itself the handoff artifact to Claude Designer, per F-018's acceptance criteria
(AC4). This pipeline (a documentation-authoring Task Packet with no code dependency) has no
mechanism to invoke a separate design tool or hand this to a human designer — "handed to Claude
Designer" here means: this document, once reviewed and approved, is the input a subsequent design
pass (human or agent) works from to produce the actual visual/interaction design, which is then
reviewed and approved before F-011 implementation begins (per the PMBook's F-011 dependency on
F-018). No design-tool invocation, mockup generation, or external handoff occurred as part of
producing this document — stating that explicitly here rather than implying a handoff step that
didn't happen.

## Version History
| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v1 | 2026-08-02 | Initial draft — covers Discovery Feed, Hidden Gems, Trending, Categories layouts; filter/sort (FR-004) and bookmark (FR-007) interactions; Angular Material-only constraint restated (ADR-011); three component gaps flagged (§7); handoff note clarifying document-only handoff | F-018 Task Packet |

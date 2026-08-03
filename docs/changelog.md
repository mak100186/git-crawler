# Changelog: GitHub Hidden Gems Discovery Platform

> Revision: 13
> Last updated: 2026-08-03

## Revision 13 — 2026-08-03 — Bookmarks tab decommissioned

**Changes:**
- **Bookmarks tab removed** — the dedicated `/bookmarks` view (`src/frontend/src/app/features/
  bookmarks/`, F-012's entire delta) and its live nav entry are gone, along with the backend
  `ListBookmarks` endpoint/query/tests it alone existed to serve (fully removed, unlike
  `GetCategories`, since nothing else consumed it). The primary nav is now a single "Hidden Gems"
  entry.
- **Not a capability regression**: Hidden Gems' existing "Bookmarked only" filter
  (`FilterSortBar.showBookmarkedToggle`, already `true` there) surfaces the identical set of
  bookmarked repos FR-007 requires a user be able to revisit — the dedicated view was a second,
  redundant path to the same data, not a distinct one. Bookmark create/delete (the toggle itself)
  are completely unaffected.
- **Dead plumbing removed alongside the tab**: `BookmarkApiService.listBookmarks()`,
  `RepositoryCardQuery.ToCardDto` (the bare-`RepositoryCardDto` factory `ListBookmarksQueryHandler`
  alone called — genuinely dead now, unlike the two prior removals where it stayed alive via this
  exact caller), and the component-scoped `BookmarkChangeApiService` DI-override pattern that lived
  entirely inside `bookmarks.ts`.
- **Docs updated**: `docs/architecture.md` (v18), `docs/project-management.md` (v27 — F-010/F-011/
  F-012 rows all amended in place), `docs/test-cases.md` (v13 — TC-012-01/02/03/04/06 marked Removed,
  TC-012-05 retargeted in place to Hidden Gems' filter), `docs/test-runbook.md` (F-012 section
  rewritten around the surviving filter-only scenario, F-011 nav step narrowed to one entry).
  `docs/prd.md` unchanged — FR-007 itself is still satisfied, just via a different UI path.
- **Verification**: backend 86/86 (was 89), frontend 47/47 (was 51), `npm run lint` clean. Not run
  through `orchestrator-development-pattern` — implemented directly via Claude Code at the operator's
  explicit direction, same as Revisions 9-12.

**Files changed:**
- `src/backend/GitCrawler.Api/Features/Bookmarks/ListBookmarks/` — deleted.
- `src/backend/GitCrawler.Api/Program.cs` — `MapListBookmarksEndpoint()` call and its `using` removed.
- `src/backend/tests/GitCrawler.Api.Tests/Features/Bookmarks/ListBookmarks/` — deleted.
- `src/backend/GitCrawler.Api/Features/Repositories/RepositoryCardQuery.cs` — `ToCardDto` removed
  (dead), comments corrected.
- `src/frontend/src/app/features/bookmarks/` — deleted.
- `src/frontend/src/app/app.routes.ts`, `app.ts`, `app.html`, `app.scss` — Bookmarks route/nav entry
  removed; `NAV_ENTRIES` down to a single "Hidden Gems" entry.
- `src/frontend/src/app/core/api/bookmark-api.service.ts` — `listBookmarks()` removed.
- `src/frontend/src/app/{app.spec.ts}` — updated for the single-entry nav.
- `docs/architecture.md`, `docs/project-management.md`, `docs/test-cases.md`, `docs/test-runbook.md`
  — updated per above.

---

## Revision 12 — 2026-08-03 — Repo card polish + click-to-open detail pane

**Changes:**
- **Repo card summary/footer spacing adjusted, per operator feedback**: `.repo-card__summary` now
  clamps to 3 lines instead of 2 (matching "Summary pending" min-height so the placeholder still
  never causes a layout jump when the real summary arrives), and the footer chip row's `padding-top`
  increased from 9px to 16px so it sits further below its divider line.
- **New: clicking a card opens a right-side repository detail pane** — fulfills the dashboard UX
  design brief's own §09 mockup ("card click → right-side mat-drawer over the current view · list
  keeps its scroll position"), which F-011's original implementation never built. Shows the repo's
  full untruncated AI summary, its topics as a chip list (not shown on the compact card at all), the
  same language/license/star/fork/trend chips as the card plus an "Open on GitHub" button, and — only
  when the item carries a `scoreBreakdown` (Hidden Gems, not Bookmarks) — an always-expanded
  five-signal score breakdown identical in content to the card's own "Why this score?" panel. The
  card's own interactive controls (bookmark toggle, score panel, GitHub link) each stop click
  propagation so clicking them doesn't also trigger the pane.
- **New shared `shared/utils/score-breakdown.util.ts`**: the five-signal display math (log-normalized
  progress-bar ratios against `ScoringWeights`' caps) was extracted out of `RepositoryCard` so the
  card's "Why this score?" panel and the new detail pane's score footer can't drift apart on the same
  computation — not a speculative abstraction, a real risk given the two now render the identical
  data independently.
- **Scope note**: no Functional Requirement in `docs/prd.md` ever committed to a detail pane — the
  design brief drew one anyway (F-018), and it simply hadn't been built. Implementing it now is UX
  polish drawing on already-approved design, not new product scope, so `docs/prd.md` is unchanged;
  `docs/architecture.md` (v17) and `docs/project-management.md` (v26 — F-011 row amended a fourth
  time) note it for completeness.
- **Docs updated**: `docs/architecture.md` (v17), `docs/project-management.md` (v26), `docs/test-
  cases.md` (v12 — new TC-011-14/TC-011-15), `docs/test-runbook.md` (new F-011 manual steps + spec
  count corrections).
- **Verification**: backend unaffected (frontend-only change; still 89/89). Frontend 51/51 (was 42; 6
  new: 2 `RepositoryCard` click-propagation cases, 2 new `RepositoryDetailPane` cases, 2 new
  `RepositoryGrid` drawer-wiring cases), `npm run lint` clean.

**Files changed:**
- `src/frontend/src/app/shared/utils/score-breakdown.util.ts` — new.
- `src/frontend/src/app/shared/components/repository-card/` — `cardClick` output added; avatar-era
  scoring-math duplication removed in favor of the new shared util; summary/footer CSS spacing.
- `src/frontend/src/app/shared/components/repository-detail-pane/` — new component (`.ts`/`.html`/
  `.scss`/`.spec.ts`).
- `src/frontend/src/app/shared/components/repository-grid/` — `mat-drawer-container`/`mat-drawer`
  wired around the existing grid content; `selectedItem` state; card-click → open, close/backdrop →
  close.
- `docs/architecture.md`, `docs/project-management.md`, `docs/test-cases.md`, `docs/test-runbook.md`
  — updated per above.

---

## Revision 11 — 2026-08-03 — Discovery Feed tab decommissioned

**Changes:**
- **Discovery Feed tab removed** — the standalone Discovery Feed view
  (`src/frontend/src/app/features/discovery-feed/`, the default/landing route) and its route/nav
  entry are gone, along with the backend `GetDiscoveryFeed` endpoint/query/tests it alone existed to
  serve (fully removed, unlike `GetCategories`, since `GetHiddenGems` already covers the same shared
  D4 filter/sort/paginate contract as a full superset). The default route now lands on Hidden Gems;
  nav order is now Hidden Gems → Bookmarks (two entries).
- **Not a capability regression**: once Categories and Trending had already folded into Hidden Gems
  (Revisions 9/10), Discovery Feed offered no browsing capability distinct from Hidden Gems — same
  filter/sort contract, same card grid, same layout — so nothing is lost by consolidating onto one
  view. The one visible difference (Hidden Gems' score badge/breakdown/trend chip) was already true
  before this change; Discovery Feed's own base-card variant simply had no distinct value left.
- **Repo card avatar removed, per operator feedback in the same request** ("remove the avatar", "keep
  the score card"): `RepositoryCard`'s avatar-initial circle (and its terracotta/olive/neutral
  rotation) is gone from the header — the score badge is now Hidden Gems' sole header visual anchor.
  Bookmarks cards (which carry no score breakdown) now render no header circle at all; this was not
  called out as a concern by the operator and no replacement was requested.
- **Docs updated**: `docs/prd.md` (v7), `docs/architecture.md` (v16 — including the Web API surface
  change, since `/api/discovery-feed` is fully removed unlike `/api/categories`), `docs/project-
  management.md` (v25 — F-010/F-011 rows amended in place a third time), `docs/test-cases.md` (v11),
  `docs/test-runbook.md`, `docs/handoff.md`. Two pre-existing stale mentions of the already-removed
  Trending/Categories tabs, missed by Revisions 9/10, were also fixed while in these files anyway:
  `Makefile`'s `make help`/`make up` output text, and three explanatory code comments in
  `RepositoryCardQuery.cs` that still referenced the long-gone Category drill-down.
- **Verification**: backend 89/89 (was 114), frontend 42/42 (was 47), `npm run lint` clean. Not run
  through `orchestrator-development-pattern` — implemented directly via Claude Code at the operator's
  explicit direction, same as Revisions 9/10.

**Files changed:**
- `src/backend/GitCrawler.Api/Features/Repositories/GetDiscoveryFeed/` — deleted.
- `src/backend/GitCrawler.Api/Program.cs` — `MapGetDiscoveryFeedEndpoint()` call and its `using`
  removed.
- `src/backend/tests/GitCrawler.Api.Tests/Features/Repositories/GetDiscoveryFeed/` — deleted.
- `src/backend/GitCrawler.Api/Features/Repositories/RepositoryCardQuery.cs`,
  `Features/Repositories/GetHiddenGems/GetHiddenGemsQuery.cs` — stale Discovery-Feed/Category-
  drill-down comments corrected.
- `src/backend/GitCrawler.Api/Data/Entities/Repository.cs`,
  `Features/Crawling/DiscoverRepositories/DiscoverRepositoriesCommand.cs` — comments referencing
  "Discovery Feed's Newest sort" generalized to "the dashboard's Newest sort".
- `src/frontend/src/app/features/discovery-feed/` — deleted.
- `src/frontend/src/app/app.routes.ts`, `app.ts`, `app.html` — Discovery Feed route/nav entry
  removed; default route now `hidden-gems`.
- `src/frontend/src/app/core/api/repository-api.service.ts` — `getDiscoveryFeed()` removed.
- `src/frontend/src/app/shared/components/repository-card/` — avatar markup/computed
  properties/styles removed.
- `src/frontend/src/app/{app.spec.ts,app.routes.spec.ts}` — updated for the two-entry nav and
  Hidden-Gems-default route.
- `Makefile` — stale "Discovery Feed, Hidden Gems, Trending, Categories" dashboard-description text
  (missed by Revisions 9/10) corrected to "Hidden Gems, Bookmarks".
- `docs/prd.md`, `docs/architecture.md`, `docs/project-management.md`, `docs/test-cases.md`,
  `docs/test-runbook.md`, `docs/handoff.md` — updated per above.

---

## Revision 10 — 2026-08-03 — Trending tab decommissioned, merged into Hidden Gems

**Changes:**
- **Trending tab removed, merged into Hidden Gems** — the standalone Trending view
  (`src/frontend/src/app/features/trending/`, per-category trend cards with an expandable
  contributing-repos panel) and its route/nav entry are gone, along with the backend `GetTrending`
  endpoint/query/tests it alone existed to serve (fully removed, unlike `GetCategories`, since nothing
  else consumed it). Nav order is now Discovery Feed → Hidden Gems → Bookmarks.
- **Not a capability regression**: the underlying trend data (`TrendAggregate`, computed nightly by
  F-009's `AggregateTrendsCommand`) is unchanged. Each Hidden Gems card now shows its own category's
  trend growth directly — a new `HiddenGemCardDto.TrendGrowth` field, computed server-side in
  `GetHiddenGemsQueryHandler` using the exact same current/previous-period formula the old Trending
  view computed client-side per trend card, rendered as a chip on `RepositoryCard`.
- **Scope decision confirmed with the operator**: trends are per-category, not per-repo, so "the
  trending score" on a card was ambiguous (growth chip vs. raw average score vs. both) — asked
  directly; operator chose the growth chip, reusing the old view's exact text/format.
- **Dead plumbing removed alongside the tab**: `TrendingApiService`, `trend.model.ts`, and the
  now-unused `trending-up` SVG icon.
- **Docs updated**: `docs/prd.md` (v6), `docs/architecture.md` (v15 — including the Web API surface
  change, since `/api/trending` is fully removed unlike `/api/categories`), `docs/project-management.md`
  (v24 — F-010/F-011 rows amended in place a second time), `docs/test-cases.md`, `docs/test-runbook.md`,
  `docs/handoff.md`.
- **Verification**: backend 114/114 (was 116), frontend 47/47 (was 51), `npm run lint` clean. Not run
  through `orchestrator-development-pattern` — implemented directly via Claude Code at the operator's
  explicit direction, same as Revision 9.

**Files changed:**
- `src/backend/GitCrawler.Api/Features/Trends/GetTrending/` — deleted.
- `src/backend/GitCrawler.Api/Program.cs` — `MapGetTrendingEndpoint()` call and its `using` removed.
- `src/backend/tests/GitCrawler.Api.Tests/Features/Trends/GetTrending/` — deleted.
- `src/backend/GitCrawler.Api/Features/Repositories/GetHiddenGems/GetHiddenGemsQuery.cs` — added
  `HiddenGemCardDto.TrendGrowth` + server-side growth-label computation from `TrendAggregate`.
- `src/backend/tests/.../GetHiddenGems/GetHiddenGemsQueryHandlerTests.cs` — 4 new `TrendGrowth` cases.
- `src/frontend/src/app/features/trending/` — deleted.
- `src/frontend/src/app/core/api/trending-api.service.ts`, `core/models/trend.model.ts` — deleted.
- `src/frontend/src/app/app.routes.ts`, `app.ts`, `app.html` — Trending route/nav entry removed.
- `src/frontend/src/app/core/models/repository.model.ts` — `HiddenGemCardDto.trendGrowth` added.
- `src/frontend/src/app/shared/components/repository-card/`,
  `shared/components/repository-grid/` — `trendGrowth` input wired through, chip rendered/styled.
- `src/frontend/src/app/core/icons/icon-registry.service.ts` — unused `trending-up` icon removed.
- `docs/prd.md`, `docs/architecture.md`, `docs/project-management.md`, `docs/test-cases.md`,
  `docs/test-runbook.md`, `docs/handoff.md` — updated per above.

## Revision 9 — 2026-08-03 — Categories tab decommissioned

**Changes:**
- **Categories tab removed** — the standalone Categories view (`src/frontend/src/app/features/categories/`,
  a grid of category tiles) and its Category drill-down route (`categories/:category`) are gone, along
  with the "Categories" nav entry and the backend `GetCategoryRepositories` endpoint/query/tests that
  existed only to serve the drill-down. `GetCategories` itself is unchanged — it still backs the
  Language filter's option list (`FacetOptionsService`). Nav order is now Discovery Feed → Hidden Gems
  → Trending → Bookmarks.
- **Not a capability regression**: Category is, and always has been, `Repository.PrimaryLanguage`
  (F-009 D2) — the exact value the existing Language `mat-select` filter on Discovery Feed/Hidden Gems
  already narrows by. The tab was a redundant second path to the same filter, not a distinct one.
- **Dead plumbing removed alongside the tab**: `FilterSortBar`'s `forcedCategory` input and pinned-
  chip logic (only the drill-down page used it), `RepositoryApiService.getCategoryRepositories()`,
  `buildRepositoryQueryParams`'s `omitLanguage` option, and the now-unused `layers` SVG icon.
- **Docs updated**: `docs/prd.md` (v5), `docs/architecture.md` (v14), `docs/project-management.md`
  (v23 — F-010/F-011 rows amended in place), `docs/test-cases.md`, `docs/test-runbook.md`,
  `docs/handoff.md`.
- **Verification**: backend 116/116 (was 121), frontend 51/51 (was 61), `npm run lint` clean. Not run
  through `orchestrator-development-pattern` — implemented directly via Claude Code at the operator's
  explicit direction.
- **Pre-existing, unrelated issue noted, not fixed here**: `npm run build`'s production configuration
  fails an 8kB per-component style budget on `filter-sort-bar.scss` — confirmed via `git log` to
  predate this change (last touched by the `ui fixes` commits before this session); this change never
  touched that file's `.scss`.

**Files changed:**
- `src/backend/GitCrawler.Api/Features/Categories/GetCategoryRepositories/` — deleted.
- `src/backend/GitCrawler.Api/Program.cs` — `MapGetCategoryRepositoriesEndpoint()` call and its
  `using` removed.
- `src/backend/tests/GitCrawler.Api.Tests/Features/Categories/GetCategoryRepositories/` — deleted.
- `src/frontend/src/app/features/categories/` — deleted (both `categories.*` and `category-detail/`).
- `src/frontend/src/app/app.routes.ts`, `app.ts`, `app.html` — Categories route/nav entry removed.
- `src/frontend/src/app/shared/components/filter-sort-bar/` — `forcedCategory` input and pinned-chip
  logic removed.
- `src/frontend/src/app/core/api/repository-api.service.ts`, `query-params.util.ts` — dead
  drill-down-only methods/options removed.
- `src/frontend/src/app/core/icons/icon-registry.service.ts` — unused `layers` icon removed.
- `docs/prd.md`, `docs/architecture.md`, `docs/project-management.md`, `docs/test-cases.md`,
  `docs/test-runbook.md`, `docs/handoff.md` — updated per above.

## Revision 8 — 2026-08-03 — F-012 (Bookmarking), run as a standalone slice of Phase 3; Phase 3 complete

**Changes:**
- **F-012 (Bookmarking)** — a dedicated `/bookmarks` view (`src/frontend/src/app/features/bookmarks/`)
  and a live "Bookmarks" nav entry (5th, after Categories) replacing F-011's inert "Bookmarks · F-012"
  ghost pill. Bookmark create/toggle and backend CRUD already shipped inside F-010/F-011's own scope —
  this feature's entire delta is the ability to *revisit* bookmarked repos from their own page. Lists
  results via F-010's existing `GET /api/bookmarks` (unpaginated, server-ordered most-recent-first,
  passed straight through with no client re-sort), rendered through the existing `RepositoryGrid`
  (Loading/Error/Populated states reused unmodified; a locally-rendered empty state supplies
  bookmarks-specific copy since `RepositoryGrid`'s own empty state is hardcoded to filter-oriented
  text). `BookmarkApiService.listBookmarks()` added; `addBookmark`/`removeBookmark` untouched.
- **Un-bookmarking from this view removes the card, Undo restores it — via a component-scoped DI
  override, not a shared-component change.** `BookmarkToggle`, `RepositoryCard`, and `RepositoryGrid`
  were all off-limits (existing F-011 code this feature only consumes). A private
  `BookmarkChangeApiService` inside `bookmarks.ts` delegates every call to the real
  `BookmarkApiService` via Angular's `skipSelf` injection while tapping confirmed add/remove calls
  over an internal `Subject`, scoped to this component's own injector only. Reviewer independently
  traced the injector wiring (not just the summary) before passing it.
- **Modules/files affected**: `src/frontend/src/app/features/bookmarks/` (new — `.ts`/`.html`/
  `.scss`/`.spec.ts`), `src/frontend/src/app/app.routes.ts` (new lazy `bookmarks` route),
  `src/frontend/src/app/app.ts`/`app.html`/`app.spec.ts` (live nav entry, ghost pill removed),
  `src/frontend/src/app/core/api/bookmark-api.service.ts` (`listBookmarks()` added). No backend
  changes — F-010's API was consumed, not modified.
- **Breaking changes**: none.
- **Integration round 1 FAILed on a pre-existing, unrelated `npm audit` finding** (6 moderate
  vulnerabilities in a devDependency chain via `@angular/cli`'s tooling, introduced by an earlier,
  separate commit adding Playwright/`make dev` — not by F-012, which touches no `package.json`).
  Reviewer-Integration's round-1 FAIL correctly caught two legitimately-in-scope documentation-drift
  fixes Integration had deferred instead of making, plus asked for verification of an inconsistency
  claim against F-011 that turned out, on checking git history, not to actually exist (F-011's own
  finalization predates the vulnerable dependency's introduction). Integration's retry fixed both doc
  drifts directly and carried the corrected `npm audit` finding forward as PM-007 (a human decision on
  a breaking `@angular/cli` bump, not something to force through Integration) — round 2 PASSed.
- **Documentation**: `docs/project-management.md` v22 (F-012 → Done, Phase 3 → Done, PM-007 added,
  PM-006 noted-not-resolved); `docs/test-cases.md` v7 (new F-012 section, TC-012-01 through
  TC-012-06) and v8 (TC-011-11 corrected — its "ghost pill present" assertion contradicted the new
  TC-012-02, retitled to "Reserved v2 placeholder is inert"); `docs/test-runbook.md` (new F-012
  section; existing F-011 step corrected to match the now-live nav entry); `docs/handoff.md` (this
  session).
- **Graphify**: incremental `--update` over `src`, 17 changed files (13 code + 4 Angular templates).
  Graph grew 1445→1500 nodes, 2262→2349 edges, 114→125 communities.

**Smoke tests** (see `docs/test-runbook.md` F-012 section for full steps):
1. **Happy path**: bookmark 2-3 repos from any existing view, navigate to `/bookmarks` — they render,
   most-recently-bookmarked first, matching the API's own order.
2. **Edge case**: navigate to `/bookmarks` with zero bookmarks — bookmarks-specific empty-state copy
   renders, not the generic filter-oriented "No repositories match these filters" text.
3. **Regression-sensitive**: un-bookmark a card from `/bookmarks` — it leaves the grid immediately;
   click Undo on the snack-bar — it's restored; navigate away and back to confirm the removal actually
   persisted server-side (not just a local UI flip).

---

## Revision 7 — 2026-08-02 — F-011 (Web Dashboard), run as a standalone slice of Phase 3; post-hoc `core/services` split

**Changes:**
- **F-011 (Web Dashboard)** — the four required views (Discovery Feed, Hidden Gems, Trending,
  Categories) plus the Category drill-down, implemented as standalone, Angular-Material-only
  (ADR-011) routed components under `src/frontend/src/app/features/`, backed by live `HttpClient`
  calls to F-010's endpoints. Reused shared components: `RepositoryCard` (base + hidden-gem score
  badge + expandable "Why this score?" breakdown + "Summary pending" placeholder), `RepositoryGrid`
  (loading/empty/error/populated states + `mat-paginator`, 24/page default), `FilterSortBar`
  (`mat-select multiple` for language/license, dual-thumb `mat-slider` + synced number inputs for
  star range, `mat-chip-grid`+`mat-autocomplete` for topic, `mat-button-toggle-group` for sort,
  `mat-slide-toggle` for bookmarked-only), `BookmarkToggle` (optimistic flip, snack-bar
  confirm/Undo, revert+Retry on failure) — reused on every card, including Trending's expanded
  contributing-repo rows. Category drill-down reuses Discovery Feed's exact card-grid +
  `FilterSortBar` stack rather than a second list component, with the category pinned as a
  non-removable filter chip.
- **App shell rebuilt to the approved "Ink Header" visual design** (`dashboard-handoff.md`) — ink-900
  `mat-toolbar`, terracotta active-nav pill, Caprasimo/Figtree fonts, custom Material 3 theme tokens
  (colors, pill radii, elevation, focus ring) replacing the Phase 0 scaffold's default azure/blue
  theme. CDK `BreakpointObserver` at 960px collapses the primary nav to a bottom floating pill row
  and the filter/sort bar to a "Filters · N" button opening a `mat-sidenav` with the same controls.
  Reserved, inert "Bookmarks · F-012" nav pill and "Search (v2)" field ship as disabled placeholders
  so the shell won't reflow when those land.
- **Reviewer FAILed once (round 1), fixed and re-verified PASS (round 2)**: (1) the 960px responsive
  filter-bar collapse was missing entirely — no `BreakpointObserver` usage, no "Filters · N" trigger,
  no sidenav; (2) the Categories tile grid and the mobile bottom-nav links were built from
  custom-CSS-styled `<a>` tags mimicking a card/button look instead of real `mat-card`/
  `mat-icon-button` elements — a genuine ADR-011 violation despite the component already importing
  `MatCardModule` for other states. Both fixed; the round-2 diff was verified scoped to exactly the
  files the fix claimed.
- **Integration found and fixed a genuine pre-ship runtime defect** Developer/Reviewer both missed:
  `FilterSortBar` was missing a `MatInputModule` import, so the Star-range Min/Max facet inputs would
  have thrown `mat-form-field must contain a MatFormFieldControl` in a live browser — AC2 ("filter/sort
  controls work end-to-end") was not actually true until this fix landed. Root-caused to 22 of 23
  originally-failing tests across four spec files. Also fixed four test-only defects (a
  `RouterTestingHarness` called more than once per test, a snack-bar mock that synchronously
  auto-fired an unmocked "Undo" call, an over-broad DOM selector matching a legitimately-reused BEM
  modifier class, and a test assertion that contradicted `HttpParams.getAll()`'s documented
  null-vs-empty-array behavior) — none were gamed (no test disabled/skipped/loosened).
- **Reviewer-Integration FAILed once on a reporting-accuracy issue, not a code defect**: the
  Integration Agent's Documentation Drift section quoted a specific sentence from
  `docs/project-management.md`'s F-011 row that didn't actually exist in the file yet — the
  underlying `MatInputModule` finding was true and worth recording, but the report had gotten ahead
  of the actual document edit. Fixed by making the edit for real (PMBook → v21) and correcting the
  report to match, re-verified PASS.
- **Live E2E validated**, not deferred as Manual: `dotnet publish` was run for real (Node available in
  this environment), exercising `GitCrawler.Api.csproj`'s `BuildAngularApp`/`CopyAngularApp` MSBuild
  targets; the published host served `index.html` at `/` and correctly fell back to `index.html` for
  a directly-requested client-side route (`/hidden-gems`) via `MapFallbackToFile`, confirming FR-009
  AC3 end-to-end, not just via `npm run build` in isolation.
- **Two contract gaps in F-010, pre-flagged and handled without inventing a backend endpoint**: no
  facet-options endpoint exists (`/api/languages|licenses|topics`) — language options are sourced
  from `/api/categories` (Category ≡ PrimaryLanguage), license/topic options accumulated client-side
  from repository cards already fetched that session (`FacetOptionsService`). `TrendDto` has no
  growth/period-over-period metric and `/api/trending` isn't deduplicated per category (unlike
  `/api/categories`) — the Trending growth chip computes a real average-score delta between a
  category's two most recent period rows when both exist, falling back to the current average score
  (not a fabricated percentage) when only one period exists.
- **This Orchestrator run was deliberately scoped to F-011 alone**, not the full Phase 3 (F-010,
  F-011, F-012) — F-012 remains `Planned`; Phase 3 itself stays `Planned`.
- **Post-hoc refactor, operator-directed**: graphify flagged `frontend core/services` as a
  low-cohesion (0.05) 54-node community mixing four unrelated concerns — F-010 API client wrappers,
  a query-param-building utility, a client-side facet-derivation service, and an unrelated
  Material-icon-registration bootstrap service. Split into `core/api/` (the four `*-api.service.ts`
  files + `query-params.util.ts`), `core/facets/` (`facet-options.service.ts`), and `core/icons/`
  (`icon-registry.service.ts`), with all 16 consumer files' import paths updated. Verified via a full
  rebuild/lint/test pass (57/57 still passing) before re-running graphify.
- **Documentation drift found and fixed**: `docs/test-cases.md` extended to v6 with TC-011 (12
  scenarios, authored directly by the Orchestrator per this pipeline's Step 0.0 gap-closure pattern —
  stated explicitly to both Integration and Reviewer-Integration this run to avoid the F-010 run's
  misattribution incident). `docs/test-runbook.md` extended with an F-011 section. `docs/
  project-management.md` F-011 row → `Done` (v20, corrected to v21 for the `MatInputModule` finding).

**Modules / files affected:**
- `src/frontend/src/app/app.{ts,html,scss}`, `app.routes.ts`, `app.config.ts` — real shell, routing,
  `provideHttpClient()`/`provideAnimationsAsync()`.
- `src/frontend/src/styles.scss`, `src/frontend/src/index.html` — theme tokens, Caprasimo/Figtree fonts.
- `src/frontend/proxy.conf.json` (new), `src/frontend/angular.json` — dev-server API proxy.
- `src/frontend/package.json` — `@angular/animations` added (missing peer dep from the Phase 0 scaffold).
- `src/frontend/src/app/core/models/` (new) — `bookmark`, `category`, `repository`, `trend` DTOs mirroring F-010.
- `src/frontend/src/app/core/api/` (new, post-refactor location) — `bookmark-api.service.ts`, `category-api.service.ts`, `repository-api.service.ts`, `trending-api.service.ts`, `query-params.util.ts`.
- `src/frontend/src/app/core/facets/` (new, post-refactor location) — `facet-options.service.ts`.
- `src/frontend/src/app/core/icons/` (new, post-refactor location) — `icon-registry.service.ts`.
- `src/frontend/src/app/shared/components/{repository-card,repository-grid,filter-sort-bar,bookmark-toggle}/` (new).
- `src/frontend/src/app/shared/pipes/relative-date.pipe.ts` (new).
- `src/frontend/src/app/features/{discovery-feed,hidden-gems,trending,categories,categories/category-detail}/` (new).
- `docs/project-management.md` — v21 (F-011 → Done, `MatInputModule` finding recorded).
- `docs/test-cases.md` — v6: TC-011 added.
- `docs/test-runbook.md` — F-011 section added.
- `graphify-out/` — `graph.json`/`graph.html`/`GRAPH_REPORT.md` updated (1154→1442→1445 nodes across
  two incremental passes — F-011 content then the `core/services` split; 1754→2279→2262 edges;
  78→112→114 communities); manifest re-saved scoped to the full `src` tree (backend + frontend),
  closing the scope-mismatch risk flagged in the F-010 handoff.

**Breaking changes:** None. Frontend-only; no backend contract or schema change.

**Smoke tests:**
1. Happy path — load the dashboard, confirm it lands on Discovery Feed, select a language + narrow
   the star range + add a topic + pick a license, confirm the active-filter chips appear and the grid
   re-fetches with matching query params; toggle a bookmark and confirm the optimistic flip + confirm
   snack-bar with a working Undo.
2. Edge case — resize the viewport below 960px on Discovery Feed; confirm the filter bar collapses to
   a "Filters · N" button opening a sidenav with the same controls, and the primary nav collapses to
   a bottom floating pill row.
3. Regression-sensitive — `dotnet publish` the backend with the frontend built; confirm `GET /` serves
   the dashboard and a directly-requested client route (e.g. `/hidden-gems`) falls back to
   `index.html` instead of 404ing (FR-009 AC3).

## Revision 6 — 2026-08-02 — F-010 (Web API), run as a standalone slice of Phase 3

**Changes:**
- **F-010 (Web API)** — 8 new Wolverine command/query slices (ADR-015) under
  `Features/{Repositories,Trends,Categories,Bookmarks}/`: `GetDiscoveryFeed`, `GetHiddenGems`,
  `GetTrending`, `GetCategories`, `GetCategoryRepositories`, `CreateBookmark`, `DeleteBookmark`,
  `ListBookmarks`, each with its own minimal-API endpoint dispatching via `IMessageBus.InvokeAsync`
  (matching the existing `PingEndpoint` pattern). A shared internal (non-Wolverine) helper,
  `Features/Repositories/RepositoryCardQuery.cs`, implements one filter/sort/paginate contract
  (language/star-range/topic/license facets — AND across facets, OR within a facet; sort by
  Newest/Score/Stars/Commits × Asc/Desc; pagination, default 24/page) reused by Discovery Feed,
  Hidden Gems, and Category drill-down, rather than tripling that logic across three slices.
- **Two schema gaps closed as additive migrations** (`AddRepositoryTopicsAndFirstDiscoveredAt`),
  bundled into F-010 since its own Acceptance Criteria required data the schema didn't yet capture —
  same judgment-call pattern as F-007's mid-flight star-count amendment:
  - `Repository.Topics` (`text[]` via EF Core's primitive-collections feature) — GitHub topics were
    never crawled before this feature. `GitHubDiscoveryClient.BuildDiscoveryQuery()` (F-005) extended
    to fetch `repositoryTopics(first: 10)` alongside the existing discovery query fields.
  - `Repository.FirstDiscoveredAtUtc` — set once on first insert in
    `DiscoverRepositoriesCommandHandler`, never updated on re-crawl. Drives Discovery Feed's default
    "Newest" sort; `LastCrawledAtUtc` would have been the wrong field since it advances on every
    re-crawl, which would make old, frequently-re-crawled repos look newest.
  - **Neither column is backfilled for pre-existing `Repository` rows** — `Topics` defaults to `{}`
    (self-heals on next re-crawl), `FirstDiscoveredAtUtc` defaults to `-infinity` (does not self-heal,
    set-once by design). Tracked as new Open Item PM-006.
- **Two data-model decisions carried forward from F-009 rather than reopened**: "Categories" stayed
  `TrendAggregate.Category`-derived (`Repository.PrimaryLanguage`), not GitHub-topic-derived.
  Trending's "contributing repos" are computed at query time (matching `PrimaryLanguage`, has both a
  `Score` and a `Summary`, latest `TotalScore`) rather than stored, deliberately mirroring
  `AggregateTrendsCommandHandler`'s own write-side membership criteria — `TrendAggregate` has no
  repo-level FK by design.
- **Hidden Gems exposes FR-005's full weighted signal breakdown** — all five signals plus
  `ScoringWeights`' exact constants (18/27/22.5/22.5/10%) and `TotalScore`, not just an aggregate.
  Both Hidden Gems and Discovery Feed's `Score`/`Commits` sorts use each repo's *latest* `Score` by
  `ComputedAtUtc`, following the same "latest by time, not highest-ever" convention F-008 first got
  wrong and fixed in Phase 2 — applied correctly here from the start.
- **Bookmark create/delete are idempotent by design**: a double-create never trips the unique-index
  constraint on `Bookmark.RepositoryId`; deleting a nonexistent bookmark never errors.
- **This Orchestrator run was deliberately scoped to F-010 alone**, not the full Phase 3
  (F-010, F-011, F-012) — F-011/F-012 remain `Planned`; Phase 3 itself stays `Planned`.
- **Documentation drift found and fixed**: `docs/test-runbook.md` had no F-010 section despite 8 new
  endpoints — authored one, cross-referenced to passing tests. `docs/test-cases.md` extended to v5
  with TC-010 (10 scenarios). `docs/project-management.md` F-010 row → `Done` (v18); F-018 row
  updated to record that its design-review/approval gate (completed earlier this session, prior to
  this Orchestrator run) is now satisfied (v19) — that fact previously existed only in conversation
  history, not any governed doc.
- **Reviewer-Integration process note**: initially FAILed on a misattributed `docs/test-cases.md`
  diff (assumed Integration had silently authored and hidden it; actually the Orchestrator wrote it
  directly before dispatching Integration, per this pipeline's own Step 0.0). Self-corrected to PASS
  after independently re-reading the skill's actual text. See `docs/handoff.md`'s Important Context
  for the process lesson this surfaced.
- **Live E2E gap**: no `make up` stack was available in this environment, so the 8 new endpoints were
  validated via handler tests against a real SQLite-provider `DbContext` (including the `Topics`
  array-overlap query translation), not a live HTTP walkthrough. See `docs/handoff.md`'s What's Next.

**Modules / files affected:**
- `src/backend/GitCrawler.Api/Data/Entities/Repository.cs` — `Topics`, `FirstDiscoveredAtUtc` added.
- `src/backend/GitCrawler.Api/Data/Migrations/20260802054513_AddRepositoryTopicsAndFirstDiscoveredAt.*`, `GitCrawlerDbContextModelSnapshot.cs` — new migration.
- `src/backend/GitCrawler.Api/Features/Crawling/DiscoverRepositories/{IGitHubDiscoveryClient,GitHubDiscoveryClient,DiscoverRepositoriesCommand}.cs` — extended for `Topics`/`FirstDiscoveredAtUtc`.
- `src/backend/GitCrawler.Api/Features/Repositories/` — new: `RepositoryCardQuery.cs`, `GetDiscoveryFeed/`, `GetHiddenGems/`.
- `src/backend/GitCrawler.Api/Features/Trends/GetTrending/` — new.
- `src/backend/GitCrawler.Api/Features/Categories/` — new: `GetCategories/`, `GetCategoryRepositories/`.
- `src/backend/GitCrawler.Api/Features/Bookmarks/` — new: `CreateBookmark/`, `DeleteBookmark/`, `ListBookmarks/`.
- `src/backend/GitCrawler.Api/Program.cs` — 8 new `Map*Endpoint()` registrations.
- `src/backend/tests/GitCrawler.Api.Tests/Features/{Repositories,Trends,Categories,Bookmarks}/` — new test suites; `Crawling/DiscoverRepositories` tests/Fakes updated for the new `DiscoveredRepository` field.
- `docs/project-management.md` — v19 (F-010 → Done; F-018 row updated for completed design review; new PM-006).
- `docs/test-cases.md` — v5: TC-010 added.
- `docs/test-runbook.md` — F-010 section added.
- `graphify-out/` — `graph.json`/`graph.html`/`GRAPH_REPORT.md` updated (860→1154 nodes, 1223→1754 edges, 55→78 communities); scan scope corrected to `src/backend` only after a false-positive "17 deleted files" report against still-present `src/frontend` files (scope mismatch with an earlier wider-scoped manifest, not an actual deletion).

**Breaking changes:** None. The two new `Repository` columns are additive; existing rows get safe
(if imperfect — see PM-006) defaults rather than failing the migration.

**Smoke tests:**
1. Happy path — call the Discovery Feed endpoint with a combination of language/star-range/topic/
   license filters and `sort=Newest&direction=Desc`; confirm results match all facets (AND across,
   OR within) and are ordered by `FirstDiscoveredAtUtc` descending.
2. Edge case — bookmark the same repository twice in a row via `CreateBookmark`; confirm no
   constraint-violation error on the second call, and `ListBookmarks` shows it exactly once.
3. Regression-sensitive — seed a repository with two `Score` rows where the chronologically later
   one has a *lower* `TotalScore`/`CommitsPerWeek` than an earlier one; confirm Hidden Gems' `Score`
   sort and Discovery Feed's `Commits` sort both use the later (lower) value, not the historical peak.

## Revision 5 — 2026-08-02 — Phase 2 complete: AI summarization, trend aggregation, dashboard UX brief

**Changes:**
- **F-008 (Summarizer)** — New `Features/Summarization/GenerateSummaries/` slice. Selects repos with
  a latest `Score.TotalScore ≥ Summarization:MinimumScore` (default 40) and no existing `Summary`
  row, capped at `Summarization:BatchSize` (default 20) per run. Fetches each repo's README via
  GitHub REST (`GET /repos/{owner}/{repo}/readme`, 404 handled gracefully, no bulk cloning) and
  calls `IRepositorySummarizer` — implemented by `LmStudioRepositorySummarizer` against LM Studio's
  OpenAI-compatible `/v1/chat/completions` endpoint (Llama 3.2 3B Instruct, ADR-017) at
  `max_tokens: 300`. Per-repo failures (README or LM Studio) are logged and skipped, not
  batch-aborting; no Polly pipeline (unlike ADR-018's Crawler pipeline — LM Studio's local API has
  no rate-limit signal to retry against). `ComputeScoresJob` now attaches `GenerateSummariesJob` as
  Hangfire chain link 3 via a new `ISummarizationContinuationLink` seam.
  - **Reviewer-caught bug, fixed same round**: initial repo-selection logic used
    `Scores.Max(s => s.TotalScore)` — the highest score a repo *ever* recorded — instead of its
    chronologically latest score. Since `Summary` rows are create-once, this could permanently
    summarize a repo off a historical peak it has since fallen below. Fixed to
    `Scores.OrderByDescending(s => s.ComputedAtUtc).First().TotalScore`, matching
    `ComputeScoresCommandHandler`'s own established "latest by time" convention; a regression test
    now covers a repo whose chronologically-latest score is lower than an earlier one.
- **F-009 (Trend Aggregator)** — New `Features/Trends/AggregateTrends/` slice. Rolls up repos with
  both a `Score` and a `Summary` into per-category (`Repository.PrimaryLanguage`, null excluded)
  `TrendAggregate` rows, using each repo's latest `TotalScore`. Single-day period by default
  (`Trends:PeriodDays`, default 1). Persistence is upsert-by-`(Category, PeriodStart, PeriodEnd)` —
  a third distinct persistence pattern in this codebase (alongside `Score`'s append-history and
  `Summary`'s create-once), required for NFR-003 idempotency on re-run. `GenerateSummariesJob` now
  attaches `AggregateTrendsJob` as chain link 4 via a new `ITrendsContinuationLink` seam, completing
  the full pipeline: Crawler → Scoring → Summarizer → Trend Aggregator.
- **F-018 (Dashboard UX design brief)** — New `docs/design-briefs/dashboard-ux-brief.md`. Specifies
  the Discovery Feed, Hidden Gems, Trending, and Categories layouts; FR-004 filter/sort and FR-007
  bookmark interactions (bookmark "list" resolved as a filter toggle within the four required views,
  not a fifth view — that's F-012's scope); an explicit Angular Material-only constraint (ADR-011)
  with three genuine component gaps (infinite scroll, trend sparkline, skeleton loader) flagged with
  Material-native fallbacks. No code changed. The brief document is the handoff artifact — an actual
  design pass and its review/approval remain a follow-up step outside this feature's scope, still
  gating F-011.
- **Documentation drift found and fixed**: `docs/project-management.md`'s Phase 2 row was still
  `Planned` despite F-008/F-009/F-018 all being `Done` — corrected (v17). `docs/test-cases.md`
  extended to v4 (TC-008: 7 scenarios + 1 Manual; TC-009: 7 scenarios; TC-018: 3 scenarios).
  `docs/test-runbook.md` extended with F-008/F-009 sections.
- **Documentation drift found, not fixed (carried over)**: `docs/diagrams/mmd/daily-discovery-flow.mmd`
  still doesn't show the Summarizer/Trend Aggregator links (already stale before this phase; now more
  so). `docs/architecture.md`'s Version History has a duplicate/out-of-order `v12` row, pre-existing
  from this session's earlier Polly work, unrelated to Phase 2 — numbering fix needs original intent,
  not guessed at.
- **Pre-Phase-2 database check (operator request)**: live discovery data (1,002 repos) found to
  consist entirely of very-high-star repos (18.7K-453K stars) due to GitHub GraphQL search's
  best-match-only ranking (no explicit sort) combined with its ~1,000-result cap — not a "hidden
  gems" distribution. Operator reviewed and explicitly decided to leave discovery ranking as-is for
  now; not a Phase 2 code change, noted here for continuity.
- **Live E2E gap**: LM Studio's local server could not be started in the Integration Agent's
  environment this session — the real F-008→F-009 chain (live README fetch + live LM Studio
  inference + live trend rollup) was not exercised end-to-end, only via SQLite-backed unit tests.
  Recorded as TC-008-08 (Manual). See `docs/handoff.md`'s What's Next.

**Modules / files affected:**
- `src/backend/GitCrawler.Api/Features/Summarization/GenerateSummaries/` — new: `GenerateSummariesCommand.cs`, `IRepositorySummarizer.cs`, `LmStudioRepositorySummarizer.cs`, `GenerateSummariesJob.cs`.
- `src/backend/GitCrawler.Api/Features/Trends/AggregateTrends/` — new: `AggregateTrendsCommand.cs`, `AggregateTrendsJob.cs`.
- `src/backend/GitCrawler.Api/Features/Scoring/ComputeScores/ComputeScoresJob.cs` — chain link 3 attachment (`ISummarizationContinuationLink`).
- `src/backend/GitCrawler.Api/Program.cs` — LM Studio named `HttpClient`, `IRepositorySummarizer`, `GenerateSummariesJob`/`AggregateTrendsJob`, both continuation-link registrations.
- `src/backend/GitCrawler.Api/appsettings.json` — new `Summarization` and `Trends` sections, `LmStudio:MaxTokens`.
- `src/backend/tests/GitCrawler.Api.Tests/Features/Summarization/GenerateSummaries/`, `.../Trends/AggregateTrends/` — new test suites; ripple updates to `Scoring/ComputeScores` and `Crawling/DiscoverRepositories` test fakes for the new job constructor shapes.
- `docs/design-briefs/dashboard-ux-brief.md` — new.
- `docs/project-management.md` — v17: F-008/F-009/F-018 → Done, Phase 2 → Done.
- `docs/test-cases.md` — v4: TC-008, TC-009, TC-018 added.
- `docs/test-runbook.md` — F-008/F-009 sections added.
- `graphify-out/` — `graph.json`/`graph.html`/`GRAPH_REPORT.md` updated (518→860 nodes, 674→1223 edges, 48→55 communities); three ghost nodes from Revision 4's deletions (`RetryDelay.cs`, `HangfireDashboardAuthorizationFilter.cs` + test) pruned after an initial incremental-update path-matching miss.

**Smoke tests:**
1. Happy path — trigger the full chain (`discover-repositories` job) against a database with scored,
   summarized repos due for a trend rollup; confirm all four Hangfire chain links fire in sequence
   and a `TrendAggregate` row appears for at least one category.
2. Edge case — re-run `AggregateTrendsCommand` twice for the same day without an intervening crawl;
   confirm `TrendAggregate` row count for that period doesn't grow (upsert, not duplicate).
3. Regression-sensitive — seed a repo with two `Score` rows where the chronologically later one has
   a *lower* `TotalScore` than an earlier one; confirm both `GenerateSummariesCommand` and
   `AggregateTrendsCommand` use the later (lower) value, not the historical peak.

## Revision 4 — 2026-08-02 — Crawler retry/resilience migrated to Polly; fixed a query-building crash and a permanent-403 misclassification

**Changes:**
- **F-005** — Fixed a `NullReferenceException` in `GitHubDiscoveryClient.BuildDiscoveryQuery` that
  crashed every discovery-page fetch. The GraphQL query's `DefaultBranchRef.Name` ternary fell back
  to `string.Empty` (a static-member `MemberExpression` with a null `.Expression`), which
  Octokit.GraphQL's internal `QueryBuilder.VisitMember`/`ExpressionWasRewritten` can't handle when
  visiting it inside a union `Switch<T>()` case — every other ternary in that method already used a
  `null` literal (a `ConstantExpression`), which doesn't hit this path. Changed to `""`. Verified
  live: the discovery query now succeeds and the pipeline reaches real GitHub repos.
- **F-005/ADR-018** — Live-verifying the fix above surfaced a second, real issue: fetching the
  contributor count for `torvalds/linux` returns a permanent GitHub 403 ("history/contributor list
  too large to list contributors via the API"), which the handler's catch-all retry loop treated as
  transient — retrying it on the same schedule as a real transient failure, then aborting the whole
  crawl run once retries were exhausted (dropping every repo queued after it in that page, not just
  the one that could never succeed).
- Root-caused and fixed together with a broader change: `DiscoverRepositoriesCommandHandler`'s
  hand-rolled `while`/`try`/`catch` retry loops (rate-limit wait-until-reset, secondary-limit
  wait-exact-Retry-After, generic exponential backoff) replaced with a Polly `ResiliencePipeline` of
  two chained retry strategies — see ADR-018 for the full rationale. The generic-transient pathway
  is now 2 retries with a flat 1-minute gap (was 5 retries, exponential 60s→30min). A new
  `GitHubContributorListUnavailableException` represents the permanent 403 case specifically; it
  matches neither pathway's `ShouldHandle`, so it's never retried — the handler catches it at the
  contributor-count call site, logs a warning, and marks that repo's contributor count unavailable
  for this run (still stamping `ContributorCountFetchedAtUtc` so the existing 7-day freshness window
  keeps it from re-attempting the same permanently-blocked repo every crawl cycle) instead of
  aborting the run.
- Live-verified end-to-end against the running `make up` stack: rebuilt the `app` image, manually
  triggered the `discover-repositories` Hangfire job via its dashboard endpoint, and confirmed the
  fixed query succeeds, real repos are upserted, and `torvalds/linux`'s permanent 403 is skipped
  without retry rather than stalling or aborting the run.

**Modules / files affected:**
- `src/backend/GitCrawler.Api/Features/Crawling/DiscoverRepositories/GitHubDiscoveryClient.cs` —
  `string.Empty` → `""` in `BuildDiscoveryQuery`; `GetContributorCountAsync` now detects the
  permanent "too large" 403 by response-body message and throws
  `GitHubContributorListUnavailableException` instead of falling through to a generic
  `HttpRequestException`.
- `src/backend/GitCrawler.Api/Features/Crawling/DiscoverRepositories/IGitHubDiscoveryClient.cs` —
  new `GitHubContributorListUnavailableException` (not a `GitHubRateLimitException` subtype —
  permanent, not rate-limited).
- `src/backend/GitCrawler.Api/Features/Crawling/DiscoverRepositories/DiscoverRepositoriesCommand.cs`
  — retry loops replaced by a chained Polly `ResiliencePipeline`; contributor-count call site now
  catches `GitHubContributorListUnavailableException` specifically.
- `src/backend/GitCrawler.Api/Features/Crawling/DiscoverRepositories/RetryDelay.cs` — deleted
  (`IRetryDelay`/`TaskDelayRetryDelay`, no longer needed now that Polly owns the retry delay).
- `src/backend/GitCrawler.Api/Program.cs` — `IRetryDelay` DI registration removed.
- `src/backend/GitCrawler.Api/GitCrawler.Api.csproj` — `Polly.Core` added as a direct
  `PackageReference` (was transitive-only).
- `src/backend/tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/Fakes.cs` —
  `FakeRetryDelay` deleted; `FakeTimeProvider` now also overrides `CreateTimer` to record Polly's
  requested retry delays and fire them near-instantly.
- `src/backend/tests/GitCrawler.Api.Tests/Features/Crawling/DiscoverRepositories/DiscoverRepositoriesCommandHandlerTests.cs`
  — updated for the new 2-retry/1-minute-gap generic pathway; new test for the permanent-403 skip
  path.
- `docs/adr/ADR-018-polly-resilience-for-github-crawler.md` — new.
- `docs/architecture.md` — v12: Technology Decisions row for Polly; version history.

## Revision 3 — 2026-08-02 — Hangfire dashboard: access control removed

**Changes:**
- **F-006** — `HangfireDashboardAuthorizationFilter` (Revision 2's fail-closed shared-secret
  `?key=` query-string filter) removed entirely. Hangfire applies whatever
  `IDashboardAuthorizationFilter` is configured to every request under `/hangfire`, not just the
  page itself — including the dashboard's own bundled CSS/JS assets and its live stats-polling
  XHR, none of which carry the page's `?key=` query string forward (relative URLs, and the
  dashboard's own polling requests, don't inherit it). That left the dashboard reachable but
  unstyled, then still erroring on stats refresh once the CSS/JS gap was patched with a
  static-asset allowlist. Rather than keep special-casing more exempted paths, removed the filter
  entirely: `/hangfire` is now unauthenticated, matching the fact that no auth system exists
  anywhere else in this single-operator v1. Operator's own network boundary (don't publish the
  port beyond localhost/a trusted network) is the access control now, not an in-app filter.
  **Second fix, same revision:** removing the custom filter still left the dashboard 401ing after
  a rebuild — `DashboardOptions.Authorization` defaults to a `LocalRequestsOnlyAuthorizationFilter`
  when left unset, and Docker Desktop's port-publishing proxy doesn't preserve `127.0.0.1` as the
  apparent remote address for a host-browser request through it (the same fact ADR-009's
  Consequences already noted about the loopback check that was never used). Fixed by passing
  `Authorization = []` explicitly — an empty filter list means no filter ever runs, so every
  request is authorized. Live-verified against the actual `make up` stack (`curl` 401 before, 200
  after).

**Modules / files affected:**
- `src/backend/GitCrawler.Api/Features/Diagnostics/HangfireDashboardAuthorizationFilter.cs` —
  deleted.
- `src/backend/tests/GitCrawler.Api.Tests/Features/Diagnostics/HangfireDashboardAuthorizationFilterTests.cs`
  — deleted.
- `src/backend/GitCrawler.Api/Program.cs` — `UseHangfireDashboard` now passes
  `new DashboardOptions { Authorization = [] }` explicitly (not just an omitted call — see above);
  `HANGFIRE_DASHBOARD_KEY` config bridge removed.
- `src/backend/GitCrawler.Api/appsettings.json` — `Hangfire:DashboardAccessKey` key removed.
- `docker-compose.yml`, `.env.example` — `HANGFIRE_DASHBOARD_KEY` removed.
- `docs/adr/ADR-009-hangfire-job-scheduling.md` — Decision and Consequences updated to record the
  filter was tried and reverted, and why.
- `docs/project-management.md` (v16) — F-006's AC updated; new Revision History row.
- `docs/handoff.md`, `docs/test-runbook.md`, `docs/test-cases.md` — dashboard-reachability steps
  updated to drop the `?key=` requirement; the access-denied assertion removed since there's no
  longer any access control to assert.

**Breaking changes:** None (dashboard access got easier, not harder — no consumer depended on the
key).

## Revision 2 — 2026-08-02 — Phase 1 complete: core data pipeline

**Features shipped:**
- **F-004** — Data Store schema (EF Core). `GitCrawlerDbContext` with five entities (`Repository`,
  `Score`, `Summary`, `TrendAggregate`, `Bookmark`), three migrations to date (`InitialCreate`,
  `AddCrawlerRawSignalFields`, `AddScoreStarCountSignal`). Hangfire's own job-storage tables are
  created separately by `UsePostgreSqlStorage` (its own `hangfire` schema, not EF-migrated) —
  documented on the DbContext so F-006 didn't duplicate schema setup.
- **F-005** — GitHub Crawler. `Features/Crawling/DiscoverRepositories/` — GraphQL-first discovery
  (`Octokit.GraphQL`) with a REST fallback (typed `HttpClient`) for contributor count; idempotent
  upsert by `Repository.GitHubId`. Implements the F-001 spike's §6 back-off strategy (GraphQL
  `RATE_LIMITED`/`resetAt`, REST `x-ratelimit-*`/`Retry-After`, generic exponential backoff
  otherwise) and §7 mitigation (7-day contributor-count caching cadence) for real, not just in
  documentation.
- **F-006** — Job Scheduler (Hangfire). `AddHangfire`/`UsePostgreSqlStorage`/`AddHangfireServer`
  wired into `Program.cs`; dashboard at `/hangfire` behind a fail-closed shared-secret filter
  (`Hangfire:DashboardAccessKey`/`HANGFIRE_DASHBOARD_KEY` — no auth system exists elsewhere in this
  single-operator v1). One recurring job (`discover-repositories`, daily by default via
  `Hangfire:CrawlerCronSchedule`) triggers the Crawler.
- **F-007** — Scoring Engine. `Features/Scoring/ComputeScores/` — pure computation (no external
  calls), five independently-weighted signals (license 18%, commits-per-week 27%, contributor
  count 22.5%, fork count 22.5%, star count 10% — star count added mid-flight per operator
  direction, weighted secondary to the PRD-committed four). Completes the pipeline chain F-006 left
  open: `DiscoverRepositoriesJob` now attaches `ComputeScoresJob` via Hangfire `ContinueJobWith`
  after each crawl.
- **Operator-directed infra change**: PostgreSQL's Compose volume switched from a named Docker
  volume to a bind mount at `./data/postgres`, so the database persists as visible host files
  across `docker compose down` (not just `-v`-survivable, actually inspectable/backup-able).

**Modules / files affected:**
- `src/backend/GitCrawler.Api/Data/` — new (`GitCrawlerDbContext`, 5 entities, 3 migrations).
- `src/backend/GitCrawler.Api/Features/Crawling/DiscoverRepositories/` — new (command/handler,
  `IGitHubDiscoveryClient`/`GitHubDiscoveryClient`, `RetryDelay`, `DiscoverRepositoriesJob`).
- `src/backend/GitCrawler.Api/Features/Scoring/ComputeScores/` — new (command/handler,
  `ScoringWeights`, `ComputeScoresJob`).
- `src/backend/GitCrawler.Api/Features/Diagnostics/HangfireDashboardAuthorizationFilter.cs` — new.
- `src/backend/GitCrawler.Api/Program.cs` — Hangfire wiring, EF Core DbContext registration +
  startup `Database.Migrate()`, new config bridges (`HANGFIRE_DASHBOARD_KEY`).
- `src/backend/GitCrawler.Api/appsettings.json` — new keys: `Hangfire:CrawlerCronSchedule`,
  `Hangfire:DashboardAccessKey`, `GitHub:DiscoveryPageSize`/`DiscoveryLookbackDays`/
  `DiscoveryMinimumStars`.
- `docker-compose.yml`, `.env.example`, `.gitignore` — Postgres bind-mount; `HANGFIRE_DASHBOARD_KEY`
  plumbed through to the container.
- `docs/test-cases.md` (v2) — TC-004 through TC-007 added, filling a gap where Phase 1 had shipped
  without corresponding test-case scenarios.
- 43 new xUnit tests across `src/backend/tests/GitCrawler.Api.Tests/Data/` and
  `Features/{Crawling,Scoring,Diagnostics}/` (up from 1 smoke test at Phase 0 close).

**Breaking changes:** None.

**Known gaps / follow-ups:**
- Live verification of three scenarios was not possible in the Integration Agent's environment
  (Docker unavailable there): a real migration run against a fresh PostgreSQL 18.4 instance, live
  Hangfire dashboard reachability, and a mid-run container-restart persistence check. Automated
  test coverage exists for the underlying logic in each case (see `docs/test-cases.md` TC-004-01/
  TC-004-02/TC-006-01/TC-006-03) — the live-infrastructure half is a residual gap for the operator
  to close with a real `make up` run before relying on this in production.
- `docs/diagrams/mmd/daily-discovery-flow.mmd` is now stale: it depicts the Scheduler triggering
  Scoring independently/in-parallel with the Crawler, but the actual (and intended) design is a
  single `RecurringJob` (Crawler only) chaining into Scoring via `ContinueJobWith` — flagged by
  Integration, needs a manual diagramming pass.
- The frontend `npm audit` finding from Phase 0 (6 moderate, dev-only) remains open, unchanged this
  phase.

**Smoke tests (see `docs/test-runbook.md` for full steps):**
1. **Happy path:** `make up`, then trigger the `discover-repositories` Hangfire job (dashboard or
   its daily schedule) against a `GITHUB_TOKEN`-configured environment — expect new `Repository`
   rows, followed automatically by a chained `ComputeScoresJob` run producing `Score` rows with all
   five signals populated.
2. **Edge case:** re-run discovery against already-known repositories — expect updates in place
   (no duplicate rows), and contributor-count REST calls skipped for repos fetched within the last
   7 days.
3. **Regression-sensitive:** restart the `app` container mid-crawl — expect Hangfire's
   PostgreSQL-backed job state to survive the restart with no duplicate or dropped work, per
   F-005's idempotent upsert design.

## Revision 1 — 2026-08-01 — Phase 0 complete

**Features shipped:**
- **F-001** — Spike: GitHub GraphQL rate-limit budget validation. Output-only (no code):
  `docs/spikes/f-001-github-graphql-rate-limit-budget.md`. Verdict: risk A1 resolved for
  1K-5K repos/day; conditionally resolved (mitigation needed) at the 100k+ scale-out target,
  where the REST contributor-count fallback (not the GraphQL discovery query) is the binding
  constraint.
- **F-002** — Spike: LM Studio inference throughput benchmark. Output-only (no code):
  `docs/spikes/f-002-lm-studio-throughput-benchmark.md`. Model identifier confirmed live
  (`google/gemma-4-e4b`) and the throughput benchmark itself executed live —
  **2.57-2.82s p95 per repo across three README sizes, Pass vs. NFR-001 with ~10x headroom**. But
  the live run also found `google/gemma-4-e4b` spends 65-86% of a 300-token output budget on
  internal reasoning before the visible summary, truncating it. A live comparison against 4
  already-downloaded alternatives (spike §10) led to a **final model swap: Llama 3.2 3B Instruct**
  (ADR-017, supersedes ADR-013) — faster (0.78-1.05s mean), zero reasoning waste, complete
  natural-stop output. Risk A2 resolved (Architecture §8) for the adopted model.
- **F-003** — Project scaffolding & `make up` skeleton (amended post-scaffold, see below). First
  application code in the repository.

**Modules / files affected:**
- `src/backend/` — new .NET 10 solution (`GitCrawler.sln`), `GitCrawler.Api` project (Wolverine,
  EF Core, Hangfire, Npgsql, Octokit.GraphQL prerelease, `DotNetEnv` 3.2.0 — new, added post-scaffold),
  vertical-slice example at `Features/Diagnostics/Ping/`, `tests/GitCrawler.Api.Tests/` (xUnit
  smoke test harness). `Program.cs` now loads `.env` (via `DotNetEnv`, walking up from the project
  directory) and bridges every flat `.env` name the app reads to its hierarchical config key:
  `GITHUB_TOKEN` → `GitHub:Token`, `LMSTUDIO_PORT` → `LmStudio:BaseUrl`, `LMSTUDIO_IDENTIFIER` →
  `LmStudio:Model`, and `POSTGRES_DB`/`POSTGRES_USER`/`POSTGRES_PASSWORD`/`POSTGRES_PORT` →
  `ConnectionStrings:Postgres` — so `dotnet run` outside Docker reads the same `.env` Docker
  Compose already does. All bridges live-verified both ways (bare `dotnet run` and `make up`) this
  session. The Postgres bridge deliberately carries no `"gitcrawler"`/`"5432"` fallback literals of
  its own (single-source-of-truth pass, same session) — it only fires when all four
  `POSTGRES_DB`/`USER`/`PASSWORD`/`PORT` vars are present, relying on `.env.example` as the sole
  place those defaults are defined. `ConnectionStrings:Postgres` is wired through but not yet
  consumed — no DbContext exists until F-004.
- `src/frontend/` — new Angular 22.1.0 workspace ("dashboard"), standalone components, Angular
  Material + CDK themed, Vitest test harness, angular-eslint wired.
- `docker-compose.yml` (repo root) — **`app` and `postgres:18.4` (pinned) only.** LM Studio is
  **not** a Compose service (ADR-016, amended post-scaffold) — it runs host-installed, since the
  operator already has it installed and running natively; containerizing a second copy would have
  been CPU-only and duplicative. Postgres `DB`/`USER`/`PASSWORD` and the app's `ConnectionStrings__Postgres`
  interpolation now both read from `.env` (`POSTGRES_DB`/`POSTGRES_USER`/`POSTGRES_PASSWORD`);
  Postgres's port is now published to the host (`POSTGRES_PORT`) for local DB clients and bare
  `dotnet run`. `LmStudio__Model` is now also set (from `LMSTUDIO_IDENTIFIER`) — previously only
  `LmStudio__BaseUrl` was. **Single-source-of-truth pass (same session):** every `${VAR:-default}`
  fallback that re-hardcoded a literal `.env.example` already defaults (`POSTGRES_DB`/`USER`/`PORT`,
  `LMSTUDIO_PORT`/`LMSTUDIO_IDENTIFIER`, including inside the Postgres healthcheck's `pg_isready`
  command) was replaced with `${VAR:?...}` — required, fails loudly pointing back at `.env.example`,
  same pattern `POSTGRES_PASSWORD`/`GITHUB_TOKEN` already used. `.env.example` is now the only
  place any of these defaults are spelled out.
- `Makefile` (new, repo root) — `make up`/`down`/`status`/`logs` single entrypoint: checks Docker,
  brings up Compose, checks/starts the host LM Studio server, loads the configured model
  (default `llama-3.2-3b-instruct`, ADR-017) via the `lms` CLI. Sources `.env` automatically
  (`include .env` + `export`) so values set there actually take effect here, not just in
  `docker-compose.yml`'s own separate `.env` handling. Live-verified end-to-end against the actual
  operator machine (Docker Desktop start, LM Studio detection, model load/unload all confirmed
  working, not just written) for both the original and final model pick. **Single-source-of-truth
  pass (same session):** removed the `?=` fallback defaults for `LMSTUDIO_PORT`/`LMSTUDIO_IDENTIFIER`/
  `LMSTUDIO_MODEL` (previously duplicating `.env.example`'s literals) and added a new `check-env`
  target (prerequisite of `up`/`check-lmstudio`/`load-model`) that fails fast with a clear message
  — pointing back at `.env.example` — if `.env` or any required variable is missing, instead of
  silently limping along on a guessed default. Verified both failure modes live (missing `.env`
  entirely, and a single missing variable within an otherwise-complete `.env`) before re-confirming
  the full happy path with another live `make up`/`make down` cycle. **PowerShell/cmd.exe
  compatibility fix (same session, discovered when the operator ran `make up` from a real
  PowerShell window and hit `'test' is not recognized as an internal or external command`):** GNU
  Make on Windows picks its recipe shell by searching the invoking process's `PATH` for `sh.exe` —
  that search only succeeds from a Git Bash session (which adds Git's own bin dirs to `PATH` on
  launch), not from a plain PowerShell/cmd.exe window, where it silently falls back to `cmd.exe`,
  which can't parse these recipes' Unix syntax. Fixed by forcing `SHELL` to Git for Windows'
  bundled `bash.exe` directly on Windows (`ifeq ($(OS),Windows_NT)`), unconditionally — deliberately
  no path-exists probe, since every shell-syntax-based probe attempted (`if exist`, a `where`-based
  one) broke on one side or the other of the cmd/sh divide, the probe itself needing to be written
  in the syntax of whichever shell is currently in effect, which is exactly what's unknown at that
  point. Reproduced the exact failure live via a `cmd.exe` subprocess launched with a minimal,
  Git-`bin`-free `PATH` matching the operator's actual persistent Windows `PATH` (confirmed via
  `[Environment]::GetEnvironmentVariable('PATH','Machine'/'User')`), confirmed the fix resolves it,
  then re-ran the full `make up`/`make down` cycle normally to confirm no regression. Also found and
  restored `.env`'s `POSTGRES_PASSWORD` (blanked at some point during this session's `check-env`
  failure-mode testing) back to its known-good value. **New `make health` target (same session,
  operator request):** unlike `make status` (which only reports whether the underlying
  processes/containers are running), `health` actually probes each component's own endpoint - app
  `/health`, app `/api/ping` (proving the Wolverine command bus round-trips, not just that the
  process is up), Postgres via `docker compose exec postgres pg_isready`, and LM Studio's
  `/v1/models` - printing every result (not stopping at the first failure) and exiting non-zero if
  anything failed, so it doubles as a script/CI gate. Live-verified both outcomes: ran it against
  the fully-up stack (all four OK), then against a torn-down one (`make down` - app/Postgres FAIL,
  LM Studio correctly still OK since `make down` deliberately leaves it running on the host), then
  restored the stack to running.
- `Dockerfile` (3-stage: Angular build → .NET publish → aspnet runtime), `.dockerignore`,
  `.env.example` (now also documents `LMSTUDIO_MODEL` and how to create/configure a GitHub PAT).
- `docs/setup.md` (new) — one-time local setup: prerequisites, GitHub PAT creation (fine-grained
  recommended, classic fallback), `.env` configuration, `make up` walkthrough.
- `docs/adr/ADR-016-lm-studio-host-installed-not-containerized.md` (new) — amends ADR-002 and
  ADR-007's deployment-topology framing; does not change the LM Studio engine choice itself.
- `docs/adr/ADR-017-llama-3.2-3b-instruct-summarization-model.md` (new) — supersedes ADR-013
  (now marked `SUPERSEDED BY ADR-017`); full model comparison and decision record.
- `CLAUDE.md` — rewritten from the stale "no source code yet" placeholder; documents `make up` as
  the canonical entrypoint for both the operator and future Claude Code sessions, plus build/test
  commands and where the governed architecture docs live.
- `docs/spikes/` (new) — F-001 and F-002 output; F-002 now includes §9 (Measured Results for
  Gemma 4 E4B, kept as historical data) and §10 (model comparison + final decision), with title,
  status header, and §7 verdict all updated to reflect the final pick is Llama 3.2 3B Instruct.
- `docs/test-cases.md` (new) — E2E/smoke scenarios for Phase 0; TC-003-04 updated for the
  `make up`-based topology.
- `docs/project-management.md` — F-001/F-002/F-003 → Done; F-002/F-008 AC updated for the final
  model pick; PM-004 closed; PM-005 closed by the model swap (not the `max_tokens` mitigation it
  was originally written around).
- `docs/architecture.md` — risk A2 (§8) marked Resolved; §3 Summarizer and §7 Technology
  Decisions updated to Llama 3.2 3B Instruct / ADR-017.

**Breaking changes:** None — this is the first code in the repository. (The mid-session pivot from
a containerized LM Studio to host-installed is a scope amendment within this same unreleased
revision, not a breaking change to anything previously shipped.)

**Known gaps / follow-ups (tracked in `docs/project-management.md` Open Items):**
- `npm audit` reports 6 moderate vulnerabilities in a frontend devDependency chain
  (`@angular/cli` → `@modelcontextprotocol/sdk` → `@hono/node-server`, Windows-only path
  traversal in a local dev-server adapter). No non-breaking fix exists; the available fix
  downgrades `@angular/cli` to 21.0.4, which would violate ADR-012's Angular 22 pin. Dev-only,
  not present in the production bundle — re-check when Angular tooling publishes a patched
  `@angular/cli` that doesn't require the downgrade.
- EF Core's `DbContext` and Hangfire's `AddHangfire`/`AddHangfireServer` are referenced but
  deliberately left unwired in `Program.cs` pending F-004 (Data Store schema) — wiring a
  live-DB-dependent startup path before the schema exists would make local verification silently
  depend on Postgres already being up.

**Smoke tests (see `docs/test-runbook.md` for full steps):**
1. **Happy path:** `make up` brings up `app` + `postgres:18.4` via Compose and checks/starts the
   host-installed LM Studio, loading the configured model; `make status` confirms all three
   reachable; `GET /` serves the Angular dashboard shell.
2. **Edge case:** `GET /api/ping` round-trips through Wolverine's command bus and returns a JSON
   status payload — proves the vertical-slice convention is wired end-to-end, not just present in
   source.
3. **Regression-sensitive:** a full clean rebuild (`dotnet clean` + removed `bin`/`obj`, `npm ci`
   fresh) reproduces an identical successful build — scaffolding must not depend on stale local
   state.

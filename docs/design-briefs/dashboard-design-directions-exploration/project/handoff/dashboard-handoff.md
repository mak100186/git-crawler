# git-crawler Web Dashboard — Design Handoff (F-018 → F-011)

Source brief: `uploads/dashboard-ux-brief.md` (F-018, v1). Visual design: `Dashboard Design.dc.html`
(direction "Ink Header", deep-olive second accent). Constraint: **Angular Material / CDK components
only** (ADR-011) — this document maps every visual region to its `mat-*` component and gives the
concrete theme values to style them with.

## 1. Theme tokens

Fonts (Google Fonts): **Caprasimo 400** for headings/nav/scores, **Figtree 400/600/700** for everything else.
Icons: **Lucide** at `stroke-width: 2.75`, round caps/joins.

| Token | Value | Use |
|---|---|---|
| bg | `#f5ead8` | page ground |
| surface | `#ebddc5` | cards, inputs |
| text/ink | `#201e1d` | body text |
| ink-900 | `#2e2b25` | top toolbar bg, snack-bars, expanded score panel |
| accent (terracotta) | `#c67139` | active nav pill, active sort option, primary buttons, bookmark-active, filter chips |
| accent-600 / 700 / 800 | `#b2622d` / `#8c491a` / `#643312` | hover / pressed & accent body text / error snack-bar bg |
| accent-100 / 200 / 400 | `#fff2eb` / `#ffe1d0` / `#f6a06b` | chip tints / progress track / accent on dark |
| olive-800 (2nd accent) | `#3d472b` | language tags, growth chips, trend & category icon circles — **solid fills only, never pale tints** |
| olive-100 | `#f0fae1` | text/icon on olive-800 |
| olive-500 | `#8fa073` | score-breakdown bars on ink |
| neutral-100…900 ramp | `#f9f4ed #eee7db #dcd3c4 #c0b6a5 #a19786 #82796a #645c50 #474238 #2e2b25` | dividers, muted text, disabled |

Radii: containers 16–28px; **all buttons, chips, inputs, toggles = 999px (pill)**. Elevation:
`0 1px 2px rgba(46,43,37,.14)` (sm), `0 3px 10px …16%` (md), `0 12px 32px …22%` (lg).
Focus: `outline: 2px solid #c67139; offset 2px` on `:focus-visible`. Selection tint: 30% terracotta.

## 2. App shell (§3)

- `mat-toolbar` — ink-900 bg, cream text, 64px. Contents: brand (Caprasimo 21px) → nav `mat-button`
  row → reserved slots right.
- Nav = four `mat-button`s bound to router links, order fixed: **Discovery Feed → Hidden Gems →
  Trending → Categories**. Active route: terracotta pill (bg `#c67139`, ink text, Caprasimo).
  Inactive: neutral-300 text, hover = 10% cream tint.
- **Reserved (render disabled/ghost, 1.5px dashed neutral-600 border, 55–60% opacity):**
  “Bookmarks · F-012” nav pill and a “Search (v2)” field, right-aligned. Ship them as inert
  placeholders so the shell doesn’t reflow when F-012 / v2 land.
- Breakpoint: CDK `BreakpointObserver` at **960px**. Below it: toolbar keeps brand only; nav becomes
  a floating bottom pill (ink-900, radius 999px, 4 icon links — active icon terracotta-400,
  inactive neutral-500); filter bar collapses to one `Filters · N` button opening a full-height
  `mat-sidenav`/bottom-sheet panel with the same controls; active chips stay inline next to it.

## 3. Filter / sort bar (§5) — Discovery, Hidden Gems, Category drill-down

Chips-first row directly under the toolbar (not on Trending):

1. Four facet pill buttons (`mat-button` w/ outline style, surface bg): **Language** → opens
   `mat-select multiple`; **Stars** → `mat-slider` with `matSliderStartThumb`/`matSliderEndThumb`;
   **Topic +** → `mat-chip-grid` + `mat-autocomplete`; **License** → `mat-select multiple`.
   Panels open anchored to their button (CDK overlay, as `mat-select` does natively).
   **Opened-panel internals are designed — see screen 08:** surface bg, radius 20px, shadow-lg,
   8–10px padding. Language/License option rows are pill-shaped (hover = 7% ink tint), 17px
   6px-radius checkboxes (terracotta fill when checked), catalog count right-aligned in
   neutral-600. Stars panel (300px): dual-thumb slider (18px terracotta thumbs) with the live
   range printed top-right, 0/25k+ scale captions, and synced Min/Max `mat-form-field` number
   inputs beneath. Topic panel (290px): chip-grid input on bg-cream (removable accent chips +
   caret), `mat-autocomplete` rows below with the typed prefix bolded and topic counts.
2. Divider, then the active-filter `mat-chip-set`: one removable chip per active value
   (tint `#fff2eb`, text `#643312`), + ghost “Clear all” `mat-button` when ≥1 chip.
3. Right: sort `mat-button-toggle-group` (Newest | Score | Stars | Commits — defaults per view),
   direction `mat-icon-button` (arrow flips asc/desc), and a “Bookmarked” `mat-slide-toggle`
   (§6.2 bookmarked-only filter; Discovery + Hidden Gems).
4. Flow per §5.4: chips update optimistically; indeterminate `mat-progress-bar` pinned under this
   bar during refetch; filters and sort fully composable.

## 4. Repository card (shared: Discovery, drill-down; Gems variant below)

`mat-card`, surface bg, radius ~32px, shadow-sm, 3-across grid (CSS grid, 16px gap), 18–20px padding.

- Header row: 40px circular avatar (initial, rotating terracotta/olive/neutral fills) ·
  `mat-card-title` repo name (Caprasimo 16px) · `mat-card-subtitle` “owner · relative date” (11px
  neutral-600) · bookmark `mat-icon-button` far right.
- Body: AI summary, 12.5px/1.5, clamped to 2 lines. If absent render the **“Summary pending”
  placeholder in the same fixed 38px slot** (screen 08): spinner glyph + italic “Summary pending”
  in neutral-600 over one neutral-200 ghost bar (86% width, 7px, radius 4px). Literal copy, no
  animated ellipsis; the real summary fades in place (150ms opacity) with zero height change (§4.1).
- Footer (`mat-card-actions`, top divider): language chip (**solid olive-800, olive-100 text**),
  star count, license `mat-chip` (neutral tint), right-aligned “GitHub” external `mat-button`
  with `open_in_new` icon.
- Bookmark toggle (§6.1): outlined bookmark (neutral-600) ↔ filled (terracotta); `matTooltip`
  “Add bookmark”/“Remove bookmark”; optimistic flip + `mat-snack-bar` w/ Undo (see §7).

## 5. Views

**01 Discovery Feed** — default route. Sort default: discovery date desc. Grid + `mat-paginator`
(24/page; G1 decision: paginator over infinite scroll).

**02 Hidden Gems** — same grid; sort default Score desc. Card adds a **52px circular terracotta
score badge** (Caprasimo 20px, cream text) left of the title, and a “Why this score?” footer row
that expands a `mat-expansion-panel`: ink-900 panel, five signal columns (License, Commits/week,
Contributors, Forks, Stars) each with value (Caprasimo 16px), olive-500 progress bar on
neutral-700 track, and `weight × · score/max` caption (FR-005: independently-weighted inputs).

**03 Trending** — no filter/sort bar; render API order. Vertical `mat-card` list (max-width 880px):
44px olive-800 circle w/ trending icon · trend title + “N repos in this trend” · growth `mat-chip`
(**solid olive-800**, e.g. “▲ +18% this week” — G2 text fallback, no chart lib) · expand chevron.
Expanded `mat-expansion-panel` lists contributing repos as rows: name, olive language tag, stars,
one-line summary (ellipsis), bookmark toggle. Sub-list is fixed order, not filterable.

**04 Categories** — 4-across clickable `mat-card` tiles: 56px solid circle icon (alternate
terracotta-600 / olive-800, icon in olive-100), Caprasimo label, “N repos” neutral chip. Tile →
router link to drill-down.

**05 Category drill-down** — breadcrumb “Categories / {name}”, then the exact Discovery layout
(filter bar + card grid + paginator) with the topic chip pre-applied and removable
(`topic: mcp-servers ✕`). No separate list component.

## 5b. Repository detail pane (screen 09)

Clicking anywhere on a card except its bookmark/GitHub controls opens a **`mat-drawer`
(`position="end"`, `mode="over"`, 560px)** over the current view — the list keeps scroll position and
filter state; no route change beyond a `?repo=<id>` query param so the pane is deep-linkable.

- **Sticky header** (ink-900): 52px avatar · repo name Caprasimo 24px · “owner · discovered {rel}” ·
  bookmark `mat-icon-button` (terracotta-400 when set) · close `mat-icon-button`. Second row:
  olive language tag, license chip (14% cream tint), stars, forks, and a terracotta
  **Open on GitHub** `mat-raised-button` with `open_in_new`.
- **Body** (scrollable, 20px gaps): *AI summary* — 14px/1.6 full text (not clamped), with
  “Generated {rel}” beneath; renders the same “Summary pending” placeholder when
  absent. *Activity* and *Topics* — two
  `mat-card`s side by side: commits/week, contributors, open issues, last commit; topic chips are
  clickable and apply that topic filter to the feed behind.
- **Sticky score bar** (bottom, ink-900, full width of the drawer): 62px terracotta score circle
  labelled “Gem score” + the same five weighted signal columns as the Gems expansion panel (one
  shared component). Always visible while the body scrolls.
- Bookmark and close are the two `mat-icon-button`s at the top right of the header — there is no
  footer action bar.
- **Keyboard/a11y:** Esc closes; focus traps in the drawer and returns to the
  originating card on close; `role="dialog"` + `aria-labelledby` on the repo name.
- **Narrow viewport:** same drawer at 100% width, header close button becomes a back arrow.

## 6. Cross-view states (§3, screen 06)

- First load: centered `mat-progress-spinner` (terracotta) + “Loading repositories…”.
- Refresh: indeterminate `mat-progress-bar` under the filter bar; existing results stay at 60% opacity.
- Empty: centered `mat-card` — neutral icon circle, “No repositories match these filters”,
  secondary button “Clear all filters”.
- Error: centered `mat-card` — terracotta-tinted alert circle, message, primary “Retry”.

## 7. Bookmark snack-bars (§6.1)

`mat-snack-bar`, pill shape, shadow-lg, bottom-center: ink-900 bg — “Added to bookmarks” /
“Removed from bookmarks” with terracotta-400 **UNDO** action; failure variant accent-800 bg,
accent-100 text — “Couldn't save bookmark — try again” + **RETRY**, and the icon reverts.

## 8. Designer sign-off on brief-delegated choices

These were left to the designer by the brief and are now confirmed, load-bearing decisions:

- **Star range = dual-thumb `mat-slider`** (`matSliderStartThumb`/`matSliderEndThumb`) **plus**
  synced min/max `mat-form-field` number inputs inside the same panel — the slider for coarse
  sweep, the inputs for precision (resolves the brief's either/or in §5.1 as both).
- **Sort = `mat-button-toggle-group`**, not `mat-select`: only four options, and visible options
  make each view's default ordering (its identity) legible at a glance. Direction is a separate
  `mat-icon-button`.
- **Filter bar = persistent pill row**, not a collapsible `mat-expansion-panel`: filtering is the
  dashboard's primary verb and active chips must stay visible (§5.3); collapse happens only below
  the 960px breakpoint (into the `Filters · N` sheet).

## 9. Deferred (do not build in F-011)

- Dedicated Bookmarks view → F-012 (ghost nav pill only).
- Free-text/semantic search → v2 (ghost field only).
- Sparkline charts (G2) and skeleton loaders (G3) → future ADR conversation; text chip + Material
  progress indicators ship now.

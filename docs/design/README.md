# Design

Artboards for the dashboard's visual design live here, committed alongside the code and kept in
step with it. This directory is the design source of truth going forward.

> Status: empty pending the first artboard drop. Nothing here yet — the previous design bundle has
> been retired (see below) and the replacement artboards land once the designs are updated.

## What happened to the old design brief

The v1 (MVP) dashboard was built against an exported design-tool bundle at
`docs/design-briefs/` — `dashboard-ux-brief.md` (F-018's written brief), the "Ink Header" direction
in `Dashboard Design.dc.html`, and `dashboard-handoff.md` (the implementation handoff: palette,
type system, per-screen specs). That bundle was **never committed** — it was git-ignored as a
dropped-in export of compiled JS and generated manifests, not something meant to be diffed by
hand — and it has now been deleted from disk. It is not recoverable from this repo's history.

**Roughly two dozen source comments still cite it** as the reason a value is what it is —
`// dashboard-handoff.md §1`, `§2`, `§3`, `§4`, and `dashboard-ux-brief.md §4.1`/`§5`/`§6.1`.
Grep for `dashboard-handoff` or `dashboard-ux-brief` to find them. Those references are now
dangling, and no one should go looking for the files. Read them as: _this was specified by the
approved v1 design, not chosen arbitrarily_. Rough section mapping, from the surviving citations:

| Reference                     | Covered                                                                                 |
| ----------------------------- | --------------------------------------------------------------------------------------- |
| `dashboard-handoff.md §1`     | Palette, type system (Caprasimo headings / Figtree body), icon stroke rules, focus ring |
| `dashboard-handoff.md §2`     | App shell — ink-900 toolbar, the <960px filter collapse to a `mat-sidenav`              |
| `dashboard-handoff.md §3`     | Filter/sort bar — pill row, opened-panel internals, chips + "Clear all"                 |
| `dashboard-handoff.md §4`     | Repository card                                                                         |
| `dashboard-ux-brief.md §3-§6` | Per-view layouts, filter/sort facets, bookmark interactions, view state machine         |

The shipped code — `src/frontend/src/styles.scss` (the full token set) and each component's own
SCSS — is the accurate record of what those sections actually said. Treat the code as canonical
where a comment's citation can no longer be checked.

## Adding artboards

Drop them in as `.dc.html` artboards (one canvas, multiple artboards) plus any exported stills.
Unlike the old bundle these are **committed** — a fresh clone must get them, which is the whole
point of moving off the ignored export directory.

When artboards land and the design changes, update the code comments that cite the retired brief to
cite the artboard instead, and delete the mapping table above once nothing references it.

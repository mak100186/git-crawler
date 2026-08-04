# Handoff: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-04

## What was done

**Full documentation sync pass — every governed doc checked against current implementation**
(`docs/project-management.md` v30) — a direct, operator-directed pass via Claude Code, not run
through `orchestrator-development-pattern` (documentation-only, no code changed; verified directly
instead, see below). The prior "Narrow-viewport filter sidenav no longer shows repo-card GitHub links
through it" entry has been moved to "Narrow-viewport filter sidenav no longer shows repo-card GitHub
links through it" below now that this entry supersedes it.

- **What changed and why**: after confirming the narrow-viewport fix worked, operator asked to
  "update all documents for whatever has been implemented so far." Checked every governed doc
  (`docs/prd.md`, `docs/architecture.md`, `docs/project-management.md`, `docs/test-cases.md`,
  `docs/test-runbook.md`) against the actual current codebase rather than assuming the existing text
  was still accurate — this session's many single-topic UI/backend changes (two-summary split, README
  cap, per-repository trend, MatDialog conversion, card score-panel removal, nav removal, narrow-
  viewport z-index fix) had each updated their own directly-relevant doc section at the time, but left
  `docs/test-cases.md`/`docs/test-runbook.md` specifically behind by several rounds — those two
  describe UI/API behavior at a level of concrete detail (exact card contents, exact panel/dialog
  mechanics) that's easy to invalidate with an adjacent change and easy to forget to re-check, unlike
  the higher-level Architecture/PMBook prose that already got touched each time.
- **`docs/prd.md` (v6 → v8)**: US-8 corrected — "each hidden gem's own category trend" was never
  actually true for the per-repository computation `docs/project-management.md` v28 shipped; this
  wasn't a v6-era inaccuracy that became true later, it was wrong from the moment the per-repo change
  landed and simply hadn't been caught until now.
- **`docs/test-cases.md` (v13 → v14) and `docs/test-runbook.md`**: corrected stale references to the
  card's own "Why this score?" panel and trend chip (both removed 2026-08-04, consolidated into the
  click-through detail dialog), the per-*category* `TrendAggregate`-based trend computation (TC-010-11/
  TC-011-13 rewritten for the per-repository version), the right-side `mat-drawer` detail pane (now a
  centered `MatDialog`, TC-011-14/15 updated for its actual close/resize behavior), a bottom pill nav
  that no longer exists (the primary nav was removed entirely 2026-08-04, not narrowed), and the
  renamed `Summary.Content` → `ShortContent`/`DetailedContent` columns in a raw `psql` query. Added
  new coverage that plainly hadn't existed at all: TC-008-09 (README-length cap), TC-011-16
  (paginator page-size dropdown), and a regression check for the narrow-viewport fix itself
  (TC-011-09 step 4, noted as operator-confirmed, not just applied).
- **Separately found while doing this pass, unrelated to any of the above**: `docs/prd.md`,
  `docs/architecture.md`, and `docs/project-management.md` each had their own header `Version` marker
  silently fall behind their own changelog table — e.g. `docs/architecture.md`'s header still read
  v18 while its own table already reached v22. Root cause: this session's rapid single-topic edits
  each appended a new changelog row but didn't always also bump the header line above it in the same
  edit. Fixed in all three files, including each file's cross-references to the others' versions
  (e.g. `docs/project-management.md`'s own "PRD: docs/prd.md (built against vN)" line).
- **Verification**: documentation-only change, no test suite applies. Every corrected claim was
  checked by reading the actual current source it describes (component templates, DTOs, migration
  files, the live `.filter-bar__sheet-container` CSS) rather than assumed correct on the first pass -
  same discipline as the code-level work this session, applied to prose instead of code.

---

## Narrow-viewport filter sidenav no longer shows repo-card GitHub links through it (superseded by the above, kept for history)

**Narrow-viewport filter sidenav no longer shows repo-card GitHub links through it**
(`docs/project-management.md` v29) — a direct, operator-directed fix via Claude Code, not run through
`orchestrator-development-pattern` (an isolated CSS defect on an existing Done feature, not a new Task
Packet; verified directly instead, see below). The prior "Hidden Gems trend growth now computed per
repository, not per language" entry has been moved to "Hidden Gems trend growth now computed per
repository, not per language" below now that this entry supersedes it.

- **What changed and why**: operator screenshot of the narrow-viewport (<960px) "Filters" sidenav
  opened, showing the repository grid's own "Open on GitHub" links painted on top of the sidenav
  panel at three points instead of being hidden behind it. Read `filter-sort-bar.scss`'s existing
  z-index handling: `.filter-bar__sheet-container` (the `<mat-sidenav-container>`, i.e. Angular
  Material's `.mat-drawer-container`) ships Material's own default `z-index: 1` unconditionally: this
  app's own override already raises the sidenav and its backdrop - the container's *children* - to
  `z-index: 20`, but never touched the container element itself, so the container's own subtree was
  still only `z-index: 1` when compared against other content on the page (the repository grid,
  elsewhere in the DOM). This is offered as the most likely explanation given a careful read of every
  relevant CSS rule (confirmed via `@angular/material`'s own compiled `sidenav.mjs`/`card.mjs`/
  `button.mjs` that no other explicit z-index exists anywhere else in this app's own styles) - not
  confirmed against a live rendered DOM's actual computed styles, since no browser/devtools tool was
  available this session (see the honesty caveat below).
- **Fix**: added `z-index: 20; isolation: isolate;` directly on `.filter-bar__sheet-container`,
  matching the value already applied to its sidenav/backdrop children, so the whole subtree
  unambiguously outranks the grid regardless of the exact stacking-context mechanics at play.
  `isolation: isolate` is the defensive half of the fix - it forces this element to form its own
  stacking context outright, rather than depending on the `position: relative` + `z-index`
  combination Material's default CSS already gives it (which, per CSS spec, should already have been
  sufficient - the isolation property closes that gap regardless of why the plain z-index alone
  didn't visibly work). Also fixed a stale comment in the same file still describing `app.scss`'s
  toolbar as `z-index: 10` and referencing a `.app-bottom-nav` class that was removed earlier this
  session (see the "Hidden Gems tab decommissioned"-adjacent history further down this file) -
  outdated context that would have misled the next person reading it.
- **Confirmed working**: the fix was applied without a browser tool available to re-screenshot it
  directly (see the CSS-analysis writeup above) - operator confirmed separately, against the live
  narrow-viewport render, that the GitHub links no longer show through the opened sidenav ("it
  worked"). Recorded here as closed, not just applied.
- **Verification**: frontend 45/45 (no test targets this specific visual symptom - Vitest unit tests
  don't render real stacking/paint order), `npm run lint` clean, `npm run build`/`ng test`'s own
  build step confirms the SCSS itself compiles without error. Backend untouched, not re-run.

---

## Hidden Gems trend growth now computed per repository, not per language (superseded by the above, kept for history)

**Hidden Gems trend growth now computed per repository, not per language**
(`docs/project-management.md` v28) — a direct, operator-directed fix via Claude Code, not run through
`orchestrator-development-pattern` (an isolated computation-logic fix to an existing Done feature, not
a new Task Packet; verified directly instead, see below). The prior "README-length summarizer
failures fixed; bookmark toast styling filled in" entry has been moved to "README-length summarizer
failures fixed; bookmark toast styling filled in" below now that this entry supersedes it.

- **What changed and why**: operator: "Trend is currently calculated per language. I want it to be
  calculated per repository and then shown. What is currently being shown is the trend aggregate for
  the topic?" Confirmed by reading `GetHiddenGemsQueryHandler`: `HiddenGemCardDto.TrendGrowth` had
  inherited the old standalone Trending view's `TrendAggregate` rollup verbatim when that view was
  merged into Hidden Gems (see the "Trending tab decommissioned" entry further down this file) -
  `TrendAggregate` rows are keyed by `Category` (`Repository.PrimaryLanguage`), summarizing *every*
  repo of that language together, so every C# repo on the dashboard was showing the identical growth
  figure regardless of its own individual standing.
- **Fix**: `GetHiddenGemsQueryHandler` no longer queries `TrendAggregate` at all for this purpose.
  `ComputeScoresCommandHandler` already appends a new `Score` row per repository on every re-crawl
  (never upserts - confirmed by reading that handler directly) - `ComputeTrendGrowth` now diffs a
  repository's own two most-recent `Score.TotalScore` values from that existing history instead, no
  schema change needed. Same output format as before ("▲ +18%/▼ -12% vs. last period"), with the
  no-prior-score fallback reworded from `"{avg} avg score"` to `"{score} current score"` since it's no
  longer an average across many repos, just this one repo's sole score so far.
- **`TrendAggregate`/`AggregateTrendsCommand` are unchanged** - they still run and still back
  `GetCategoriesQuery`, the Language filter's option-list source. Only Hidden Gems' own per-card
  growth display stopped reading from them.
- **Docs updated for consistency**: `docs/architecture.md` (v22 — §3 Web Dashboard),
  `docs/project-management.md` (v28 — F-009's/F-010's rows amended).
- **Verification**: backend 85/85 (`GetHiddenGemsQueryHandlerTests`'s three `TrendAggregate`-fixture
  cases replaced with score-history fixtures - one new case explicitly adds a second, same-language
  repository to confirm the fix is actually per-repository and not still secretly blended across a
  language, per the operator's own framing of the bug) - `dotnet build`/`dotnet test`/`dotnet format
  --verify-no-changes` all clean. No frontend logic changed (`repository.model.ts`/`app.routes.ts`
  comments updated for accuracy only) - not re-verified against the live dashboard this session.

---

## README-length summarizer failures fixed; bookmark toast styling filled in (superseded by the above, kept for history)

**README-length summarizer failures fixed; bookmark toast styling filled in**
(`docs/project-management.md` v27) — two direct, operator-directed fixes via Claude Code, not run
through `orchestrator-development-pattern` (small, isolated bug fixes to existing Done features, not
new Task Packets; verified directly instead, see below). The prior "Summarizer split into short +
detailed summaries" entry has been moved to "Summarizer split into short + detailed summaries" below
now that this entry supersedes it.

- **What changed and why (summarizer)**: a live `make dev` run against the real backlog — the exact
  spot-check the prior entry below flagged as not yet done — surfaced an HTTP 400 from LM Studio
  while summarizing `openclaw/openclaw`. Reproduced directly against LM Studio's `/v1/chat/
  completions` endpoint with that repo's actual README (fetched from GitHub) to get the real error
  body, since `LmStudioRepositorySummarizer` was discarding it via a bare `EnsureSuccessStatusCode()`
  call: `"The number of tokens to keep from the initial prompt is greater than the context length
  (n_keep: 35489 >= n_ctx: 8192)"`. Root cause: the README (111KB, GitHub's `size` field) was sent to
  the model with no length cap at all — the loaded model serves an 8192-token context window, so any
  repo with a large enough README hits this, not just this one.
- **Fix**: `LmStudioRepositorySummarizer` now truncates `ReadmeContent` to
  `Summarization:MaxReadmeCharacters` (new config, default 6000) before building either prompt,
  appending `"[README truncated for length]"` when it does. 6000 was picked from the live failure's
  own numbers (~3.1 chars/token for that README) with headroom for denser code-heavy Markdown, and
  because a README's opening section (purpose/features/install) is where a summarizer needs the
  most signal — truncating the tail loses far less than truncating the start would. Truncated once
  per repo and reused for both the short and detailed calls, since the cap exists to fit the model's
  context window, not either prompt's own target length. Also fixed the `EnsureSuccessStatusCode()`
  swallowing the response body on any future LM Studio error — `CallLmStudioAsync` now reads and
  includes it in the thrown exception, so a future failure is diagnosable from
  `GenerateSummariesCommandHandler`'s existing `logger.LogWarning(ex, ...)` alone, without a manual
  repro. `docs/architecture.md` (v21 — §3 Summarizer) updated to document the cap.
- **What changed and why (bookmark toasts)**: operator asked to confirm the bookmark-toggle
  toasts (`Added to bookmarks`/`Removed from bookmarks` with Undo, `Couldn't save bookmark — try
  again` with Retry) matched a reference screenshot (dark rounded pill, bold uppercase action text,
  a distinct brick-red variant for the error case). The `BookmarkToggle` component's `MatSnackBar
  .open()` calls, copy, and `panelClass` wiring (`app-snackbar`/`app-snackbar-error`) were already
  fully implemented — but `styles.scss`'s own comment claiming those panelClass rules existed
  "above" the dialog rules was stale/inaccurate: they were never actually written, so every toast was
  rendering as Material's stock gray rectangular bar.
- **Fix**: added the actual `.app-snackbar`/`.app-snackbar-error` rules to `styles.scss`, targeting
  `.mat-mdc-snackbar-surface` (confirmed via `@angular/material/fesm2022/snack-bar.mjs`'s own
  component metadata that `panelClass` lands on an ancestor of the surface element, not the surface
  itself — same node_modules-inspection approach used for this file's mat-select token names).
  Standard toast: `--color-ink-900` background, matching this design's existing dark-surface tone
  (repo-detail dialog header, app toolbar). Error toast: `--color-accent-800` — there's no dedicated
  "danger" token in this palette (see the custom-property block at the top of the file), so the
  darkest/most saturated existing accent step doubles as this app's one danger surface rather than
  introducing an off-palette red. Both get `--radius-pill` corners and bold/uppercase action text
  (MDC's M3 default isn't uppercase, unlike the old M2 default the reference screenshot resembles).
- **Verification**: backend 86/86 (`dotnet build`/`dotnet test`/`dotnet format --verify-no-changes`
  all clean; no existing unit tests target `LmStudioRepositorySummarizer` directly — it's exercised
  live, not via `Fakes.cs`'s `FakeRepositorySummarizer`, so the README-truncation logic itself has no
  dedicated test). Frontend 45/45, `npm run lint` clean (styling-only change, no component logic
  touched, `bookmark-toggle.spec.ts` unchanged). The corrected summarizer behavior itself (does LM
  Studio now succeed for `openclaw/openclaw`) wasn't re-run end-to-end against the live batch job this
  session — worth confirming on the next `GenerateSummariesJob` run.

---

## Summarizer split into short + detailed summaries (superseded by the above, kept for history)

**Summarizer split into short + detailed summaries** (`docs/project-management.md` v26) — a direct,
operator-directed change via Claude Code, not run through `orchestrator-development-pattern` (a
data-model/prompt change to an existing Done feature, not a new Task Packet; verified directly
instead, see below). The prior "Summarizer prompt and scheduling adjusted" entry has been moved to
"Summarizer prompt and scheduling adjusted" below now that this entry supersedes it.

- **What changed and why**: F-008's Summarizer generated one summary per repo, shared by both the
  dashboard card (clamped to 3 lines) and the click-through detail dialog (shown in full). Operator:
  "there should be two kinds of summaries: short that show on the repo card and then the detailed
  one." Asked which generation approach before implementing (genuine cost/reliability tradeoff, not
  mine to pick silently) - operator chose two separate LM Studio calls per repo (one short prompt,
  one detailed prompt) over a single call with a parsed structured response, prioritizing per-prompt
  reliability over halving inference time.
- **Backend**: `IRepositorySummarizer.SummarizeAsync` now returns `RepositorySummaryResult(
  ShortSummary, DetailedSummary)` instead of a bare `string`. `LmStudioRepositorySummarizer` makes
  two sequential calls (not `Task.WhenAll` - LM Studio serves one local model instance, so
  concurrent requests would contend rather than actually parallelize) with two distinct system
  prompts: the existing short one (`Summarization:MaxSummaryLength`, unchanged at 220 chars) and a
  new detailed one (`Summarization:MaxDetailedSummaryLength`, new config, default 900 chars/2-4
  short paragraphs, still plain-text/no-headings since the dialog interpolates it directly rather
  than rendering Markdown). `Summary.Content` split into `Summary.ShortContent`/`Summary
  .DetailedContent`; `GenerateSummariesCommandHandler` writes both from the one `SummarizeAsync`
  call. `RepositoryCardDto`/`HiddenGemCardDto` gained `DetailedSummaryContent`, mapped from
  `Summary.DetailedContent` in `GetHiddenGemsQueryHandler`.
- **Migration deletes existing Summary rows, operator-confirmed**: "i dont mind if the existing
  summaries are deleted in this implementation." `SplitSummaryContentIntoShortAndDetailed` renames
  `Content` → `ShortContent`, adds `DetailedContent`, then `DELETE FROM "Summaries"` - avoids leaving
  every pre-existing row with a permanently-empty `DetailedContent` (Summary is create-once, no
  automatic backfill path otherwise). Affected repos aren't lost - they simply have no Summary row
  again, so `GenerateSummariesCommandHandler`'s existing "no Summary row yet" selection filter picks
  them back up automatically, including via the standalone hourly trigger added in the prior entry
  below.
- **Frontend**: `RepositoryDetailPane` now reads `item.detailedSummaryContent` instead of
  `item.summaryContent` for its "AI summary" section (falls back to the same "Summary pending"
  placeholder when null) - the card itself (`RepositoryCard`) is unchanged, still reads
  `summaryContent` only.
- **A live LM Studio run to confirm the model actually follows both new prompts wasn't done this
  session** (same category of gap prior summarizer-touching sessions have disclosed) - worth a
  spot-check the next time `make up`/`make dev` runs against a populated backlog, particularly
  whether the detailed prompt's 900-character/2-4-paragraph target holds up in practice the way the
  short prompt's 220-character one was already confirmed to.
- **Docs updated for consistency, not just code**: `docs/architecture.md` (v20 — §3 Summarizer
  rewritten for the two-summary/two-call design), `docs/project-management.md` (v26 — F-008's row
  amended again).
- **Verification**: backend 86/86 (7 tests touched: `Fakes.cs`'s `FakeRepositorySummarizer` now
  takes two args per enqueued summary, `GenerateSummariesCommandHandlerTests`/
  `AggregateTrendsCommandHandlerTests`/`GitCrawlerDbContextTests` updated for the renamed/added
  entity fields) — `dotnet build`/`dotnet test`/`dotnet format --verify-no-changes` all clean.
  Frontend 45/45 (`repository-detail-pane.spec.ts`'s two summary-content tests rewritten for the
  swapped field; five other fixture files updated to satisfy the DTO's new required field) —
  `npm run lint` clean. A locally running `dotnet watch` process (from an earlier `make dev` session)
  had to be stopped mid-session so `dotnet ef migrations add` could write its build output -
  `dotnet watch` auto-restarted itself afterward, no operator action needed to resume live iteration.
- **Race condition caught and fixed against the live local database**: `dotnet watch`'s own
  file-watcher detected the migration file the instant `dotnet ef migrations add` created it and
  auto-rebuilt/re-ran `Database.Migrate()` *before* the follow-up edit adding
  `DELETE FROM "Summaries";` to its `Up()` method landed - so this database's
  `__EFMigrationsHistory` recorded the migration as applied using the rename+add-column-only version,
  permanently skipping the delete on any future `Database.Migrate()` call (EF only applies a given
  migration ID once, regardless of the file's current content). Caught by directly querying the live
  table afterward (`docker compose exec postgres psql`) - 65 rows still had the old pre-fix
  "**Summary**"-formatted `ShortContent` and an empty `DetailedContent` from `AddColumn`'s own
  default, exactly the signature of a delete that never ran. Fixed by running the same
  `DELETE FROM "Summaries";` directly against this database once, by hand - the migration file
  itself is left as originally written (correct as-is for any fresh database that runs it from
  scratch, where this race can't occur since there's no already-applied history to race against).

---

## Summarizer prompt and scheduling adjusted (superseded by the above, kept for history)

**Summarizer prompt and scheduling adjusted** (`docs/project-management.md` v25) — a direct,
operator-directed change via Claude Code, not run through `orchestrator-development-pattern` (a
config/prompt-level tweak to an existing Done feature, not a new Task Packet; verified directly
instead, see below). The prior "Bookmarks tab decommissioned" entry has been moved to "Bookmarks tab
decommissioned" below now that this entry supersedes it.

- **What changed and why**: three related fixes to F-008's Summarizer, all from the same operator
  note. (1) **No more headings in the summary text** — `LmStudioRepositorySummarizer`'s
  `SystemPrompt` asked for a "concise, structured summary covering: purpose, key features, tech
  stack, and notable caveats," which the model rendered as literal section headings/labels
  ("Summary:", "Tech Stack:", etc). The dashboard card renders the summary in a fixed 3-line CSS
  clamp (`repository-card.scss`'s `-webkit-line-clamp: 3`) with no server-side truncation, so that
  heading text was eating into the visible budget and crowding out the actual summary content.
  Reworded to ask for a single plain-text passage, no Markdown/headings/bullet points/section
  labels. (2) **Summary should be a specific character length** — added
  `Summarization:MaxSummaryLength` (new config, default 220, confirmed with the operator — sized to
  fit the card's 3-line clamp at its 12.5px font / up to 420px card width) and told to the model
  directly in `BuildUserPrompt` ("Write the summary in {N} characters or fewer"). Deliberately
  enforced via the prompt, not by trimming the LM Studio response afterward — the operator's own
  framing was "so we dont have to truncate." (3) **Check more frequently for repos without a
  summary** — `GenerateSummariesJob` previously only ran via its chain attachment inside
  `ComputeScoresJob` (F-008's own header comment), giving it one chance to run per
  `Hangfire:CrawlerCronSchedule` cycle (daily by default); combined with
  `Summarization:BatchSize`'s 20-per-run cap, a backlog larger than one batch could sit unsummarized
  for days. `Program.cs` now also registers `GenerateSummariesJob` as its own independent
  `RecurringJob` (id `"generate-summaries"`, distinct from `"discover-repositories"`) on a new
  `Hangfire:SummarizationCronSchedule` (default hourly, `"0 * * * *"`), in addition to the existing
  chain attachment. Safe to run this often: `GenerateSummariesCommandHandler`'s own "no Summary row
  yet" selection filter makes every extra run a no-op once the backlog clears, and the job's own
  `AggregateTrendsJob` continuation is idempotent (F-009's upsert-by-period persistence).
- **No backend truncation existed to remove** — checked before assuming there was a trim step to
  delete: `Summary.Content` is written straight from `LmStudioRepositorySummarizer.SummarizeAsync`'s
  `.Trim()`'d response with no length cap anywhere in the write path; the only place text was ever
  being cut off was the frontend's CSS line-clamp, which the prompt-side length constraint now
  avoids hitting in practice rather than the code special-casing it.
- **Docs updated for consistency, not just code**: `docs/architecture.md` (v19 — §3 Summarizer
  responsibility now describes the plain-text/length-capped prompt instead of "concise, structured";
  §3 Job Scheduler responsibility now notes the Summarizer's second, standalone recurring trigger),
  `docs/project-management.md` (v25 — F-008's row annotated with the prompt/config/scheduling
  changes).
- **Pre-existing, unrelated documentation drift noticed while editing `docs/project-management.md`,
  not introduced or fixed here**: this file's own Version History had only reached v24 before this
  edit, but `docs/handoff.md`'s three most recent entries below (Repo card polish, Discovery Feed
  removal, Bookmarks removal) cite it at v25, v26, and v27 respectively — those version bumps were
  apparently never actually made to the file. Flagged rather than silently resolved, since fixing it
  correctly needs to know which historical edit each of those three numbers was meant to correspond
  to, not just renumbering forward from here.
- **Verification**: backend 86/86 (no new/removed tests — this is a prompt/config/scheduling change
  to existing code, not new surface area) — `dotnet build`/`dotnet test` both clean,
  `dotnet format --verify-no-changes` clean. No frontend changes. Not live-verified end-to-end
  against a running LM Studio instance in this session (same category of gap prior summarizer-touching
  sessions have disclosed, e.g. Phase 2's handoff below) — the prompt wording and character-cap
  instruction are new and unverified against the actual model's real-world adherence; worth a live
  spot-check the next time `make up`/`make dev` runs against a populated backlog.

---

## Bookmarks tab decommissioned (superseded by the above, kept for history)

**Bookmarks tab decommissioned** (`docs/project-management.md` v27) — a direct, operator-directed
change via Claude Code, not run through `orchestrator-development-pattern` (no Task Packet/Developer/
Reviewer/Integration loop applies to this entry; verified directly instead, see below). The prior
"Repo card polish + click-to-open detail pane" entry has been moved to "Repo card polish + click-to-
open detail pane" below now that this entry supersedes it.

- **What changed and why**: the dedicated `/bookmarks` view (F-012's entire delta — a card-grid
  listing every bookmarked repo, most-recently-bookmarked first) and its live nav entry were removed,
  along with the backend `ListBookmarks` endpoint/query/tests that existed only to serve it (unlike
  `GetCategories`, nothing else consumed it, so it's gone entirely). The operator's own framing: "i
  dont think we need the bookmarks tab either since its a filter on the hidden gems tab" — correct:
  `FilterSortBar`'s existing "Bookmarked only" toggle (already wired on Hidden Gems since F-011)
  surfaces the identical set of repos. The primary nav is now down to a single "Hidden Gems" entry.
- **Genuinely dead code this time, unlike the two prior removals' near-misses**:
  `RepositoryCardQuery.ToCardDto` — checked before deleting anything, since the same method survived
  both the Discovery Feed and (implicitly) Categories removals by virtue of `ListBookmarksQueryHandler`
  still calling it. With that handler gone too, `ToCardDto` has no caller left, so it was removed for
  real this time. Also removed: `BookmarkApiService.listBookmarks()` and the component-scoped
  `BookmarkChangeApiService` DI-override pattern that lived entirely inside the now-deleted
  `bookmarks.ts` (documented in F-012's own original handoff section below as a notable pattern - it
  no longer exists anywhere in the codebase).
- **Bookmark create/delete themselves are completely unaffected** — the toggle, its optimistic UI,
  and FR-007's "revisit it later" requirement all still work exactly as before, just via Hidden Gems'
  filter instead of a separate route.
- **Docs updated for consistency, not just code**: `docs/architecture.md` (v18 — §3 Web Dashboard/Web
  API responsibilities narrowed further), `docs/project-management.md` (v27 — F-010/F-011/F-012 rows
  all amended in place; F-012's row is the first of the three original F-010/011/012 features to get
  its own amendment rather than only being referenced from F-011's), `docs/test-cases.md` (v13 —
  TC-012-01/02/03/04/06 marked Removed following the established precedent, TC-012-05 retargeted in
  place to Hidden Gems' filter since that capability persists — same judgment call as TC-010-01's
  Discovery Feed retargeting), `docs/test-runbook.md` (F-012 section rewritten around the one
  surviving scenario, F-011's nav step narrowed to one entry). `docs/prd.md` deliberately left
  unchanged — FR-007 is still fully satisfied, just through a different UI path, not a scope change.
- **Verification**: backend 86/86 (was 89; `ListBookmarksQueryHandlerTests` removed) — `dotnet build`/
  `dotnet test` both clean. Frontend 47/47 (was 51; `bookmarks.spec.ts` removed with its component) —
  `npm run lint` clean.
- **Same pre-existing, unrelated `filter-sort-bar.scss` budget issue noted in every prior entry still
  applies** (not touched or worsened by this change either).

---

## Repo card polish + click-to-open detail pane (superseded by the above, kept for history)

- **What changed and why**: two small CSS tweaks the operator asked for directly — `.repo-card__summary`
  now clamps to 3 lines instead of 2 ("make more room for summary: 3 lines"), with the "Summary
  pending" placeholder's height bumped to match so a summary arriving later still never causes a
  layout jump; and the footer chip row's `padding-top` increased from 9px to 16px so it sits further
  below its divider line ("scoot the bottom layer... down a bit").
- **New: click-to-open repository detail pane**, per "click to open details pane. see 09 in
  [the design brief]" — the operator was pointing at `docs/design-briefs/dashboard-design-directions-
  exploration/project/Dashboard Design.dc.html`'s own screen 09 ("Repository detail pane... card click
  → right-side mat-drawer over the current view · list keeps its scroll position"), a mockup F-011's
  original implementation never actually built. Clicking a card anywhere except its own interactive
  controls (bookmark toggle, "Why this score?" panel, "Open on GitHub" link — each stops click
  propagation in the template) now opens a `mat-drawer` (mode="over", position="end") owned by
  `RepositoryGrid`, since that's the one component every card-clicking view (Hidden Gems, Bookmarks)
  shares. The pane shows the repo's full untruncated AI summary, its topics as a chip list (genuinely
  new — topics exist on every `RepositoryCardDto` but were never rendered anywhere before this), the
  same chips the card shows plus "Open on GitHub", and — only for items with a `scoreBreakdown`
  (Hidden Gems, not Bookmarks) — an always-expanded five-signal score breakdown.
- **New shared `shared/utils/score-breakdown.util.ts`**: the log-normalized progress-bar math
  (`normalizeLog` + the five caps) was extracted out of `RepositoryCard`'s own computed properties so
  the card's "Why this score?" panel and the new detail pane's score footer can't drift apart on the
  same display computation — both now call `buildScoreRows(breakdown)`. A deliberate, narrow
  extraction (one pure function, two real callers), not a speculative abstraction.
- **Scope note, checked before implementing**: no Functional Requirement in `docs/prd.md` ever
  committed to a detail pane — F-018's design brief drew one anyway, and it simply hadn't been built
  yet. Implementing it now is UX polish drawing on already-approved design, not new product scope, so
  `docs/prd.md` was left unchanged; `docs/architecture.md` and `docs/project-management.md` note it
  for completeness since it's a real, shipped capability either way.
- **Docs updated for consistency, not just code**: `docs/architecture.md` (v17 — §3 Web Dashboard
  responsibility bullet), `docs/project-management.md` (v26 — F-011 row amended a fourth time),
  `docs/test-cases.md` (v12 — new TC-011-14 "card click opens the detail pane", TC-011-15 "the card's
  own controls don't also trigger it"), `docs/test-runbook.md` (matching manual steps + corrected
  Vitest spec counts in both the F-011 and F-012 sections).
- **Verification**: backend unaffected (frontend-only change) — still 89/89. Frontend 51/51 (was 42;
  6 new: 2 `RepositoryCard` click-propagation cases, 2 new `RepositoryDetailPane` cases, 2 new
  `RepositoryGrid` drawer-open/close cases) — `dotnet build`/`dotnet test` and `npm run lint` all
  clean.
- **One real a11y lint catch, fixed before finalizing**: an initial draft wrapped `BookmarkToggle` in
  a plain `<span (click)="...">` to stop propagation, which `@angular-eslint/template`'s
  `click-events-have-key-events`/`interactive-supports-focus` rules correctly flagged (a bare `<span>`
  with a click handler isn't keyboard-accessible). Fixed by moving the stop-propagation handler onto
  `<app-bookmark-toggle>` itself instead of introducing a wrapping element — custom component
  selectors aren't flagged by this rule the same way bare HTML elements are (same reason `<mat-card>`'s
  and `<mat-expansion-panel>`'s own click handlers elsewhere in this component were never flagged).
- **Same pre-existing, unrelated `filter-sort-bar.scss` budget issue noted in every prior entry still
  applies** (not touched or worsened by this change either).

---

## Discovery Feed tab decommissioned (superseded by the above, kept for history)

**Discovery Feed tab decommissioned** (`docs/project-management.md` v25) — a direct, operator-
directed change via Claude Code, not run through `orchestrator-development-pattern` (no Task Packet/
Developer/Reviewer/Integration loop applies to this entry; verified directly instead, see below). The
prior "Trending tab decommissioned" entry has been moved to "Trending tab decommissioned" below now
that this entry supersedes it.

- **What changed and why**: the standalone Discovery Feed view (the base card-grid + filter/sort bar,
  no score breakdown) and its route/nav entry were removed, both the UI (`features/discovery-feed/`,
  the `/discovery-feed` route, the "Discovery Feed" nav entry) and the backend endpoint that existed
  only to serve it (`GetDiscoveryFeed` — unlike `GetCategories`, nothing else consumed this one, so
  it's gone entirely rather than kept; `GetHiddenGems` already covers the same shared D4 filter/
  sort/paginate contract as a full superset). The operator's own framing was direct: "there isnt much
  difference between that and the hidden gems" — true by this point, since Categories (Revision 9)
  and Trending (Revision 10) had already folded their distinct content into Hidden Gems, leaving
  Discovery Feed with nothing left to differentiate it. The default route now lands on Hidden Gems;
  nav order is Hidden Gems → Bookmarks (two entries).
- **Repo card avatar removed, requested in the same message** ("keep the score circle on hidden
  gem's card, remove the avatar"): `RepositoryCard`'s avatar-initial circle (and its terracotta/olive/
  neutral color rotation, `AVATAR_PALETTE`) is gone from the header entirely — the score badge is now
  Hidden Gems' sole header visual anchor. This was a shared component, so it also affects Bookmarks
  cards (which carry no score breakdown): those now render no header circle at all, since the operator
  asked specifically to remove the avatar without requesting a replacement for views that never had a
  score badge to fall back on.
- **Dead plumbing removed alongside the tab**: nothing beyond the slice/component itself this time —
  `RepositoryCardDto` (the base, non-score card shape `GetDiscoveryFeed` used to return) stays alive
  because `ListBookmarksQueryHandler` still returns it via `RepositoryCardQuery.ToCardDto` (checked
  before assuming it was dead, unlike the genuinely-orphaned Categories/Trending plumbing in the prior
  two removals).
- **Pre-existing staleness fixed while in these files anyway, missed by the two prior removals**:
  `Makefile`'s `make help`/`make up` output text still described the dashboard as "Discovery Feed,
  Hidden Gems, Trending, Categories" — stale since Revision 9/10, now corrected to "Hidden Gems,
  Bookmarks". Three explanatory comments in `RepositoryCardQuery.cs` still referenced the
  already-removed Category drill-down as a consumer — corrected to name the helper's actual current
  callers (`GetHiddenGems`, `ListBookmarks`).
- **Nav order**: Hidden Gems → Bookmarks (two entries; Discovery Feed's slot before Hidden Gems is
  gone).
- **Docs updated for consistency, not just code**: `docs/prd.md` (v7 — US-8 narrowed a third time),
  `docs/architecture.md` (v16 — §3 Web Dashboard/Web API responsibilities + FR-009 narrowed to Hidden
  Gems alone; `/api/discovery-feed`'s removal called out explicitly as a Web API surface change, same
  treatment as Trending's v15 entry), `docs/project-management.md` (v25 — F-010/F-011 rows amended in
  place a third time), `docs/test-cases.md` (v11 — TC-010-01/TC-011-02 retargeted from Discovery Feed
  to Hidden Gems in place rather than marked Removed, since the underlying filter/sort/paginate
  capability persists; TC-011-01 narrowed to one required view), `docs/test-runbook.md` (matching
  retargeting + a corrected, now-accurate Vitest spec count in both the F-011 and F-012 sections,
  which had drifted from the actual count across the two prior removals without being caught until
  now).
- **Verification**: backend 89/89 (was 114; `GetDiscoveryFeedQueryHandlerTests` removed — 17 `[Fact]`
  + 4 `[Theory]` × 2 `InlineData` = 25 cases) — `dotnet build`/`dotnet test` both clean. Frontend
  42/42 (was 47; `discovery-feed.spec.ts` removed with its component — 5 cases; `app.spec.ts`/
  `app.routes.spec.ts` updated for the two-entry nav and Hidden-Gems-default route) — `npm run lint`
  clean.
- **Same pre-existing, unrelated `filter-sort-bar.scss` budget issue noted in both prior entries still
  applies** (not touched or worsened by this change either).

---

## Trending tab decommissioned (superseded by the above, kept for history)

**Trending tab decommissioned** (`docs/project-management.md` v24) — a direct, operator-directed
change via Claude Code, not run through `orchestrator-development-pattern` (no Task Packet/Developer/
Reviewer/Integration loop applies to this entry; verified directly instead, see below). The prior
"Categories tab decommissioned" entry has been moved to "Categories tab decommissioned" below now
that this entry supersedes it.

- **What changed and why**: the standalone Trending view (a list of per-category trend cards with an
  expandable contributing-repos panel) and its route/nav entry were removed, both the UI
  (`features/trending/`, the `/trending` route, the "Trending" nav entry, `TrendingApiService`,
  `trend.model.ts`) and the backend endpoint that existed only to serve it (`GetTrending` — unlike
  `GetCategories`, nothing else consumed this one, so it's gone entirely rather than kept). The
  underlying trend *data* is unchanged (`TrendAggregate`, computed nightly by `AggregateTrendsCommand`,
  F-009) — only the dedicated browsing surface for it moved. Each Hidden Gems card now shows its own
  category's trend growth directly: `GetHiddenGemsQueryHandler` computes a `TrendGrowth` string per
  card server-side (same current/previous-period formula the old Trending view computed client-side
  per trend — "▲ +18% vs. last period", falling back to "{avg} avg score" when only one period
  exists), returned as a new `HiddenGemCardDto.TrendGrowth` field. `RepositoryCard` renders it as a
  chip (`.repo-card__trend-chip`, same visual language as the old `.trend-card__growth-chip`) via a
  new, independently-optional `trendGrowth` input, wired through `RepositoryGrid` alongside the
  existing `scoreBreakdown` pass-through.
- **Scope decision, asked of the operator rather than assumed**: since trends are inherently
  per-category (not per-repo), "the trending score" on a card was ambiguous between the growth chip,
  a raw average-score number, or both. Asked directly; operator chose the growth chip (reusing the
  exact text/format the old Trending view already showed), not a bare average-score badge.
- **Dead plumbing removed alongside the tab**: `TrendingApiService`, `trend.model.ts`
  (`TrendDto`/`TrendingRepositoryDto`), and the now-unused `trending-up` SVG icon registration (only
  the Trending nav entry/view used it).
- **Nav order**: Discovery Feed → Hidden Gems → Bookmarks (three entries; Trending's slot between
  Hidden Gems and Bookmarks is gone).
- **Docs updated for consistency, not just code**: `docs/prd.md` (v6 — US-8 narrowed further),
  `docs/architecture.md` (v15 — §3 Web Dashboard/Web API responsibilities + FR-009 narrowed;
  `/api/trending`'s removal called out explicitly since it's a Web API surface change, not just a UI
  one), `docs/project-management.md` (v24 — F-010/F-011 rows amended in place a second time),
  `docs/test-cases.md` and `docs/test-runbook.md` (Trending scenarios/steps trimmed or marked removed,
  same in-place-note precedent as the Categories removal).
- **Verification**: backend 114/114 (was 116; `GetTrendingQueryHandlerTests` removed, 4 new
  `GetHiddenGemsQueryHandlerTests` cases added for `TrendGrowth`) — `dotnet build`/`dotnet test` both
  clean. Frontend 47/47 (was 51; `trending.spec.ts` removed with its component, one new
  `repository-card.spec.ts` case and one new `hidden-gems.spec.ts` case added for the trend-growth
  chip) — `npm run lint` clean.
- **Same pre-existing, unrelated `filter-sort-bar.scss` budget issue noted in the prior entry still
  applies** (not touched or worsened by this change either).

---

## Categories tab decommissioned (superseded by the above, kept for history)

**Categories tab decommissioned** (`docs/project-management.md` v23) — a direct, operator-directed
change via Claude Code, not run through `orchestrator-development-pattern` (no Task Packet/Developer/
Reviewer/Integration loop applies to this entry; verified directly instead, see below). F-012's own
operative detail has been moved to "F-012 handoff" below now that this entry supersedes it.

- **What changed and why**: the standalone Categories view (a grid of category tiles) and its
  Category drill-down route were removed, both the UI (`features/categories/`, the `/categories` and
  `/categories/:category` routes, the "Categories" nav entry) and the backend plumbing that existed
  only to serve them (`GetCategoryRepositories` endpoint/query/tests). This is not a capability
  regression: Category is, and always has been, `Repository.PrimaryLanguage` (F-009 D2, carried
  through F-010/F-011 unchanged) — the exact same value Discovery Feed's and Hidden Gems' existing
  Language `mat-select` filter already narrows by. The Categories tab was a second, redundant way to
  reach that same filter, not a distinct one; removing it loses no discovery capability. `GetCategories`
  itself is untouched and stays mapped — it still backs `FacetOptionsService.ensureLanguageOptionsLoaded()`,
  the Language filter's own option-list source.
- **Dead plumbing removed alongside the tab**: `FilterSortBar`'s `forcedCategory` input and the
  pinned-non-removable-chip logic it alone existed for (only the now-deleted Category drill-down page
  ever passed it); `RepositoryApiService.getCategoryRepositories()` and `buildRepositoryQueryParams`'s
  `omitLanguage` option (only that method used it); the now-unused `layers` SVG icon registration
  (only the Categories nav entry/tile used it).
- **Nav order**: Discovery Feed → Hidden Gems → Trending → Bookmarks (Bookmarks moves from 5th to
  4th; the other three keep their existing order).
- **Docs updated for consistency, not just code**: `docs/prd.md` (v5 — US-8 narrowed to three views),
  `docs/architecture.md` (v14 — §3 Web Dashboard responsibility + FR-009 narrowed to three views),
  `docs/project-management.md` (v23 — F-010/F-011 rows amended in place, matching this doc's own
  convention for scope changes to already-Done features, rather than reopening either feature),
  `docs/test-cases.md` and `docs/test-runbook.md` (scenarios/steps referencing the Categories tab or
  the drill-down endpoint trimmed or marked removed — TC-010-04 narrowed to the still-live Categories
  *list* endpoint only, TC-011-05/TC-011-10 marked removed following the same in-place-note precedent
  TC-011-11 already established in that doc).
- **Verification**: backend 116/116 (was 121; the 5 removed were `GetCategoryRepositoriesQueryHandlerTests`,
  scoped entirely to the deleted endpoint) — `dotnet build`/`dotnet test` both clean. Frontend 51/51
  (was 61; 10 removed — `categories.spec.ts`/`category-detail.spec.ts` deleted with their components,
  plus one `filter-sort-bar.spec.ts` case and one `query-params.util.spec.ts` case scoped to the
  removed `forcedCategory`/`omitLanguage` behavior) — `npm run lint` clean.
- **Pre-existing, unrelated issue surfaced while verifying, not introduced by this change**:
  `npm run build` (which resolves to the `production` configuration per `angular.json`'s
  `defaultConfiguration`) fails `anyComponentStyle`'s 8kB budget on `filter-sort-bar.scss` (currently
  exactly at 8.00kB). Confirmed pre-existing via `git log` — that file was last touched by the
  `ui fixes` commits preceding this session, and this change touched only `filter-sort-bar.ts`/`.html`,
  never its `.scss`. Left unfixed as out of scope for a Categories-tab removal; worth a follow-up trim
  before the next production `dotnet publish`/Docker build, since that pipeline's `BuildAngularApp`
  MSBuild target runs `ng build` the same unqualified way.

---

## F-012 handoff (superseded by the above, kept for history)

**F-012 (Bookmarking) is Done — Phase 3 is now complete** (F-010, F-011, F-012 all `Done`), run as a
standalone, single-feature slice via `orchestrator-development-pattern` (same pattern as the prior
F-010/F-011 runs). F-011's own operative detail has been moved to "F-011 handoff" below now that this
run supersedes it.

- **F-012** (Bookmarking) — PASS on the first Developer/Reviewer attempt; Integration initially FAILed
  (round 1), reached PASS on retry (round 2). Bookmark create/toggle and backend CRUD had already
  shipped inside F-010/F-011's own scope — this feature's actual delta was narrow: a dedicated
  `/bookmarks` route (`features/bookmarks/{bookmarks.ts,html,scss,spec.ts}`), a live "Bookmarks" nav
  entry replacing F-011's inert ghost pill (`app.ts`/`app.html`/`app.spec.ts`), and
  `BookmarkApiService.listBookmarks()` reading F-010's existing `GET /api/bookmarks` (unpaginated,
  server-ordered most-recent-first — passed straight through, no client re-sort).
- **Un-bookmarking from this view removes the card, Undo restores it — implemented without touching
  any shared component.** `BookmarkToggle`/`RepositoryCard`/`RepositoryGrid` were all explicitly
  off-limits (existing, working F-011 code this feature only consumes). The Task Packet's suggested
  approach (listen to `BookmarkToggle`'s `bookmarkedChange` output) turned out not to be reachable —
  neither `RepositoryCard` nor `RepositoryGrid` forwards that output. Solved instead with a
  component-scoped Angular DI override: a private `BookmarkChangeApiService` inside `bookmarks.ts`
  that delegates every call to the real `BookmarkApiService` via `skipSelf` while tapping confirmed
  add/remove calls over an internal `Subject`, provided only within this component's own injector so
  it's invisible to every other view. Reviewer independently traced the actual injector wiring (not
  just the Developer's summary) and confirmed it resolves correctly in both production and the spec's
  mock-provider pattern before passing it.
- **Empty state and pagination handled as two more local, non-invasive workarounds**: `RepositoryGrid`
  hardcodes filter-oriented empty copy with no override input, so `bookmarks.ts` computes the same
  `isEmptyState` condition internally and renders its own minimal empty-state card only for that one
  state (Loading/Error/Populated still route through the real `RepositoryGrid`). Since
  `GET /api/bookmarks` returns everything unpaginated, `RepositoryGrid`'s paginator is fed the full
  array with `pageSize`/`totalCount` sized to the list length — present in the DOM but functionally a
  single inert page, simplest of the two Task-Packet-sanctioned options.
- **Reviewer** — PASS on the first review. Independently re-read the actual diff (not just the
  Developer's summary) and specifically verified the DI-decorator pattern's injector wiring, confirming
  all 6 of `docs/test-cases.md`'s new TC-012 scenarios were genuinely covered by the new/updated spec
  files.
- **Integration — round 1 FAILed on a pre-existing, unrelated issue; Reviewer-Integration correctly
  pushed back; round 2 PASSed.** Integration's round-1 report found all code/test gates green (backend
  121/121, frontend 61/61) but reported Overall Status FAIL solely due to 6 moderate `npm audit`
  findings in a devDependency chain (`@hono/node-server` via `@modelcontextprotocol/sdk` via
  `@angular/cli`'s tooling), plus flagged-but-deferred two documentation-drift items. Reviewer-
  Integration's round-1 FAIL made three points: (1) it initially *claimed* this was inconsistent with
  F-011 having passed with "the same issue" (asked for verification, didn't just assert it); (2) the
  test-runbook drift fix was actually in Integration's own scope, not a legitimate defer; (3) the
  test-cases-doc internal inconsistency needed resolving, not just flagging. The Orchestrator
  independently checked git history before the retry (`git log`/`git diff` on `package.json`/
  `package-lock.json`) and found commit `8387b4e` ("added playwright, make dev...") introduced the
  vulnerable dependency **after** F-011's own finalization commit — so Reviewer-Integration's inference
  was a reasonable but factually wrong guess, not a confirmed precedent violation; there was no actual
  F-011/F-012 inconsistency to resolve. Integration's retry fixed the two legitimately-in-scope doc
  drifts directly (`docs/test-runbook.md`'s stale F-011 step, `docs/test-cases.md`'s TC-011-11 vs
  TC-012-02 contradiction — see Documentation drift below) and carried the corrected npm-audit finding
  forward as an explicit Unresolvable Issue (now PM-007) rather than blocking Finalization on a
  breaking `@angular/cli` version-bump decision that isn't F-012's to make. Reviewer-Integration
  round 2 independently re-read the actual post-fix file contents (not just the quoted diffs) before
  reversing to PASS.
- **Documentation drift found and fixed this run**: `docs/test-cases.md` extended to v7 with a new
  `F-012 — Bookmarking` section (TC-012-01 through TC-012-06), drafted by the Orchestrator *before*
  dispatching the Developer (same Step 0.0 gap-closure precedent as every prior feature run, stated
  explicitly to both Integration and Reviewer-Integration this time to avoid F-010's earlier
  misattribution). During the Integration retry, `docs/test-cases.md`'s existing TC-011-11 was also
  corrected — its "Bookmarks · F-012 pill is present/disabled" assertion directly contradicted the new
  TC-012-02 ("pill is gone, replaced by a live entry") in the same document; retitled to "Reserved v2
  placeholder is inert" and narrowed to just the still-accurate Search (v2) assertion (v8 row added to
  that doc's own Version History). `docs/test-runbook.md`'s existing F-011 manual step was corrected
  the same way (it still instructed the operator to expect the disabled ghost pill). PMBook F-012 row
  `Planned` → `Done` (v22); Phase 3 → `Done`. New PM-007 open item added for the carried-forward `npm
  audit` finding (dev-only, pre-existing, needs a human call on a breaking `@angular/cli` bump).
- **Graphify** — one incremental pass this run, `--update` scoped to `src`. 17 new/changed files (13
  code + 4 Angular `.html` templates), not code-only so the full AST + one semantic-subagent-chunk
  pipeline ran (the 4 templates only needed one chunk). Graph grew **1445→1500 nodes**, **2262→2349
  edges**, **114→125 communities**. Community labels generated from each community's dominant source
  path (`Features/<Area>/<Slice>` for backend, `frontend <folder>` for frontend), same heuristic as
  every prior run.
- New/changed docs this run: `docs/project-management.md` v22, `docs/test-cases.md` v7 (F-012 section)
  and v8 (TC-011-11 correction, same file), `docs/test-runbook.md` (new F-012 section + F-011 step
  correction), `docs/changelog.md` (this session's revision bump), `docs/handoff.md` (this file).

All of it is uncommitted in the working tree as of this handoff — the Orchestrator does not run git
commands; see **Commit Messages** in this session's final response for what to run.

---

## F-011 handoff (superseded by the above, kept for history)

**F-011 (Web Dashboard) is Done**, run as a standalone, single-feature slice of Phase 3 via
`orchestrator-development-pattern` — scoped to F-011 alone (same pattern as the prior F-010 run), so
F-012 remains `Planned` and Phase 3 itself stays `Planned` until it completes too. F-010's own
operative detail has been moved to "F-010 handoff" below now that this run supersedes it.

- **F-011** (Web Dashboard) — PASS after one Reviewer retry round; Integration PASS (57/57 frontend
  + 121/121 backend tests, 0 production vulnerabilities); Reviewer-Integration PASS after one
  self-corrected round (see below). Four required views (`features/{discovery-feed,hidden-gems,
  trending,categories}/`) plus a Category drill-down (`features/categories/category-detail/`), all
  standalone, Angular-Material-only (ADR-011) components backed by live `HttpClient` calls to F-010.
  Shared components (`shared/components/{repository-card,repository-grid,filter-sort-bar,
  bookmark-toggle}/`) implement the FR-004 filter/sort contract (language/star-range/topic/license
  facets, `mat-button-toggle-group` sort, `mat-slide-toggle` bookmarked-only), the FR-005 score
  breakdown panel, and the FR-007 bookmark toggle (optimistic flip, snack-bar confirm/Undo,
  revert+Retry on failure) exactly once each, reused everywhere a repo card renders — including
  Trending's expanded contributing-repo rows.
- **App shell restyled to the operator-approved "Ink Header" design** (`dashboard-handoff.md`) —
  custom Material 3 theme tokens, Caprasimo/Figtree fonts, terracotta active-nav pill, CDK
  `BreakpointObserver`-driven responsive collapse at 960px (primary nav → bottom pill row, filter bar
  → "Filters · N" button + `mat-sidenav`). Reserved, inert "Bookmarks · F-012"/"Search (v2)"
  placeholders ship disabled so the shell won't reflow when those land.
- **Reviewer FAILed once, fixed and re-verified PASS**: (1) the 960px filter-bar collapse was
  entirely missing (no `BreakpointObserver`, no trigger, no sidenav) — a silent deviation from the
  binding design doc; (2) the Categories tile grid and mobile bottom-nav links were built from
  custom-CSS anchors mimicking `mat-card`/`mat-icon-button` instead of the real components — an
  ADR-011 violation despite the component already importing `MatCardModule` elsewhere. Both fixed;
  round-2 diff verified scoped to exactly the claimed files.
- **Integration found and fixed a genuine pre-ship runtime defect neither Developer nor Reviewer
  caught**: `FilterSortBar` was missing a `MatInputModule` import, so the Star-range Min/Max facet
  inputs would have thrown `mat-form-field must contain a MatFormFieldControl` in a live browser —
  AC2 ("filter/sort controls work end-to-end") was not actually true until this fix landed. This was
  the root cause of 22 of 23 originally-failing tests across four spec files. Also fixed four
  test-only defects (a `RouterTestingHarness` called twice in one test, a snack-bar mock that
  synchronously auto-fired an unmocked "Undo" call, an over-broad DOM selector colliding with a
  legitimately-reused BEM modifier class, and an assertion contradicting `HttpParams.getAll()`'s
  documented null-vs-empty-array behavior) — none gamed, all genuine root-cause fixes.
- **Reviewer-Integration FAILed once on a reporting-accuracy issue, not a code defect**: the
  Integration Agent's Documentation Drift section quoted a sentence from the PMBook's F-011 row that
  didn't exist in the file *yet* — the `MatInputModule` finding was true and worth recording, but the
  report had gotten ahead of the actual document edit. Fixed by making the edit for real (PMBook →
  v21) and correcting the report to match; re-verified PASS by reading the literal post-edit file.
- **Live E2E validated, not deferred as Manual**: this environment had both a .NET SDK and Node
  available, so TC-011-12 (originally spec'd Manual) ran for real — `dotnet publish` triggered the
  actual `BuildAngularApp`/`CopyAngularApp` MSBuild targets, the published host served `index.html`
  at `/`, and a directly-requested client route (`/hidden-gems`) correctly fell back to `index.html`
  via `MapFallbackToFile` instead of 404ing — confirming FR-009 AC3 end-to-end, closing the FR-009
  verification gap the prior F-010 handoff had left open (see What's Next in the F-010 section below,
  now closed).
- **Two F-010 contract gaps handled without inventing a backend endpoint, exactly as pre-flagged**:
  no facet-options endpoint exists, so language options are sourced from `/api/categories` and
  license/topic options accumulate client-side from repository cards already fetched that session
  (`FacetOptionsService`). `TrendDto` has no growth metric and `/api/trending` isn't deduplicated per
  category — the Trending growth chip computes a real delta between a category's two most recent
  period rows when both exist, falling back to the current average score (not a fabricated
  percentage) when only one exists.
- **Post-hoc refactor, operator-directed, after the feature loop closed**: the graphify pass below
  flagged `core/services/` as a low-cohesion (0.05) 54-node community mixing four unrelated concerns
  (F-010 API client wrappers, a query-param-building utility, a client-side facet-derivation service,
  and an unrelated Material-icon-registration bootstrap service). Split into `core/api/`,
  `core/facets/`, `core/icons/`; all 16 consumer files' import paths updated. Verified via a full
  rebuild/lint/test pass (57/57 still passing, build still outputs to `dist/dashboard/browser/`)
  before re-running graphify on the rename. The old mega-community is gone; its members now sit in
  smaller, functionally-coherent communities (cohesion 0.07–0.31 instead of one 0.05 blob).
- **Documentation drift found and fixed this run**: `docs/test-cases.md` extended to v6 with TC-011
  (12 scenarios — four-view nav, filter/sort end-to-end, bookmark optimistic/undo/retry, Trending
  server-order, Categories grid/drill-down, loading/empty/error states, pagination-beyond-last-page,
  "Summary pending" no-layout-jump, 960px responsive collapse, category URL-encoding, reserved
  placeholder inertness, and the live-publish smoke test). PMBook F-011 row `Planned` → `Done`
  (v20, corrected to v21 for the `MatInputModule` finding). `docs/test-runbook.md` extended with an
  F-011 section (Happy path ×3, Edge case, Regression-sensitive smoke test).
- **Graphify** — two incremental passes this run. First: F-011's ~52 new/changed frontend files
  (Node/TS + HTML templates → full AST + semantic pipeline, 3 parallel subagent chunks since HTML
  templates aren't code-only). Second (the `core/services` split): 26 changed + 9 deleted files, all
  `.ts` except one comment-only `index.html` edit — deliberately ran AST-only and skipped the LLM
  semantic subagent dispatch for this pass (a judgment call: the actual content change was a
  mechanical rename plus a one-line comment, not new semantic material, so spending on redundant
  extraction wasn't worth it — documented explicitly rather than silently reusing the "code-only"
  fast path the skill wouldn't have granted on its own, since `index.html`'s content technically
  changed). Graph grew 1154→1442→**1445 nodes**, 1754→2279→**2262 edges**, 78→112→**114
  communities** across the two passes. The incremental prune step hit the same suffix-matching issue
  the F-010 handoff already documented (absolute deleted-file paths vs. relative `source_file`
  fields) — applied the same fix again. Also found: AST extraction assigns `source_file` at a
  different relative depth than semantic (LLM) extraction for the same file (e.g. `core/api/x.ts` vs
  `src/frontend/src/app/core/api/x.ts`), which broke folder-based auto-labeling for a handful of
  communities (`core`/`features` as bare, unhelpful labels) — caught by spot-checking and manually
  labeled instead of trusting the heuristic blindly.
- New/changed docs this run: `docs/project-management.md` v21, `docs/test-cases.md` v6,
  `docs/test-runbook.md` (new F-011 section), `docs/changelog.md` Revision 7, `docs/handoff.md`
  (this file).

All of it is uncommitted in the working tree as of this handoff — the Orchestrator does not run git
commands; see **Commit Messages** in this session's final response for what to run.

---

## F-010 handoff (superseded by the above, kept for history)

**F-010 (Web API) is Done**, run as a standalone, single-feature slice of Phase 3 via
`orchestrator-development-pattern` — the operator deliberately scoped this run to F-010 alone rather
than the full phase (F-010, F-011, F-012), so F-011 and F-012 remain `Planned` and Phase 3 itself
stays `Planned` until they complete too. Phase 2 (AI summarization/trend detection/UX brief) closed
2026-08-02 — see "Phase 2 handoff" below for that detail; this section covers F-010's operative state.

- **F-010** (Web API) — PASS on the first Developer/Reviewer attempt; Integration PASS (121/121
  tests, 0 vulnerabilities, ran twice for stability); Reviewer-Integration PASS after one
  self-corrected round (see below). `Features/{Repositories,Trends,Categories,Bookmarks}/` — 8 new
  Wolverine command/query slices (ADR-015): `GetDiscoveryFeed`, `GetHiddenGems`, `GetTrending`,
  `GetCategories`, `GetCategoryRepositories`, `CreateBookmark`, `DeleteBookmark`, `ListBookmarks`,
  each with its own endpoint dispatching via `IMessageBus.InvokeAsync` (matching the existing
  `PingEndpoint` pattern). A shared *internal* helper, `Features/Repositories/RepositoryCardQuery.cs`
  (plain class, not a Wolverine message), implements one filter/sort/paginate contract reused by
  Discovery Feed, Hidden Gems, and Category drill-down — avoiding tripling that logic while keeping
  each Wolverine slice boundary intact per ADR-015.
- **Two schema gaps closed as additive migrations** (same precedent as F-007's
  `AddScoreStarCountSignal`), both bundled into F-010 rather than deferred: `Repository.Topics`
  (`text[]` via EF Core's primitive-collections feature — GitHub topics were never crawled before
  this feature despite being in FR-004's filter scope; F-005's `GitHubDiscoveryClient` now fetches
  `repositoryTopics(first: 10)` alongside the existing discovery query) and
  `Repository.FirstDiscoveredAtUtc` (set once on first insert, never updated on re-crawl — this is
  what Discovery Feed's default "Newest" sort orders by; `LastCrawledAtUtc` would have been wrong
  since it advances on every re-crawl). **Neither column is backfilled for pre-existing rows**
  (`Topics` defaults to `{}`, `FirstDiscoveredAtUtc` defaults to `-infinity`) — tracked as new open
  item PM-006, see What's Next.
- **Two data-model decisions resolved before implementation, not re-derived by the Developer**:
  "Categories" stayed `TrendAggregate.Category`-derived (i.e. `Repository.PrimaryLanguage`), not
  GitHub-topic-derived, since F-009 already shipped that semantic and reopening it was out of scope;
  Trending's "contributing repos" for a trend are computed at query time (has both `Score` and
  `Summary`, latest `TotalScore`, matching `PrimaryLanguage`) rather than stored, mirroring
  `AggregateTrendsCommandHandler`'s own membership criteria exactly — `TrendAggregate` has no
  repo-level FK by design (see its own header comment).
- **Hidden Gems exposes FR-005's full weighted signal breakdown**, not just a total — each of the
  five signals (license/commits-per-week/contributor count/fork count/star count) alongside
  `ScoringWeights`' exact constants (18%/27%/22.5%/22.5%/10%) and `TotalScore`. Both Hidden Gems and
  Discovery Feed's `Score`/`Commits` sorts use each repo's *latest* `Score` by `ComputedAtUtc`, not
  `Max(TotalScore)` — the same class of correctness rule F-008 first got wrong and then fixed in
  Phase 2 (see Important Context below), applied correctly from the start here.
- **Bookmark create/delete are idempotent by design**: a double-create never throws the unique-index
  constraint violation (`Bookmark.RepositoryId`); a delete of a nonexistent bookmark never errors.
  Both documented in-code as deliberate choices, not oversights.
- **Reviewer** — PASS on the first pass. Independently re-ran the full test suite (121/121, matching
  the Developer's own report), cross-checked the score-breakdown weights against the literal
  `ScoringWeights.cs` constants, and verified the migration's generated SQL applies cleanly with
  correct defaults for both new NOT NULL columns against a non-empty table.
- **Integration** — PASS on the first attempt (no code fixes needed — format/build/tests/audit were
  already clean). Found and fixed one real documentation-drift gap: `docs/test-runbook.md` had no
  F-010 section despite 8 new user-facing endpoints — authored one, cross-referencing every scenario
  to its actual passing test. Flagged (not fixed, out of its scope) that no live `make up` stack was
  available to walk the new endpoints end-to-end over real HTTP — same category of gap Phase 1/2's
  own Integration passes disclosed for their live-infrastructure checks.
- **Reviewer-Integration — initially FAILed on a misattribution, then self-corrected to PASS**: it
  found `docs/test-cases.md` had also changed (+80 lines: a new `## F-010 — Web API` section,
  TC-010-01 through TC-010-10) and assumed the Integration Agent had silently authored and hidden
  that work, since Integration's own report only listed `docs/test-runbook.md` as touched. In fact
  the Orchestrator (not Integration) wrote the TC-010 section directly, *before* dispatching
  Integration, per this skill's own Step 0.0 ("Test Cases Doc — quality review... have the
  Orchestrator draft the missing scenarios") — the same gap-closure pattern already used for
  Phase 1/2 (see those sections below). When the Orchestrator flagged this, Reviewer-Integration
  independently re-read the skill's actual Step 0.0 and Documentation Drift Check text (rather than
  taking the correction at face value) and confirmed `test-cases-doc` was never in the Integration
  Agent's Documentation Drift scope in the first place — reversed its own verdict to PASS. Worth
  remembering for future sessions: when the Orchestrator pre-drafts test-cases-doc content itself,
  say so explicitly in the Integration Agent's prompt in a way that also reaches Reviewer-Integration,
  not just Integration — this ambiguity is cheap to prevent up front.
- **Documentation drift found and fixed this run**: `docs/test-cases.md` extended to v5 with TC-010
  (10 scenarios covering filter/sort/paginate, score breakdown, trend membership parity, categories,
  bookmark CRUD + idempotency, topic filtering, and the two schema-specific regressions). PMBook
  F-010 row updated `Planned` → `Done` with implementation annotations (v18); a new PM-006 open item
  added for the unbackfilled schema columns.
- **Graphify** — ran over `src/backend` only (`--update`, incremental), code-only fast path (all 34
  new/changed files were `.cs`, no LLM semantic extraction needed). Graph grew from 860 nodes/1223
  edges/55 communities (Phase 2) to **1154 nodes/1754 edges/78 communities**. The incremental diff
  initially reported 17 `src/frontend` files as "deleted" — verified false: they still exist on disk,
  the false-positive was purely a scope mismatch (an earlier run's saved manifest was built from a
  wider scan root than this backend-scoped run). Deletion pruning was deliberately skipped for those
  17 files to avoid destroying legitimate frontend graph nodes, and the manifest was re-saved scoped
  correctly to `src/backend` so a future backend-only `--update` won't repeat the false report.
  Community labels generated from each community's dominant `Features/<Area>/<Operation>/` source
  folder, same heuristic as Phase 1/2. Outputs refreshed in `graphify-out/` (`graph.html`,
  `graph.json`, `GRAPH_REPORT.md`).
- New/changed docs this run: `docs/project-management.md` v18, `docs/test-cases.md` v5,
  `docs/test-runbook.md` (new F-010 section), `docs/handoff.md` (this file). `docs/changelog.md` not
  yet bumped as of this edit — see Commit Messages in this session's final response.

All of it is uncommitted in the working tree as of this handoff — the Orchestrator does not run git
commands; see **Commit Messages** in this session's final response for what to run.

---

## Phase 2 handoff (superseded by the above, kept for history)

Phase 2 (AI summarization and trend detection; dashboard UX design brief) is complete, orchestrated end-to-end via `orchestrator-development-pattern`. Phase 1 (core data pipeline) closed 2026-08-02 — see `docs/changelog.md` Revisions 1-4 for that detail; this handoff focuses on Phase 2's operative state.

- **F-008** (Summarizer) — PASS after one retry round. `Features/Summarization/GenerateSummaries/`
  — `GenerateSummariesCommandHandler` selects repos by latest (`ComputedAtUtc`-ordered, not
  highest-ever) `Score.TotalScore ≥ Summarization:MinimumScore` (default 40) without an existing
  `Summary` row, capped at `Summarization:BatchSize` (default 20); README fetched via GitHub REST
  (`GET /repos/{owner}/{repo}/readme`, reusing the Crawler's REST client, 404 handled gracefully);
  `IRepositorySummarizer`/`LmStudioRepositorySummarizer` call LM Studio's OpenAI-compatible
  `/v1/chat/completions` endpoint at `max_tokens: 300` (per ADR-017, no truncation risk at this
  model). Per-repo failures (README or LM Studio) are logged and skipped, not batch-aborting — no
  Polly pipeline, unlike ADR-018's Crawler pipeline, since LM Studio's local API has no rate-limit
  *signal* equivalent to retry against. `ComputeScoresJob` now attaches `GenerateSummariesJob` as
  chain link 3 via a new `ISummarizationContinuationLink` seam. **First-round Reviewer FAIL**: the
  original selection logic used `Scores.Max(s => s.TotalScore)` (highest-ever value) instead of the
  chronologically latest score, which could permanently summarize a repo off a historical peak it
  has since fallen below (Summaries are create-once, never regenerated). Fixed to
  `OrderByDescending(ComputedAtUtc).First().TotalScore`, matching `ComputeScoresCommandHandler`'s
  own established convention; a regression test now distinguishes the two semantics explicitly.
- **F-009** (Trend Aggregator) — PASS on the first attempt. `Features/Trends/AggregateTrends/` —
  rolls up repos with both a `Score` and a `Summary` into per-category (`Repository.PrimaryLanguage`,
  null excluded) trend rows, using each repo's latest `TotalScore`. Single-day period by default
  (`Trends:PeriodDays`, default 1). Persistence is **upsert-by-`(Category, PeriodStart, PeriodEnd)`**
  — a third distinct persistence pattern in this codebase, alongside `Score`'s append-history and
  `Summary`'s create-once, needed for NFR-003 idempotency (re-running the same period must not
  duplicate rows). `GenerateSummariesJob` now attaches `AggregateTrendsJob` as chain link 4 via a new
  `ITrendsContinuationLink` seam, completing the pipeline: **Crawler → Scoring → Summarizer → Trend
  Aggregator**, all four links chained via Hangfire `RecurringJob` + `ContinueJobWith`.
- **F-018** (Dashboard UX design brief) — PASS on the first attempt, no code. `docs/design-briefs/dashboard-ux-brief.md`
  specifies the Discovery Feed, Hidden Gems, Trending, and Categories layouts, FR-004 filter/sort and
  FR-007 bookmark interactions (the "list bookmarked" verb resolved as a filter toggle within the
  four required views, not a fifth view — that's F-012's scope), and an explicit Angular-Material-only
  constraint (ADR-011) with three genuine component gaps (infinite scroll, trend sparkline, skeleton
  loader) flagged with Material-native fallbacks rather than silently spec'd as custom widgets. The
  "handoff to Claude Designer" is a document handoff, not a tool invocation — the actual design pass
  and its review/approval against F-018's four acceptance criteria are a follow-up step outside this
  feature's own scope, still gating F-011.
- **Integration** — PASS on the first attempt (no fixes needed — format/build/64 tests/audit were
  already clean). Live E2E of the real F-008→F-009 chain was **not** executed: LM Studio's local
  server could not be started in the Integration Agent's environment (`lms server start` timed out).
  Recorded as TC-008-08 (Manual) in `docs/test-cases.md` and as a runbook caveat — see What's Next.
- **Reviewer-Integration** — PASS. Independently re-ran all quality gates (exact match: 64/0/0/0
  tests, 0 vulnerabilities) and confirmed no documentation-drift finding was dropped between being
  found and reported.
- **Documentation drift found and fixed this phase**: `docs/project-management.md`'s Phase 2 row was
  still `Planned` despite F-008/F-009/F-018 all being `Done` — corrected (v17). `docs/test-cases.md`
  extended to v4 with TC-008 (7 scenarios + 1 Manual), TC-009 (7 scenarios), TC-018 (3 scenarios).
  `docs/test-runbook.md` extended with F-008/F-009 sections (F-018 deliberately given none — a
  brief-vs-4-ACs review has no meaningful runbook steps beyond what TC-018 already specifies).
- **Documentation drift found, NOT fixed (carried over, out of scope for any current agent)**:
  `docs/diagrams/mmd/daily-discovery-flow.mmd` was already stale before this phase (showed Scoring as
  independently scheduled rather than `ContinueJobWith`-chained) and is now more stale — the real
  chain is a 4-link Crawler→Scoring→Summarizer→Trend Aggregator chain the diagram doesn't show at
  all. Also noticed (pre-existing, unrelated to this phase): `docs/architecture.md`'s Version History
  table has a duplicate/out-of-order `v12` row (the Polly/ADR-018 entry vs. the A2-risk-resolved
  entry) — fixing the numbering needs to know original intent, not guessed at here.
- **Graphify** — ran over `src/` (`--update`, incremental), code-only fast path (all 24 changed files
  were `.cs`/`.json`, no LLM semantic extraction needed). Graph grew from 518 nodes/674 edges/48
  communities (Phase 1) to **860 nodes/1223 edges/55 communities**. The incremental `--update` prune
  step initially missed 28 ghost nodes from three files genuinely deleted earlier this session
  (`RetryDelay.cs`, `HangfireDashboardAuthorizationFilter.cs`, its test) — the prune comparison used
  absolute Windows paths from the file-change detector against the graph's stored relative
  `source_file` paths, so nothing matched. Caught by spot-checking the report's "Surprising
  Connections" section (it still referenced `IRetryDelay`/`FakeRetryDelay`, both from a deleted
  file), fixed with a suffix-based path match, re-clustered, and re-verified zero stale hits remain.
  Community labels for all 55 communities generated from each community's dominant
  `Features/<Area>/<Operation>/` source folder (a fast heuristic given the scale — 55 communities —
  rather than hand-authoring each one) — reasonable for a small, cleanly-vertical-sliced codebase
  where folder structure already tracks feature boundaries closely. Outputs refreshed in
  `graphify-out/` (`graph.html`, `graph.json`, `GRAPH_REPORT.md`).
- New docs this phase: `docs/design-briefs/dashboard-ux-brief.md` (new, F-018),
  `docs/adr/ADR-018-polly-resilience-for-github-crawler.md` (new, carried in from the Polly/Hangfire
  fix committed at the start of this session, not a Phase 2 feature itself).
  `docs/project-management.md` v17, `docs/test-cases.md` v4, `docs/test-runbook.md` extended,
  `docs/changelog.md` Revision 5, `docs/handoff.md` (this file).

All of it is uncommitted in the working tree as of this handoff — the Orchestrator does not run
git commands; see **Commit Messages** in this session's final response for what to run.

---

## Phase 1 handoff (superseded by the above, kept for history)

Phase 1 (Core data pipeline) is complete, orchestrated end-to-end via `orchestrator-development-pattern`. Phase 0 (scaffolding, spikes) closed 2026-08-01 — see `docs/changelog.md` Revision 1 for that detail; this handoff focuses on Phase 1's operative state.

- **F-004** (Data Store schema, EF Core) — PASS on the first attempt. `GitCrawlerDbContext` with
  five entities (`Repository`, `Score`, `Summary`, `TrendAggregate`, `Bookmark`) under
  `src/backend/GitCrawler.Api/Data/`, three migrations to date (`InitialCreate`,
  `AddCrawlerRawSignalFields`, `AddScoreStarCountSignal`). Hangfire's own job-storage tables are
  created separately by `UsePostgreSqlStorage` at F-006 runtime (its own `hangfire` schema, not
  EF-migrated) — documented on the DbContext so F-006 didn't duplicate schema setup. Reviewer
  independently re-ran the full test suite and inspected the generated migration SQL by hand.
- **F-005** (GitHub Crawler) — PASS on the first attempt. `Features/Crawling/DiscoverRepositories/`
  — GraphQL-first discovery via `Octokit.GraphQL` with a typed-`HttpClient` REST fallback for
  contributor count; idempotent upsert by `Repository.GitHubId`. Genuinely implements (not just
  documents) the F-001 spike's §6 back-off strategy — GraphQL `RATE_LIMITED`/`resetAt`, REST
  `x-ratelimit-*`/`Retry-After`, generic exponential backoff (60s doubling, capped 30 min)
  otherwise — and §7 mitigation (7-day contributor-count freshness cache), since REST's
  contributor-count fallback, not the GraphQL query, is the binding rate-limit constraint at scale
  per that spike's finding. Reviewer independently reflected on the installed `Octokit.GraphQL`
  0.4.0-beta assembly to verify the `RATE_LIMITED` string-match heuristic's stated limitation was
  real, not assumed.
- **F-006** (Job Scheduler, Hangfire) — PASS on the first attempt. `AddHangfire`/
  `UsePostgreSqlStorage`/`AddHangfireServer` wired into `Program.cs` — the wiring F-003/F-004
  deliberately deferred. Dashboard at `/hangfire` is unauthenticated (updated 2026-08-02, same
  day, per operator request — the original fail-closed shared-secret query-key filter was removed
  after it also blocked the dashboard's own CSS/JS assets and stats-polling XHR, none of which
  carry the `?key=` query string forward). Removing the filter alone still left the dashboard
  401ing, since Hangfire's own default (`DashboardOptions.Authorization` unset) falls back to a
  `LocalRequestsOnlyAuthorizationFilter`, and Docker Desktop's proxy doesn't preserve `127.0.0.1`
  for a host-browser request through it — fixed by passing `Authorization = []` explicitly.
  Live-verified against the real `make up` stack (`curl` 401 → 200). See ADR-009 Consequences for
  both write-ups. One recurring job (`discover-repositories`, daily by default via
  `Hangfire:CrawlerCronSchedule`) triggers the Crawler; the `ContinueJobWith` attachment point for
  the next stage was left as a documented code comment (not a stub), per the scope note that only
  one real pipeline stage existed yet.
- **F-007** (Scoring Engine) — PASS after a mid-flight scope amendment (see below), following a
  clean PASS on the original four-signal scope. `Features/Scoring/ComputeScores/` — pure
  computation (Architecture §3 requires zero external calls here), reads `Repository`'s raw
  crawled fields and writes weighted `Score` rows. Also completes the pipeline chain F-006 left
  open: `DiscoverRepositoriesJob` now attaches `ComputeScoresJob` via Hangfire `ContinueJobWith`
  after each crawl (via an `IScoringContinuationLink` seam so the wiring is unit-testable without a
  live Hangfire server).
- **Operator-directed amendment mid-session**: "star count should also be part of the scoring for
  a repository." Routed back through a Developer amendment + full re-review rather than patched
  silently, since it changed F-007's already-reviewed scope. Legitimate extension, not scope creep
  — Architecture §3's Scoring Engine description already named "existing popularity/quality
  signals (per PRD Constraints)" as a fifth category beyond the PRD's committed four. Final
  weights: license 18%, commits-per-week 27%, contributor count 22.5%, fork count 22.5%, star
  count 10% (rebalanced from the original 20/30/25/25 by a uniform ×0.9 scale factor) — star count
  deliberately weighted below the smallest primary signal so it can move a score without ever
  dominating the four signals the PRD explicitly commits to. A new additive migration
  (`AddScoreStarCountSignal`) added the column; `InitialCreate` and `AddCrawlerRawSignalFields`
  were untouched.
- **Operator-directed infra change mid-session**: "the docker container for postgres should have a
  local folder mounted so that the db is persisted even across sessions." PostgreSQL's Compose
  volume changed from a named Docker volume (`postgres-data:`) to a bind mount at `./data/postgres`
  — the database now persists as visible, backup-able host files, not just Docker-managed opaque
  volume storage. `.gitignore` updated (`data/postgres/*`, keeping `.gitkeep`);
  `docs/test-runbook.md`'s clean-rebuild step corrected to note `docker compose down -v` no longer
  clears a bind mount's contents (`rm -rf data/postgres/*` needed for a truly fresh DB). Handled
  directly rather than through the full feature Task Packet apparatus, consistent with how Phase
  0's own ad hoc operator-directed infra fixes (Makefile SHELL, config single-source-of-truth) were
  handled.
- **Test-cases doc gap found and closed before Integration**: `docs/test-cases.md` only covered
  Phase 0 (v1) when Phase 1 features completed — flagged per the orchestrator's own Step 0.0 rule
  rather than silently proceeding. TC-004 through TC-007 drafted (v2) covering all four Phase 1
  features, including the five-signal independence check the star-count amendment required.
- **Integration** — PASS after one round of genuine fixes (not gamed): (1) an EF Core query
  (`OrderByDescending(...).FirstOrDefault()` inside a `Score`-selection projection) that SQLite (the
  test provider) can't translate over `DateTimeOffset` but Npgsql/Postgres has no such restriction
  against — rewritten to resolve client-side via `.Include()` + `.Max()`, same business logic,
  verified behaviorally equivalent; (2) a Hangfire test-helper defect (`JobCancellationToken.Null`
  is a genuine null reference in Hangfire.Core 1.8.24, not a null-object instance, tripping
  `PerformContext`'s null guard) — fixed with a real no-op `IJobCancellationToken`, confirmed the
  production code under test never reads that state. Final: 43/43 tests passing, 0 build warnings,
  0 vulnerable packages. Docker was unavailable in the Integration Agent's environment, so three
  live-infrastructure checks (fresh-Postgres migration, Hangfire dashboard reachability, mid-run
  restart persistence) were explicitly flagged as unresolved rather than silently skipped — see
  What's Next.
- **Reviewer-Integration** — PASS. Independently re-ran all quality gates (exact match: 43/0/0/0),
  reproduced the Hangfire `JobCancellationToken.Null` claim in an isolated console project, and
  confirmed the diagram-drift finding (below) was genuinely dual-recorded rather than dropped
  between being found and reported.
- **Documentation drift found**: `docs/diagrams/mmd/daily-discovery-flow.mmd` depicts the Scheduler
  triggering Scoring independently/in parallel with the Crawler — not what F-006/F-007 actually
  built (Crawler is the only `RecurringJob`; Scoring is chained via `ContinueJobWith`, not
  independently scheduled). Regenerating the diagram is outside any current agent's scope; flagged
  for a manual diagramming pass, noted in `docs/test-runbook.md`'s Known Caveats.
- **Graphify** — ran over `src/` only (`--update`, incremental), code-only fast path (all 32
  changed/new files were `.cs`/`.json`, no LLM semantic extraction needed). Graph grew from
  168 nodes/157 edges/23 communities (Phase 0 baseline) to **518 nodes/674 edges/48 communities**.
  Outputs refreshed in `graphify-out/` (`graph.html`, `graph.json`, `GRAPH_REPORT.md`). One stray
  duplicate cache directory (`src/backend/graphify-out/`, produced by AST extraction running with a
  different cwd than the prior Phase 0 run) was found and excluded going forward — `.gitignore`'s
  graphify-cache rule broadened from two hardcoded stray paths to a general `**/graphify-out/` /
  `!/graphify-out/` pattern so any future stray location is caught automatically.
- New docs this session: none (`docs/test-cases.md` v2 and `docs/test-runbook.md` extended
  in-place rather than as new files). `docs/changelog.md` Revision 2. `docs/project-management.md`
  v15+ (F-004 through F-007 → Done, F-007's row amended for star count, all inline).

All of it was uncommitted at the time this Phase 1 handoff was originally written — see
`docs/changelog.md` for the actual commit history since.

---

## Current state

The platform now has a working, self-hosted, end-to-end **crawl → score → summarize → aggregate
trends** pipeline, a JSON Web API surfacing it, and a live Angular dashboard consuming that API —
all four pipeline stages still chained via Hangfire `RecurringJob` + `ContinueJobWith`. **Phase 3
(Dashboard, API, and bookmarking) is now fully Done.**

| Layer | State |
|---|---|
| Data Store | PostgreSQL 18.4 via EF Core; 5 entities, 4 migrations; unchanged this run |
| Crawler | GitHub GraphQL-first discovery + REST contributor-count fallback, Polly resilience pipeline (ADR-018); unchanged this run |
| Job Scheduler | Hangfire wired, dashboard at `/hangfire` unauthenticated; chains **four** links: Crawler → Scoring → Summarizer → Trend Aggregator; unchanged this run |
| Scoring Engine | Pure computation, five weighted signals; unchanged this run |
| Summarizer | LM Studio + Llama 3.2 3B Instruct via `IRepositorySummarizer`; unchanged this run |
| Trend Aggregator | Rolls up scored+summarized repos by `PrimaryLanguage` into `TrendAggregate` rows; unchanged this run |
| Web API | F-010, amended four times since (Categories/Trending/Discovery Feed/Bookmarks-list decommissioned — see the superseded sections below): 3 Wolverine slices remain, serving Hidden Gems (incl. each card's own trend growth), Categories (list only, backs the Language filter), and bookmark create/delete (no list) |
| Web Dashboard | F-011, amended five times since (Categories/Trending/Discovery Feed/Bookmarks view decommissioned, plus the card polish + detail pane — see the superseded sections below): Angular 22 SPA, live at `/` once `make up` is running — Hidden Gems is the dashboard's only view and default route; a card click opens a right-side detail pane (full summary, topics, score breakdown — design brief §09); approved "Ink Header" visual design; responsive below 960px; primary nav is a single "Hidden Gems" entry |
| **Bookmarking** | F-012: bookmark toggle (create/delete, optimistic UI) lives on every Hidden Gems card; "revisit later" is done via Hidden Gems' own "Bookmarked only" filter — the dedicated `/bookmarks` view this feature originally shipped was decommissioned (see the superseded section below) |
| Postgres persistence | Bind-mounted to `./data/postgres`; unchanged this run |
| Test harness | **86 xUnit backend tests + 47 Vitest frontend tests**, all passing (backend down from 121 at F-012's own original completion — each of the Categories/Trending/Discovery Feed/Bookmarks-list removals deleted tests scoped entirely to its own deleted slice; frontend net of the same four removals plus 6 cases added for the detail pane); `npm audit --omit=dev` and `dotnet list package --vulnerable` both clean (6 moderate frontend *dev*-only vulnerabilities carried forward as PM-007, see Important Context) |
| Docs | `docs/test-cases.md` v13, `docs/test-runbook.md`, and `docs/project-management.md` v27 cover Phase 0-3 in full (F-010, F-011, F-012) plus all five post-completion amendments; Phase 3 → Done |

No backend work happened this run — F-012 was frontend-only, per its own Task Packet scope
(F-010's `GET /api/bookmarks` already existed and was consumed unmodified).

A live database check before Phase 2 started (operator request) found the Crawler's discovery
query is functioning but only ever surfaces very-high-star repos (18.7K-453K stars across all 1,002
discovered rows) — GitHub's GraphQL search has no explicit sort parameter and defaults to
"best-match" relevance, which correlates heavily with popularity, combined with the ~1,000-result
visibility cap. The operator reviewed this and explicitly decided **not** to treat it as a bug for
now ("leave as-is") — noted here so a future session doesn't have to re-discover it from scratch.
If discovery strategy is revisited later (e.g. star-range bracketing, REST search with explicit
sort, or random sampling), that's a change to `GitHubDiscoveryClient.BuildSearchQuery()` (F-005),
not anything F-010/F-011 touched.

## What's next

1. **Phase 3 is complete — Phase 4 (Digest Service, Observability) is next.** F-013 (Digest Service)
   and F-014 (Observability) are both `Planned`, `Should` priority. F-013 depends on F-007/F-009
   (both Done); F-014 depends on F-005/F-006/F-007/F-008/F-009 (all Done) and can proceed
   incrementally. Invoke `orchestrator-development-pattern` scoped to Phase 4 (or one feature at a
   time, same pattern as every Phase 3 run) when ready to start.
2. **PM-007, new this run**: `src/frontend`'s devDependency tree has 6 moderate `npm audit`
   vulnerabilities (path traversal in `@hono/node-server`, reachable via `@modelcontextprotocol/sdk`
   → `@angular/cli`), introduced by commit `8387b4e` (added Playwright/`make dev` tooling), confirmed
   dev-only and unrelated to any feature's own diff. The only fix (`npm audit fix --force`) forces a
   breaking `@angular/cli` downgrade (22.x → 21.0.4) — needs an explicit human decision on toolchain
   compatibility risk before applying it.
3. **PM-006, still open, worth a quick verification pass**: F-010's two new `Repository` columns had
   no backfill for pre-existing rows as of F-011's close. A migration named
   `20260802091937_BackfillFirstDiscoveredAtUtc` now exists in
   `src/backend/GitCrawler.Api/Data/Migrations/` — it wasn't authored by this F-012 run and its
   content/application status wasn't verified as part of F-012's scope. Confirm it actually backfills
   `FirstDiscoveredAtUtc` correctly and has been applied, then close PM-006 for real rather than
   carrying it forward indefinitely.
4. **F-010's live-E2E verification gap is closed** (confirmed again this run, unchanged) — F-011's
   Integration pass ran `dotnet publish` for real and confirmed the dashboard serves correctly from
   the ASP.NET Core host. F-011's *own* three testing-depth gaps (TC-011-03's literal Undo-click-
   through, TC-011-04's exact server-order-preservation assertion, TC-011-08's visual no-layout-shift
   check) remain code-reviewed-but-not-automated — see `docs/test-runbook.md`'s F-011 section for the
   manual steps that cover them; not blocking, but worth automating if this dashboard sees heavier
   iteration. F-012 itself has the same style of gap for TC-012-05 (cross-view bookmark-state sync —
   verified by code trace during Integration, no dedicated automated test).
5. **Close the live-E2E verification gap from Phase 2, still open**: LM Studio could not be started
   in that Integration Agent's environment, so the real F-008→F-009 chain (actual README fetch +
   actual LM Studio inference + actual trend rollup against live data) was never exercised
   end-to-end — only via SQLite-backed unit/handler tests. Run this runbook's F-008 and F-009 Happy
   Path steps against a real `make up` stack with LM Studio actually running at least once.
6. **`docs/diagrams/mmd/daily-discovery-flow.mmd` still needs a manual diagramming pass** — flagged
   at Phase 1 close, still unaddressed: it doesn't show the Summarizer or Trend Aggregator links, and
   doesn't show the Web API or Dashboard as consumers of the Data Store. Not blocking Phase 4, but
   should be fixed before it misleads someone reading Architecture alongside the diagram.
7. **`docs/architecture.md`'s Version History table has a duplicate/out-of-order `v12` row**
   (noticed in Phase 2, still unaddressed) — fixing the numbering needs to know original intent;
   flagged rather than guessed at.
8. Once real summaries/trends exist at scale, sanity-check `max_tokens: 300` isn't clipping longer
   real-world READMEs the way the F-002 spike's synthetic test content couldn't reveal — carried over
   from Phase 1's handoff, still open.
9. **`docs/adr/ADR-011-angular-material-ui-library.md` (or wherever ADR-011 lives) could note the
   icon-sourcing decision** — F-011 sourced Lucide-style SVG icons via `MatIconRegistry.
   addSvgIconLiteral` rather than the Material Icons ligature font, a judgment call within ADR-011's
   scope (icon *assets*, not a component library) but worth a one-line ADR note if a future feature
   also needs iconography decisions. Not blocking, purely a documentation-completeness nice-to-have.

## Important context

- **A Reviewer-Integration FAIL's *reasoning* can be wrong even when its instinct to push back is
  right — verify the specific factual claim, don't just accept or reject the verdict wholesale.**
  This run's round-1 FAIL asserted F-011 had "passed with the same npm audit issue," implying an
  inconsistency. That specific claim was false (checked via `git log`/`git diff` on
  `package.json`/`package-lock.json`: the vulnerable dependency was added by a later, unrelated
  commit, after F-011's own finalization) — but two of the Reviewer-Integration's other three points
  (the test-runbook and test-cases-doc drift fixes being genuinely in Integration's own scope, not a
  legitimate defer) were correct and got fixed. Treat each point in a FAIL verdict independently;
  don't let one disproven claim discredit the rest, and don't let one correct point validate an
  unverified one.
- **When a Task Packet's suggested implementation approach turns out to be unreachable, say so and
  solve the underlying requirement a different way — don't force the suggested approach or silently
  drop the requirement.** F-012's Task Packet suggested "listen to `BookmarkToggle`'s
  `bookmarkedChange` output" for the un-bookmark-removes-card behavior; tracing the actual component
  tree showed neither `RepositoryCard` nor `RepositoryGrid` forwards that output, and both were
  off-limits to modify. The Developer solved it with a component-scoped DI override instead
  (`BookmarkChangeApiService`, `skipSelf`-delegating) and flagged the deviation explicitly — the
  Reviewer then independently verified the injector wiring actually works rather than trusting the
  explanation. This is the pattern to repeat: a suggested approach in a Task Packet is a hint from
  the Orchestrator's own file-reading pass, not a verified fact about the codebase's reachable APIs.
- **Stating test-cases-doc pre-draft ownership explicitly to *both* Integration and
  Reviewer-Integration up front (the lesson from F-010's run, below) worked cleanly this time** —
  this run's Reviewer-Integration was told directly in its prompt that the Orchestrator authored
  TC-011 before Integration started, and it neither flagged a misattribution nor needed to
  self-correct. Keep doing this for every future feature run; it's a cheap prevention for a
  confirmed real failure mode.
- **Integration/Reviewer-Integration can and do find genuine production bugs the Developer/Reviewer
  loop missed — that's the pipeline working as designed, not a process failure.** F-011's
  `MatInputModule` omission would have shipped a broken Star-range filter to a live browser; neither
  the Developer nor the Reviewer caught it (Reviewer's job is verifying claims against the Task
  Packet, not exhaustively running every component in a browser). Integration's independent test run
  caught it because the missing import broke `filter-sort-bar.spec.ts` and three other specs outright
  — a reminder that "Reviewer PASS" is not the same guarantee as "Integration PASS," and both gates
  matter.
- **A Reviewer-Integration FAIL can be about reporting accuracy, not code** — this run's round-1 FAIL
  was Integration's Documentation Drift section quoting PMBook text that didn't exist in the file
  yet, even though the underlying finding (`MatInputModule`) was true. The fix was making the
  document edit for real, not just editing the report. When an agent's report claims a specific,
  checkable fact about a file's *content*, verify that fact against the literal file before
  finalizing the report — don't let a true underlying finding excuse an inaccurate citation of it.
- **`core/services/` was split into `core/api/`/`core/facets/`/`core/icons/` post-hoc, operator-
  directed, not part of F-011's own Task Packet** — graphify flagged it as a low-cohesion (0.05)
  community mixing unrelated concerns (API clients, a query-param util, a facet-derivation service, an
  icon-registration bootstrap service). If a future feature adds another service to this folder,
  place it in whichever of the three sub-folders matches its concern (or a new sub-folder) rather than
  reintroducing a flat `core/services/` grab-bag — the split was a deliberate structural decision, not
  an arbitrary rename.
- **Graphify's AST and semantic (LLM) extraction passes can assign different relative depths to
  `source_file` for the same file** (e.g. `core/api/x.ts` from an AST-only incremental pass vs.
  `src/frontend/src/app/core/api/x.ts` from a semantic pass that scanned from the repo root) — this
  broke the folder-based community auto-labeling heuristic for a handful of communities this run
  (labeled bare `core`/`features` instead of something meaningful). Caught by spot-checking generic
  single-word labels on communities with meaningful size, not by trusting the heuristic blindly.
  Worth a fix in a future graphify-focused session (normalize `source_file` depth at extraction time)
  if this keeps recurring, but a manual label patch is a fine one-off workaround.
- **Deliberately skipped the semantic (LLM) extraction subagent dispatch for the `core/services`
  split's graphify update, even though the skill's `code_only` check would have required it** (one
  file, `index.html`, had a one-line comment change, technically making the batch not code-only) —
  judgment call that a mechanical rename plus a comment-wording fix has no new semantic content worth
  paying for, documented explicitly here and in the changelog rather than silently reusing the
  code-only fast path the skill wouldn't have granted on its own. Don't treat this as precedent for
  skipping semantic extraction whenever it's inconvenient — this was justified by the specific
  nothing-semantically-new nature of a rename, not by cost alone.
- **This session hit the account's monthly spend limit mid-Integration-run once** — the Integration
  Agent was interrupted mid-task by a billing error (not a task failure), resumed cleanly from its own
  transcript once the operator raised the limit, and picked up exactly where it left off. If a future
  session sees an agent "fail" with a spend-limit message, that's an infra/billing event to resolve
  and resume, not a defect to debug.
- **When the Orchestrator pre-drafts test-cases-doc scenarios itself (Step 0.0), say so explicitly
  in a way that reaches every downstream sub-agent, not just Integration** — this run's
  Reviewer-Integration initially FAILed the whole Integration Output because it saw
  `docs/test-cases.md` had changed and assumed Integration was hiding that work, when actually the
  Orchestrator wrote it before Integration even started (see What Was Done). It self-corrected by
  reading the skill's own text directly, but the ambiguity was avoidable — future sessions should
  make this ownership explicit to both Integration *and* Reviewer-Integration up front.
- **F-010 bundled two additive schema columns rather than deferring them** (`Repository.Topics`,
  `Repository.FirstDiscoveredAtUtc`) — same judgment-call pattern as F-007's mid-flight star-count
  amendment: a feature's own Acceptance Criteria genuinely required data the schema didn't yet
  capture, so closing the gap in the same feature (with a clear "why," an additive migration, and a
  documented backfill caveat — see PM-006) beat either silently shipping incomplete filtering or
  opening a whole separate feature for one column.
- **The "hidden gems only surface mega-popular repos" finding (see Current State) was raised and
  explicitly accepted by the operator before Phase 2 started** — don't re-flag it as a fresh
  discovery in a future session without checking here first; it's a known, accepted-as-is state,
  not an open item.
- **The "latest by time, not highest-ever value" `Score` rule now has a third and fourth correct
  application**: `GetHiddenGemsQueryHandler` and `GetDiscoveryFeedQueryHandler`'s `Score`/`Commits`
  sorts both got this right from the start in F-010 (following `ComputeScoresCommandHandler`'s
  original convention and the F-008 first-round mistake that established why it matters — see the
  Phase 2 handoff section below for that history). If a future feature also needs "this repo's
  current score," follow `OrderByDescending(ComputedAtUtc).First()`, never `Max(TotalScore)`.
- **Four distinct persistence patterns now coexist in this codebase, by design**: `Score` = append
  history (one row per re-score, never updated), `Summary` = create-once (never regenerated once
  created), `TrendAggregate` = upsert-by-key (`Category`/`PeriodStart`/`PeriodEnd`, updated in place
  on re-run), and now `Repository.FirstDiscoveredAtUtc` = set-once-on-insert-only (a single field
  within an otherwise-mutable entity, not a whole row's persistence style, but the same "never
  overwrite after first write" discipline as `Summary`). Each is deliberate and documented at its own
  handler — don't assume one pattern generalizes to a new stage without checking that stage's own
  idempotency/history requirements.
- **`TrendAggregate` has no repo-level FK, by design** — F-010's Trending endpoint computes
  "contributing repos" for a trend at query time (matching `PrimaryLanguage`, has both `Score` and
  `Summary`) rather than storing the relationship, deliberately mirroring
  `AggregateTrendsCommandHandler`'s own write-side membership criteria so the two never drift apart.
  If a future feature needs the same "which repos are in trend X" answer, reuse this same
  recomputation approach rather than adding a stored FK that could desync from F-009's own logic.
- **The graphify `--update` scope must stay pinned to `src/backend`** — this run's incremental diff
  initially misreported 17 still-present `src/frontend` files as deleted, because an earlier run's
  saved manifest had been built from a wider scan root. Verified false (files still on disk),
  deletion pruning was skipped for them, and the manifest was re-saved scoped correctly to
  `src/backend` — a future `--update` run scoped the same way should not repeat this. If it does,
  verify against the actual filesystem before trusting the prune step's "deleted" list, the same
  discipline Phase 2's handoff already established for the inverse (under-pruning) failure mode.
- **Version-pin caveats carried from earlier phases, still current**: LM Studio host-installed
  (ADR-016), `llama-3.2-3b-instruct` (ADR-017), PostgreSQL 18.4 (ADR-014), Angular 22 (ADR-012).
- **Open items**: PM-001, PM-002, PM-003 remain deferred exactly as before; PM-006 (schema backfill)
  may already be resolved by a migration that now exists on disk but wasn't verified this run — see
  What's Next #3; PM-007 (new this run) tracks the carried-forward `npm audit` finding.
- **Docs are governed, not exempt** — this run's Integration and Reviewer-Integration passes again
  treated the PMBook/test-cases/test-runbook as specs the code must satisfy: two genuine doc-drift
  fixes (test-runbook, test-cases TC-011-11) landed directly rather than being deferred, and a FAIL
  verdict's factual claim was checked against git history rather than accepted or dismissed on
  instinct. Keep doing this for Phase 4.

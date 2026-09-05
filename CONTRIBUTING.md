# Contributing to GitCrawler

GitCrawler is a self-hosted .NET 10 / Angular 22 modular monolith — one deployable process that
serves the API and the built Angular dashboard together. This guide covers how to get it running,
what has to be green before you push, and the few rules that are stricter here than in an average
repo.

Read [docs/setup.md](docs/setup.md) first for one-time prerequisites (Docker Desktop, LM Studio and
its `lms` CLI, a GitHub token, `.env`). This document assumes that is done and picks up from there.

---

## 1. Running the stack

**Always use `make`. Never run `docker compose up` on its own.**

`make up` is not a wrapper around Compose — it also checks Docker is running (starting Docker
Desktop if needed), verifies LM Studio's host-installed server is responding (starting it via `lms
server start` if not), and loads the configured model with `lms load`. Skip it and the stack comes
up without a model, so every summarization silently fails. LM Studio is host-installed rather than
containerized on purpose — see [ADR-016](docs/adr/ADR-016-lm-studio-host-installed-not-containerized.md).

```bash
make up       # full stack; dashboard at http://localhost:8080/
make status   # what is currently running (Docker, Compose services, LM Studio)
make health   # probe every component's real endpoint, not just "is the container up"
make logs     # tail the app container
make down     # stop Compose; LM Studio on the host is left running
make help     # everything else
```

### Use `make dev` while you are actually iterating

`make up` rebuilds the entire app image on every change — Angular build, .NET publish, Docker image.
That is the right call for a final check and the wrong one for a round of CSS fixes.

`make dev` containerizes only Postgres and prints the two commands to run the app bare, both
hot-reloading on save:

```bash
make dev
# terminal 1:  cd src/backend/GitCrawler.Api && dotnet watch run     -> :5073
# terminal 2:  cd src/frontend && npm start                          -> :4400
```

The dashboard is then at **http://localhost:4400/**, not `:8080` — Angular's own dev server, proxying
`/api/*` to the bare backend via [src/frontend/proxy.conf.json](src/frontend/proxy.conf.json).

**Stop `make up`'s `app` container first** (`make down`) if it is running. Otherwise it and your
bare backend both run the Hangfire pipeline against the same Postgres database, and you will be
debugging jobs you did not trigger.

---

## 2. The quality gate

CI ([.github/workflows/quality.yml](.github/workflows/quality.yml)) runs three jobs on every PR and
every push to `main`. Run the local equivalents before you push:

```bash
make format        # writes fixes: eslint + prettier (frontend), prettier (markdown), dotnet format
make test          # Vitest (frontend) + xUnit (backend)
make secret-scan   # gitleaks over full history and working tree
```

`make format` **writes**; CI **verifies** (`npm run format:check`, `npm run format:docs:check`,
`dotnet format --verify-no-changes`). A formatting failure in CI therefore always means someone
skipped `make format` — running it is the whole fix.

That covers markdown too: `README.md`, `CONTRIBUTING.md` and everything under `docs/` are formatted
and gated alongside source, because governed docs are specs here rather than side notes. The one
exclusion is `docs/archive/` — a closed tranche kept verbatim — via the root
[.prettierignore](.prettierignore). Prettier's config lives at the repo root
([.prettierrc](.prettierrc)) so `npx prettier` behaves the same from any directory.

`make secret-scan` needs gitleaks installed locally, pinned to **v8.30.1** to match CI exactly. If
you ever bump it, bump it in both the [Makefile](Makefile) and the workflow together — the point of
the pin is that the local check and the CI gate run the identical binary and ruleset.

CI also runs Snyk against both dependency manifests. That needs a `SNYK_TOKEN` and has no local
`make` target; the closest local equivalents are `dotnet list package --vulnerable
--include-transitive` in `src/backend` and `npm audit` in `src/frontend`.

Not run by CI: the Playwright spec at [src/frontend/e2e/home.spec.ts](src/frontend/e2e/home.spec.ts).
Run it by hand against a live stack if you touch the app shell.

### Never make a gate green by weakening it

No disabled or skipped tests, no loosened assertions, no swallowed errors, no widened lint
exclusions. If a check fails, either the code is wrong or the check is wrong — and changing a check
is a decision that belongs in the PR description, not in a quiet one-line edit.

---

## 3. Code layout, and the constraints that come with it

```
src/backend/GitCrawler.Api/
  Features/            one folder per vertical slice — Crawling, Scoring, Summarization,
                       Trends, Digest, Repositories, Bookmarks, Categories, Diagnostics
  Data/                EF Core DbContext, entities, migrations
  Infrastructure/      cross-cutting wiring
src/backend/tests/     xUnit
src/backend/tools/     SeedHarness (perf seeding, see `make seed-perf`)
src/frontend/src/app/
  features/            routed components
  shared/              components, pipes, utils reused across features
  core/                app-wide services (icon registry, API clients)
```

Four constraints are load-bearing. Each has an ADR behind it, and each is the kind of thing that
gets violated by reflex:

| Constraint                              | ADR                                                                                                                                          | What it rules out                                                                                                                                                  |
| --------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Vertical Slice + CQRS via **Wolverine** | [ADR-015](docs/adr/ADR-015-vertical-slice-cqrs-wolverine.md)                                                                                 | MediatR. Handlers live in the slice, not in a layered `Services/` folder.                                                                                          |
| **Angular Material only** for UI        | [ADR-011](docs/adr/ADR-011-angular-material-ui-library.md)                                                                                   | Third-party component or icon libraries. Icons are inline SVGs registered in [icon-registry.service.ts](src/frontend/src/app/core/icons/icon-registry.service.ts). |
| **Local** LLM via LM Studio             | [ADR-001](docs/adr/ADR-001-local-self-hosted-ai-summarization.md), [ADR-016](docs/adr/ADR-016-lm-studio-host-installed-not-containerized.md) | Cloud AI vendors and per-call costs.                                                                                                                               |
| **Hangfire** for scheduling             | [ADR-009](docs/adr/ADR-009-hangfire-job-scheduling.md)                                                                                       | Quartz.NET (ADR-006, superseded).                                                                                                                                  |

Read [docs/architecture.md](docs/architecture.md) and the [ADR index](docs/adr/) before any
structural or technology-choice change — and add or amend an ADR when you make one. ADR-016 is a
worked example of an amendment; ADR-009 superseding ADR-006 is an example of a full supersession.

### Match the surrounding code

This codebase comments _why_, not _what_ — usually citing the requirement, ADR, or design decision a
value came from. Match that habit and that density. A magic number with no cited source is the thing
most likely to come back in review.

---

## 4. Governed docs are specs, not notes

`docs/prd.md`, `docs/architecture.md`, `docs/project-management.md`, `docs/adr/` and
`docs/handoff.md` are specifications the code must satisfy. **Update them in the same change that
makes them stale, not as a follow-up.** Documentation drift is treated as a defect here, not
housekeeping — the orchestrator's Integration phase runs an explicit Documentation Drift Check
against these files and will fail on it. (CI does not; it only gates format, tests, and secrets.)

Also kept in step:

- [docs/test-cases.md](docs/test-cases.md) — scenarios per feature
- [docs/test-runbook.md](docs/test-runbook.md) — manual verification steps, one section per feature
- [docs/changelog.md](docs/changelog.md) — carries the revision counter in its own header
  (`> Revision: N`); there is no separate `REVISION.md`

Screenshots go in [docs/screenshots/](docs/screenshots/). Design artboards go in
[docs/design/](docs/design/) — read that README before chasing any `dashboard-handoff.md §N` comment
in the frontend, because that file no longer exists.

---

## 5. How work enters the repo

Tranche v1 (MVP) is closed. Tranche v2 is open with an **empty backlog** — no phases, no features.
So there is no "next ticket" to pick up, and new work goes through three gated steps rather than
straight to a branch:

1. **`/idea-discovery`** — evaluates an external input (a repo, article, tool) against this
   codebase, verifying premises against the code rather than the claim. Records a one-line entry in
   [docs/ideas.md](docs/ideas.md) and a research note in [docs/stash/](docs/stash/). Writes nothing
   else, and only on explicit instruction.
2. **`/idea-triage`** — drives one idea through PRD → Architecture → PMBook, each gated on explicit
   approval. Assigns the feature ID (v2 continues from **F-019**) and phase (from **Phase 6**). IDs
   are stable once assigned — never renumber.
3. **`/orchestrator-development-pattern`** — builds a PMBook item end-to-end through
   Developer → Reviewer → Integration → Finalization.

[.claude/SKILLS.md](.claude/SKILLS.md) binds those skills to this repo's paths. If you move or
rename a governed doc, update that file in the same change — a binding pointing at a missing path
degrades a skill's phase silently instead of failing loudly.

Working by hand instead of through the skills is fine. The gates still apply: a feature needs a
PMBook row, a structural decision needs an ADR, and the governed docs need to be true when you are
done.

---

## 6. Commits and pull requests

Use [Conventional Commits](https://www.conventionalcommits.org/), scoped to the area and citing the
feature ID where there is one:

```
feat(backend): add F-017 scalability indexes and server-side sort/pagination
feat(reliability): add Hangfire concurrency guards to all pipeline jobs (F-016)
fix(frontend): stop filter chips wrapping below 960px
docs: align test runbook with F-016 changes
```

Not every commit in this repo's history follows that — aim for the convention regardless.

Before opening a PR:

- `make format && make test && make secret-scan` all pass
- Governed docs updated in the same change, not promised in the description
- An ADR added or amended if you made a structural or technology choice
- Screenshots refreshed if you changed the UI, at their existing dimensions so the README layout holds
- Branch off `main` — do not commit to it directly

Say what you verified and what you did not. "Tests pass" should mean you ran them.

---

## 7. Gotchas

- **Windows:** the [Makefile](Makefile) forces its recipe shell to Git for Windows' bundled
  `bash.exe`, so `make` behaves identically from PowerShell, `cmd.exe`, or Git Bash — but it needs
  Git for Windows installed at its default location.
- **Two ports, two modes.** `:8080` is the container serving the built Angular app; `:4400` is the
  dev server from `make dev`. Running both at once means two processes on one database.
- **`data/postgres/`** is a live bind-mounted database directory. It survives `docker compose down`
  deliberately, and it is git-ignored — never commit it, and do not grep it (it is gigabytes of WAL
  segments, and it will match almost anything).
- **`make seed-perf`** seeds a _separate_ scratch database (`gitcrawler_perf`), never your real one.
  It drops and recreates that database on every run.
- **Summaries need a loaded model.** If they come back empty, check `make status` before debugging
  the summarization slice.

---

## Licence

MIT — see [LICENSE](LICENSE). Contributions are accepted under the same terms.

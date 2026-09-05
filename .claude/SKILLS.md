# Available Skills

This file binds project-specific variable values to skills that accept
parameters. Claude reads this file to know which skills are active and
what paths/values to inject.

Bindings are verified against the repo as of 2026-09-05 — every path below
either exists or is documented as auto-created. Re-verify after any docs
reorganisation; a binding that points at a non-existent path silently
degrades a skill's phase rather than failing loudly.

---

## Idea Discovery

Skill: `idea-discovery` (located at `~/.claude/skills/idea-discovery/SKILL.md`, v1.0.0)
Trigger: `/idea-discovery`, or when the user shares a repo/video/article and asks
what to make of it ("have a look at this repo", "can we use this", "add this to ideas").

Sits *before* `idea-triage` — it decides which ideas are worth triaging and leaves a
briefing behind. It never triages, never writes code, and never writes a file unless told to.

values:
  docs-directory: "./docs"
  ideas-file: "./docs/ideas.md"
  stash-directory: "./docs/stash"

---

## Idea Triage

Skill: `idea-triage` (located at `~/.claude/skills/idea-triage/SKILL.md`, v1.8.0)
Trigger: `/idea-triage` or when the user presents a new idea or feature concept before any code exists.

values:
  docs-directory: "./docs"
  prd-file: "./docs/prd.md"
  architecture-file: "./docs/architecture.md"
  project-management-doc: "./docs/project-management.md"
  handoff-doc: "./docs/handoff.md"
  archive-directory: "./docs/archive"
  adr-directory: "./docs/adr"
  diagrams-mmd-dir: "./docs/diagrams/mmd"
  diagrams-img-dir: "./docs/diagrams/img"
  diagram-render-script: "./.claude/scripts/render-diagrams.ps1"

---

## Orchestrator Development Pattern

Skill: `orchestrator-development-pattern` (located at `~/.claude/skills/orchestrator-development-pattern/SKILL.md`, v2.8.0)
Trigger: `/orchestrator-development-pattern`, or an explicit ask for the full pipeline
("orchestrate a fix for X", "run the orchestrator on this bug", "build this from the PMBook",
"spike this", "this is a P0"). A plain "debug X" / "fix this" is *not* a trigger.

The `orchestrator-development-pattern` detects the platform at runtime.

values:
  project-management-doc: "./docs/project-management.md"
  handoff-doc: "./docs/handoff.md"
  source-code-directory: "./src"
  revision-file: "./docs/changelog.md"
  changelog-file: "./docs/changelog.md"
  snyk-command: "cd src/backend && dotnet list package --vulnerable --include-transitive && cd ../frontend && npm audit --audit-level=high"
  test-runbook-location: "./docs/test-runbook.md"
  architecture-doc: "./docs/architecture.md"
  test-cases-doc: "./docs/test-cases.md"
  adr-directory: "./docs/adr"
  diagrams-mmd-dir: "./docs/diagrams/mmd"
  diagrams-img-dir: "./docs/diagrams/img"
  run-state-file: "./docs/.orchestrator-run-state.json"
  metrics-log-file: "./docs/orchestrator-metrics.md"

### Binding notes

- **`revision-file` and `changelog-file` are the same file on purpose.** This repo does not keep a
  separate `REVISION.md`; the revision counter lives in `docs/changelog.md`'s own header
  (`> Revision: N`) directly above the per-revision sections. Step 3.4 bumps that header and prepends
  the new section in one edit rather than touching two files.
- **`test-runbook-location` is a single file, not a per-feature directory.** Every feature's manual
  verification steps are appended as a section of `docs/test-runbook.md`. Automated coverage lives in
  `src/backend/tests/` (xUnit) and `src/frontend/src/**/*.spec.ts` (Vitest) and is out of the runbook's
  scope by design.
- **`snyk-command` is not Snyk.** The variable name is the skill's; the audit is the toolchain's own —
  `dotnet list package --vulnerable` for NuGet and `npm audit` for the Angular app. Secret scanning is
  separate and runs via `make secret-scan` (gitleaks, NFR-002/F-015) — the same check CI gates on.
- **`source-code-directory` is `./src`, not `./`.** The repo root also holds `data/` (a live Postgres
  volume), `test-results/`, and `graphify-out/`; pointing agents at the root makes them read gigabytes
  of WAL segments.
- **`run-state-file` is auto-created** on the first state write and is git-ignored — it is per-run
  scratch, not a governed doc. **`metrics-log-file`** is committed (it is a measurement log across runs).

---

## Graph-Assisted Context

Skill: `graphify` (located at `~/.claude/skills/graphify/SKILL.md`)
Trigger: `/graphify` or `/graphify query "<topic>"` or `/graphify --update`

Use graphify before starting any feature that touches multiple modules.
The graph report lives at `graphify-out/GRAPH_REPORT.md` after the first run.

---

## Find Skills

Skill: `find-skills` (located at `~/.claude/skills/find-skills/SKILL.md`)
Trigger: `/find-skills` or `/find-skills <query>`

Use this skill to discover available skills by name or capability.

---

## How Skills Are Loaded

1. Skills are installed globally at `~/.claude/skills/<skill-name>/SKILL.md`
2. This file (`SKILLS.md`) provides the variable values Claude substitutes at runtime
3. Skills can also be installed from marketplaces via `/install-skill`
4. Custom skills: create `~/.claude/skills/<name>/SKILL.md` with your own instructions

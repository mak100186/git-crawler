# Available Skills

This file binds project-specific variable values to skills that accept
parameters. Claude reads this file to know which skills are active and
what paths/values to inject.

---

## Orchestrator Development Pattern

Skill: `orchestrator-development-pattern` (located at `~/.claude/skills/orchestrator-development-pattern/SKILL.md`)
Trigger: `/orchestrator-development-pattern` or when the user says "start development", "implement", "build".

The `orchestrator-development-pattern` detects the platform at runtime.

values:
  project-management-doc: "./docs/project-management.md"
  handoff-doc: "./docs/handoff.md"
  source-code-directory: "./"
  revision-file: "./REVISION.md"
  changelog-file: "./CHANGELOG.md"
  snyk-command: "<TODO — dependency audit command, e.g. npm audit / pip-audit / cargo audit>"
  test-runbook-location: "./docs/testing/<FEATURE-ID>-<short-name>.md"
  architecture-doc: "./docs/architecture.md"
  test-cases-doc: "./docs/test-cases.md"
  adr-directory: "./docs/adr"
  diagrams-mmd-dir: "./docs/diagrams/mmd"
  diagrams-img-dir: "./docs/diagrams/img"

---

## Idea Triage

Skill: `idea-triage` (located at `~/.claude/skills/idea-triage/SKILL.md`)
Trigger: `/idea-triage` or when the user presents a new idea or feature concept before any code exists.

values:
  docs-directory: "./docs"
  prd-file: "./docs/prd.md"
  architecture-file: "./docs/architecture.md"
  project-management-doc: "./docs/project-management.md"
  adr-directory: "./docs/adr"
  diagrams-mmd-dir: "./docs/diagrams/mmd"
  diagrams-img-dir: "./docs/diagrams/img"
  diagram-render-script: "./.claude/scripts/render-diagrams.ps1"

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

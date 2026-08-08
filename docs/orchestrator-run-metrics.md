# Orchestrator Run Metrics

Token-usage log for `orchestrator-development-pattern` runs against this repo, kept so workflow
changes to the skill can be compared against real before/after numbers instead of a single
unanchored data point. Not a governed spec doc (nothing here is a claim the code must satisfy) —
just a measurement log.

**Caveat: runs are not equal-sized.** Total tokens scale with feature scope (files touched, retry
loops, docs updated) — compare runs of similar scope, not raw totals across the whole table.

| Date | Feature(s) | Skill Version | Total Tokens | Cache Hit Rate | Uncached Input | Cached Input | Output | Scope Notes |
|------|-----------|----------------|--------------|-----------------|-----------------|---------------|--------|-------------|
| 2026-08-07 | F-017 (Scalability: indexing & partitioning) | 2.1.0 | 48,174,596 | 83.6% | 7,847,651 | 40,003,038 | 323,907 | Largest run to date: new EF migration + query-handler rewrite + new `SeedHarness` console project + 4 governed docs updated + 1 full Integration retry (Reviewer-Integration round-1 FAIL on a report/runbook contradiction) + Docker networking investigation + graphify incremental update (4 parallel semantic subagents). Not comparable 1:1 to F-015/F-016 (smaller scope, no retries). |

## How to add a row

After a Finalization pass, pull the day's token dashboard totals and add a row with the skill's
`metadata.version` at the time of the run (from `~/.claude/skills/orchestrator-development-pattern/SKILL.md`
frontmatter) and a one-line scope note (files/retries/docs touched) so later comparisons account for
run size, not just the raw total.

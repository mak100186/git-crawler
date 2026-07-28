# PRD: GitHub Hidden Gems Discovery Platform

> Status: DRAFT
> Version: v1
> Last updated: 2026-07-28

## Problem Statement

Developers are repeatedly exposed to the same highly popular repositories, while thousands of potentially valuable repositories are created every week and never receive meaningful visibility. Existing discovery methods fall short:

- GitHub Trending favors projects that are already popular.
- Search results are noisy and hard to filter for genuine signal.
- Manual exploration of GitHub is time-consuming.
- Developers have limited time to evaluate whether a repository is worth investigating.
- Signals that indicate future potential (activity patterns, quality markers, early community interest) are scattered across GitHub metadata and not aggregated anywhere.

Developers need a way to discover high-potential projects early, quickly understand what a repository does, decide whether it's worth further investigation, and track emerging technology trends — without manually trawling GitHub.

## Goals

- **Discover hidden gems** — surface repositories that are relatively new, show evidence of quality, demonstrate early community interest, and solve interesting problems, ranked by a computed score rather than raw popularity.
- **Eliminate evaluation time** — generate concise, structured AI summaries so users don't need to read full READMEs, browse repository structure, or inspect manifests manually to judge relevance.
- **Detect trends** — aggregate discoveries into technology/framework/language/ecosystem trend summaries (e.g. growth in MCP-related repos, AI coding assistants, new .NET libraries, emerging Angular tooling).
- **Optimize for signal over popularity** — the core design principle: if GitHub Trending shows what's already successful, this platform should identify what is *about to become* successful.

## Non-Goals

Explicitly out of scope for this version (v1 / MVP):

- Social features (comments, following other users, shared workspaces).
- Team workspaces / multi-user collaboration.
- Repository cloning at scale (Phase 1 relies primarily on GitHub APIs; cloning is optional and limited, not a bulk operation).
- Personalized discovery via user-defined interest profiles (Goal 4 in the seed concept — explicitly deferred to a future version).
- Advanced personalization / recommendation engine ("because you liked X...").
- GitHub account integration (using a user's stars/follows to personalize discovery).
- Trend forecasting (predicting future high-growth repositories).
- Semantic/intent-based search.
- Repository-to-repository comparison.
- Browser extensions.
- Delivery via Teams, Slack, Discord, or RSS (v1 delivery is email + web dashboard only).

## User Stories

- As a software engineer, I want to discover newly-created repositories with strong quality and activity signals so that I find promising projects before they become mainstream.
- As a software engineer, I want AI-generated summaries of each discovered repository so that I don't have to read the full README or browse the codebase to decide if it's relevant to me.
- As a software engineer, I want to filter and sort discovered repositories by language, star range, topic, and license so that I can focus on the technologies I care about.
- As a software engineer, I want a daily digest of top hidden gems and emerging trends so that I stay current without actively searching GitHub myself.
- As a software engineer, I want to save/bookmark repositories I'm interested in so that I can revisit them later.
- As an engineering leader or developer advocate, I want aggregated trend summaries (e.g. "MCP-related repos are growing") so that I can track where the ecosystem is heading without reading every individual repo.
- As a user, I want to browse repositories through distinct views (Discovery Feed, Hidden Gems, Trending, Categories) so that I can explore the catalog in the way that matches what I'm looking for.

## Success Metrics

- **Discovery quality:** repositories bookmarked, repositories clicked, repositories starred (on GitHub) after being surfaced by the platform.
- **Summary quality:** user rating of AI-generated summaries; summary usefulness score.
- **Platform engagement:** daily active users, weekly digest opens, saved repositories.

Concrete numeric targets for each metric are intentionally left as `[TBD]` — this is a new platform with no usage data yet; targets should be set from an initial baseline period after launch rather than guessed upfront.

## Constraints & Assumptions

- **Team/timeline:** solo project, no fixed external deadline — MVP scope is not being trimmed for a launch date, but should still stay genuinely minimal (see Non-Goals) rather than expanding opportunistically.
- **Data source:** Phase 1 relies primarily on the GitHub API (REST/GraphQL) for discovery and metadata; full repository cloning is optional and not the default path, per the seed concept.
- **AI provider / cost approach:** intentionally undecided at the PRD level. The summarization layer must stay provider-agnostic (an abstraction like `IRepositorySummarizer`, not a hard dependency on one vendor) so the cost/quality tradeoff (cloud LLM vs. local/self-hosted model) can be decided in the Architecture phase without revisiting product scope. See Open Questions.
- **Processing volume assumption (from seed concept):** the platform should be designed to handle on the order of 1,000–5,000 repositories discovered/processed per day, scaling toward 100k+ repositories and 1M+ analysis records over time without a redesign. These are directional assumptions carried from the original concept, not committed NFR targets — Architecture phase should validate and formalize them.
- **Responsiveness assumption:** summary generation on the order of seconds-per-repository (not minutes), and a dashboard that feels interactive rather than batch-oriented. Exact thresholds to be formalized as NFRs in Architecture.
- **Security assumption:** the platform holds a GitHub API token and (eventually) an AI provider credential; both need secure storage and handling of GitHub API rate limits. Formal security NFRs to be defined in Architecture.

## Open Questions

| ID | Question | Owner | Resolved? |
|----|----------|-------|-----------|
| Q1 | Cloud LLM (Azure OpenAI/OpenAI) vs. local/self-hosted model for AI summarization — cost/quality tradeoff to be decided in Architecture phase given the provider-agnostic constraint above. | Maxx | No |
| Q2 | What are realistic initial numeric targets for the Success Metrics once the platform has a baseline usage period? | Maxx | No |
| Q3 | Should personalized discovery (Goal 4 / user-defined interests) be scoped into an early post-MVP phase, or is it a longer-term future enhancement with no near-term commitment? | Maxx | No |

## Version History
| Version | Date | Change | Triggered By |
|---------|------|--------|---------------|
| v1 | 2026-07-28 | Initial draft | — |

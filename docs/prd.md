# PRD: GitHub Hidden Gems Discovery Platform

> Status: APPROVED
> Version: v6
> Last updated: 2026-08-03

## Problem Statement

Developers are repeatedly exposed to the same highly popular repositories, while thousands of potentially valuable repositories are created every week and never receive meaningful visibility. Existing discovery methods fall short:

- GitHub Trending favors projects that are already popular.
- Search results are noisy and hard to filter for genuine signal.
- Manual exploration of GitHub is time-consuming.
- Developers have limited time to evaluate whether a repository is worth investigating.
- Signals that indicate future potential (activity patterns, quality markers, early community interest) are scattered across GitHub metadata and not aggregated anywhere.

Developers need a way to discover high-potential projects early, quickly understand what a repository does, decide whether it's worth further investigation, and track emerging technology trends — without manually trawling GitHub.

## Goals

- **Discover hidden gems** — surface repositories that are relatively new, show evidence of quality, demonstrate early community interest, and solve interesting problems, ranked by a computed score rather than raw popularity. Scoring must weigh concrete signals including license presence/type, activity level (commits per week), and community health (contributor count, fork count) — not stars alone.
- **Eliminate evaluation time** — generate concise, structured AI summaries so users don't need to read full READMEs, browse repository structure, or inspect manifests manually to judge relevance.
- **Detect trends** — aggregate discoveries into technology/framework/language/ecosystem trend summaries (e.g. growth in MCP-related repos, AI coding assistants, new .NET libraries, emerging Angular tooling).
- **Optimize for signal over popularity** — the core design principle: if GitHub Trending shows what's already successful, this platform should identify what is *about to become* successful.

## Non-Goals

Explicitly out of scope for this version (v1 / MVP) — grouped by *why* they're excluded, not just what they are:

**Deferred future enhancements — on the roadmap, just not v1:**
- Personalized discovery via user-defined interest profiles (Goal 4 in the seed concept) — v1 ships discovery/scoring with fixed, global criteria; per-user interest weighting needs the base engine proven first. See Open Question Q3.
- Advanced personalization / recommendation engine ("because you liked X, you may like Y") — needs enough user interaction history (bookmarks, clicks) to be worth building against, which only exists after v1 has been running.
- GitHub account integration (using a user's own stars/follows to personalize discovery) — requires GitHub OAuth and per-user data handling; that auth/privacy surface isn't justified until personalization itself is in scope.
- Trend forecasting (predicting future high-growth repositories, vs. v1's Goal 3 which only reports trends already observed) — a genuinely separate, harder modeling problem than descriptive trend detection.
- Semantic/intent-based search (e.g. "show me .NET libraries for agent orchestration") — v1 ships structural filter/sort (language, stars, topic, license); intent-based search needs its own retrieval approach layered on later.
- Repository-to-repository comparison — depends on the AI summarization pipeline being stable and trustworthy first; comparing unreliable summaries side-by-side would just double the unreliability.

**Excluded by product identity — no near-term plan to add:**
- Social features (comments, following other users, shared workspaces) — this is a discovery tool for an individual engineer's evaluation workflow, not a community platform; adding social surfaces would change what the product fundamentally is.
- Team workspaces / multi-user collaboration — v1 has no concept of an organization or shared account; saved/bookmarked state is per-user only.
- Browser extensions — a distinct distribution surface and codebase from the web dashboard; not worth maintaining two clients for v1.
- Delivery via Teams, Slack, Discord, or RSS — v1 digest delivery is email + web dashboard only; each additional channel is its own integration with its own auth and rate-limit model, not a variant of an existing one.

**Scoped down, not eliminated:**
- Repository cloning at scale — Phase 1 relies primarily on the GitHub API for discovery and metadata (see Constraints & Assumptions). Cloning an individual repository may still happen selectively (e.g. to read a manifest file the API doesn't expose directly), but routine/bulk cloning of every discovered repo is out of scope for v1 on cost and complexity grounds.

## User Stories

- As a software engineer, I want to discover newly-created repositories with strong quality and activity signals so that I find promising projects before they become mainstream.
- As a software engineer, I want AI-generated summaries of each discovered repository so that I don't have to read the full README or browse the codebase to decide if it's relevant to me.
- As a software engineer, I want to filter and sort discovered repositories by language, star range, topic, and license so that I can focus on the technologies I care about.
- As a software engineer, I want repository scores to account for license type, weekly commit activity, and community health (contributors, forks) so that I can trust a high score reflects a genuinely healthy, usable project — not just visibility.
- As a software engineer, I want a daily digest of top hidden gems and emerging trends so that I stay current without actively searching GitHub myself.
- As a software engineer, I want to save/bookmark repositories I'm interested in so that I can revisit them later.
- As an engineering leader or developer advocate, I want aggregated trend summaries (e.g. "MCP-related repos are growing") so that I can track where the ecosystem is heading without reading every individual repo.
- As a user, I want to browse the repository catalog through a single, focused Hidden Gems view so that I don't have to reconcile two near-identical lists, with each hidden gem's own category trend shown right on its card.

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
- **Scoring signal set (product-level commitment):** the "hidden gem" score must account for, at minimum: license presence/type, activity level measured specifically as commits per week (not a vaguer "recent activity" bucket), and community health via contributor count and fork count, alongside the previously captured popularity/quality signals. This granularity is a product decision, not an implementation detail — Architecture should design the scoring engine to expose these as identifiable, independently-weighted inputs.

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
| v2 | 2026-07-28 | Made license, weekly-commit activity, and community health (contributors/forks) explicit scoring signals in Goals, User Stories, and Constraints & Assumptions | Triage edit |
| v3 | 2026-07-28 | Elaborated Non-Goals with rationale per item, grouped into deferred-future, excluded-by-identity, and scoped-down categories | Triage edit |
| v4 | 2026-07-28 | Status → APPROVED | Gate approval |
| v5 | 2026-08-03 | US-8 narrowed: the standalone Categories browsing view was decommissioned (its content — Repository.PrimaryLanguage — is still fully accessible via the existing Discovery Feed/Hidden Gems Language filter, so no discovery capability was actually lost); User Stories now lists Discovery Feed, Hidden Gems, and Trending as the distinct views | Operator: "make category a filter and get rid of the category tab" |
| v6 | 2026-08-03 | US-8 narrowed again: the standalone Trending view was also decommissioned and merged into Hidden Gems — each repo's own category trend growth now shows directly on its card (no discovery capability lost, same rationale as v5's Categories removal) | Operator: "merge trending, add the trending score to the repo card on the hidden gems and then remove the trending tab as well" |
| v7 | 2026-08-03 | US-8 narrowed a third time: the standalone Discovery Feed view was decommissioned too — once Categories and Trending had already folded into it, Hidden Gems offered no distinct browsing experience left to differentiate it from Discovery Feed, so the catalog is now browsed through Hidden Gems alone | Operator: "Discovery Feed: remove it. there isnt much difference between that and the hidden gems." |

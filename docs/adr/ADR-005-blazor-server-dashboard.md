# ADR-005: Blazor Server for the Web Dashboard

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v1)

## Context

The PRD requires a web dashboard with several browsing views (Discovery Feed, Hidden Gems,
Trending, Categories), filter/sort, and bookmarking, that "feels interactive rather than
batch-oriented." The project is .NET (per the repo's existing `.gitignore`), solo-maintained, and
self-hosted (ADR-002). No separate frontend team or existing frontend stack constrains this choice.

## Decision

The web dashboard is built with Blazor Server, hosted from the same ASP.NET Core process as the
Web API.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Separate JS SPA (React/Angular) + .NET API | Adds a second language/toolchain, build pipeline, and API-contract-versioning surface for a solo maintainer to own, with no requirement in the PRD (offline support, huge client-side interactivity) that specifically needs it. |
| Blazor WebAssembly | Ships the whole app to the browser and needs a separate API auth boundary since it runs fully client-side; Blazor Server's persistent SignalR connection fits a single-user-per-session dashboard better and keeps data access server-side, next to the Data Store. |

## Consequences

- Single language (C#) end-to-end (crawler, scoring, summarizer, API, dashboard), which matters
  for a solo maintainer.
- Blazor Server requires a persistent SignalR connection per active user session — fine at
  expected usage (individual engineer(s) checking a personal dashboard), but a real constraint if
  usage ever grows toward many concurrent sessions (out of scope per PRD Non-Goals: no team/social
  features in v1).
- Self-hosted deployment (ADR-002) must keep sticky sessions / SignalR connectivity in mind if a
  reverse proxy or load balancer is later added in front of the app.

## Related

- Architecture section: 3. Components → Web Dashboard; 7. Technology Decisions
- Supersedes: none

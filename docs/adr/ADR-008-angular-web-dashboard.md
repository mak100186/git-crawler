# ADR-008: Angular SPA for the Web Dashboard

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v4)

## Context

ADR-005 picked Blazor Server for the dashboard on the assumption that no existing frontend stack
constrained the choice. That assumption was wrong — the operator's actual, standing preference is
Angular for frontend work. The functional requirements are unchanged from ADR-005's context: the
Discovery Feed, Hidden Gems, Trending, and Categories views, filter/sort, and bookmarking, in a
dashboard that "feels interactive rather than batch-oriented" (PRD).

## Decision

The web dashboard is built as an Angular SPA, consuming the Web API over HTTP, and served as
static assets from the same ASP.NET Core process that hosts the Web API (via static file
middleware, SPA fallback routing to `index.html`) — keeping the single-deployable-process
principle from ADR-002 rather than introducing a separate frontend container.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Blazor Server (ADR-005) | Superseded by this ADR — was chosen without the operator's actual frontend preference factored in. |
| Angular SPA behind its own nginx container | Would isolate the frontend build/serve concern from the API process, but adds a second container and a cross-origin (CORS) boundary between SPA and API for no benefit at solo-operator scale; same-process static serving keeps ADR-002's deployment footprint unchanged. |

## Consequences

- Introduces a second language/toolchain (TypeScript/Angular CLI) alongside the .NET backend,
  which ADR-005 had specifically avoided — now accepted as the tradeoff for matching the
  operator's actual stack preference.
- Web API (Architecture §3) must be a fully self-contained JSON API with no server-rendered view
  dependencies, since the dashboard now only talks to it over HTTP as a client.
- No SignalR/sticky-session concern (this was ADR-005's main scaling caveat) — the SPA is
  stateless from the server's perspective once served.
- The Angular build output must be produced and copied into the ASP.NET Core host's static file
  root as part of the build/Docker image process — a new build-pipeline step not previously
  needed.

## Related

- Architecture section: 3. Components → Web Dashboard; 7. Technology Decisions
- Supersedes: ADR-005

# ADR-015: Vertical Slice Architecture with CQRS (Wolverine), Applied Platform-Wide

> Status: ACCEPTED
> Date: 2026-07-28
> Architecture: docs/architecture.md (v10)

## Context

The Architecture defines components at a system-design level (Web API, Crawler, Scoring Engine,
Summarizer, Trend Aggregator, Digest Service) but had not specified an internal code-organization
pattern for any of them. The Web API has clearly distinct query types (Discovery Feed, Hidden
Gems, Trending, Categories, filter/sort — FR-004/FR-009) and command types (bookmark create/delete
— FR-007). Each background pipeline stage is triggered as a discrete unit of work by the Job
Scheduler (Hangfire, ADR-009). The operator wants one consistent CQRS/mediator pattern across the
whole platform, and explicitly ruled out MediatR — worth noting as a real factor rather than just
preference: MediatR's maintainer announced a move toward commercial licensing for its newer
versions, a cost/licensing risk that doesn't affect a fully open-source alternative.

## Decision

Every component — the Web API and each of the five background pipeline stages (Crawler, Scoring
Engine, Summarizer, Trend Aggregator, Digest Service) — is structured as Vertical Slice
Architecture, with a Wolverine message/handler pair as the unit of a slice (e.g. a
`DiscoverRepositories` command handled by a Wolverine handler for the Crawler stage, a
`ComputeScores` command for the Scoring Engine), instead of a shared service/repository layer.
Wolverine's in-process command bus (`IMessageBus.InvokeAsync`) replaces the role MediatR would
otherwise have played. Hangfire's `RecurringJob` / `ContinueJobWith` triggers (ADR-009) invoke a
Wolverine command per stage rather than calling a service method directly; Web API endpoints
dispatch Wolverine commands/queries per operation.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| MediatR | Explicitly ruled out by the operator; also carries a real licensing risk — its maintainer has moved newer versions toward a commercial license, unlike Wolverine which remains fully open-source with no paid tier for the core library. |
| MassTransit | A capable alternative with a similar open-source posture, but it's oriented around full message-bus/transport scenarios (RabbitMQ, Azure Service Bus, etc.); heavier than needed when v1 only needs in-process command/query dispatch, with no cross-process messaging requirement. |

## Consequences

- Every component is invoked through Wolverine's command bus, giving one consistent invocation
  and testing pattern platform-wide — including the background stages, where a Hangfire trigger
  now invokes a Wolverine command instead of calling a service method.
- Adds the Wolverine dependency and a slice-per-feature folder layout (e.g.
  `Features/Crawling/DiscoverRepositories/`) to every project, including the five background
  stages — a real up-front structuring cost even for single-purpose scheduled jobs that didn't
  strictly need a query/command distinction on their own.
- Cross-cutting concerns (retry/backoff for NFR-003, structured logging/metrics for NFR-005) are
  natural fits for Wolverine middleware — one middleware wraps every command/query platform-wide
  instead of being reimplemented per component.
- No licensing exposure from a mediator dependency going commercial later — Wolverine's core stays
  open-source, which was a deciding factor here, not just a style preference.
- New pipeline stages or Web API endpoints added in later phases follow the same slice pattern,
  keeping the codebase self-consistent as scope grows (e.g. a future personalization feature per
  PM-002).

## Related

- Architecture section: 2. High-Level Architecture; 3. Components (all); 7. Technology Decisions
- Supersedes: none

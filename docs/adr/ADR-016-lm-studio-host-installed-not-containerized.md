# ADR-016: LM Studio Runs Host-Installed, Not Containerized

> Status: ACCEPTED
> Date: 2026-08-01
> Architecture: docs/architecture.md (v11)

## Context

ADR-007 picked LM Studio as the local LLM runtime engine and framed it as "run as its own
container/process in the self-hosted deployment." F-003's initial scaffold implemented the
container path: a `lm-studio` service in `docker-compose.yml` running the `lmstudio/llmster-preview`
image — explicitly flagged at the time as a Technical Preview, CPU-only image with no stable GA
release.

The operator has since clarified that LM Studio is already installed and running as a native
desktop application on the machine this platform runs on. Containerizing a second copy is pure
duplication: it wastes the GPU/Metal acceleration a native LM Studio install can use (the
Technical Preview container is CPU-only), duplicates model storage, and adds a dependency on an
unstable preview image for no benefit when a working native install already exists.

## Decision

LM Studio runs as a host-installed native application, not a Docker container. The `app` container
reaches it over the network via `host.docker.internal` (Docker Desktop's standard host-loopback
alias) rather than a Compose service name. A `Makefile` at the repo root (`make up`) orchestrates
the previously-implicit "everything is one `docker compose up`" story across this now-split
topology: it checks Docker is running (starting Docker Desktop if not), brings up `docker compose`
(now just `app` + `postgres`), checks LM Studio's local server is running (starting it via the
`lms` CLI if not), and loads the configured model with the right context length/GPU settings before
declaring the stack ready.

This changes *how* LM Studio is deployed, not *that* it's LM Studio (ADR-007's engine choice is
unchanged) — this ADR amends ADR-007's deployment-topology framing and ADR-002's "all components
must be containerized" consequence; it does not supersede either.

## Alternatives Considered

| Option | Why not chosen |
|--------|-----------------|
| Keep the `lmstudio/llmster-preview` container (original F-003 scaffold) | CPU-only (no GPU/Metal acceleration), Technical Preview stability, and duplicates a model runtime the operator already has installed and configured natively — strictly worse on every axis once a native install is confirmed to exist. |
| Require the operator to always manually start LM Studio before `docker compose up` | Reintroduces exactly the "did you remember to start X" operational burden a Makefile / single-entrypoint script exists to remove — inconsistent with ADR-002's solo-operator, low-ops-overhead framing. |

## Consequences

- `docker-compose.yml` no longer has an `lm-studio` service; it manages `app` and `postgres` only.
- The `app` container's `LmStudio__BaseUrl` points at `http://host.docker.internal:1234` instead
  of a Compose service name. This requires `extra_hosts: host.docker.internal:host-gateway` on
  Linux hosts (Docker Desktop on Windows/macOS provides the alias natively) — see
  `docker-compose.yml`'s inline comment.
- `appsettings.json`'s default `LmStudio:BaseUrl` (used when running the API directly via
  `dotnet run`, outside Docker) is `http://localhost:1234`, matching LM Studio's documented default
  port; Compose overrides this via environment variable only when the API itself is containerized.
- **Operational dependency, not a technical one**: the app now depends on the operator having LM
  Studio installed and the `lms` CLI enabled (LM Studio Settings → Developer → "Enable CLI") before
  running `make up`. `docs/setup.md` documents this prerequisite explicitly.
- **Exact `lms` CLI flags used by the Makefile are best-effort, not independently verified against
  a live install in this environment** — same epistemic caveat as F-002's spike. The Makefile
  comments flag this and point at `lms --help` for the operator's installed version.
- F-003's original acceptance criterion ("Docker Compose brings up... postgres:18.4, and LM Studio
  together; health check confirms all three are reachable") no longer literally holds — amended in
  `docs/project-management.md` to describe the new two-process topology (Compose for app+postgres,
  Makefile for the LM Studio host check) rather than a single three-container Compose stack.

## Related

- Architecture section: 2. High-Level Architecture; 3. Components → Summarizer; 7. Technology
  Decisions
- Amends: ADR-007 (deployment topology only; engine choice unchanged), ADR-002 ("containerized from
  the start" consequence no longer applies to the LLM runtime specifically)
- Supersedes: none

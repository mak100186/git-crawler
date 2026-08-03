# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

GitHub Hidden Gems Discovery Platform — a self-hosted .NET 10 / Angular 22 modular monolith. Phase
0 (scaffolding) is complete; Phase 1 (data pipeline) has not started. See `docs/handoff.md` for the
current build state and what's next, and `docs/project-management.md` for the full feature backlog.

## Starting the stack — always use `make`, not `docker compose` directly

```bash
make up       # starting point for both the operator and Claude Code sessions in this repo
make status   # check what's currently running (Docker, Compose services, LM Studio)
make down     # stop docker compose (app + postgres); LM Studio on the host is left running
make logs     # tail the app container's logs
make help     # full target list
```

`make up` is the single entrypoint — never run `docker compose up` on its own. It checks Docker is
running (starting Docker Desktop if needed), brings up Compose (`app` + `postgres`), checks LM
Studio's host-installed local server is responding (starting it via `lms server start` if needed),
and loads the configured model via `lms load` before the stack is actually ready to serve
summaries. See `docs/setup.md` for one-time prerequisites (Docker Desktop, LM Studio + its `lms`
CLI, a `.env` file with `POSTGRES_PASSWORD`/`GITHUB_TOKEN`/`LMSTUDIO_MODEL`) and ADR-016 for why LM
Studio specifically is host-installed rather than containerized.

Requires a `make` binary and, on Windows, Git for Windows installed at its default location — the
`Makefile` forces its recipe shell to Git for Windows' bundled `bash.exe` there, so `make up` works
the same from PowerShell, `cmd.exe`, or Git Bash.

**Iterating on UI/backend code (e.g. a round of CSS/template fixes): use `make dev`, not `make
up`.** `make up` rebuilds the whole app image (Angular build + .NET publish + Docker image) on every
change, which is slow for a tight edit-verify loop. `make dev` starts only Postgres in Docker, then
prints the commands to run the backend (`dotnet watch run` in `src/backend/GitCrawler.Api`) and
frontend (`npm start` in `src/frontend`) bare — both hot-reload on save. The dashboard is then at
`http://localhost:4200/` (proxying `/api/*` to the bare backend on `:5073` via
`src/frontend/proxy.conf.json`), not `:8080`. Stop `make up`'s `app` container first if it's
running (`make down`) — otherwise it and the bare backend both process the same Postgres data. See
docs/setup.md §3a for the full explanation.

## Build / test commands

Backend (`src/backend/`):
```bash
dotnet build          # build
dotnet test           # run tests
dotnet format         # format
```

Frontend (`src/frontend/`):
```bash
npm run build          # production build
npm run test -- --watch=false   # run tests (Vitest)
npm run lint            # lint (ESLint via Angular CLI)
```

## Architecture

Modular .NET 10 monolith (one deployable process: API + served Angular dashboard), Vertical Slice
Architecture + CQRS via Wolverine (not MediatR) for every component. Full component breakdown,
technology decisions, and rationale (15 ADRs) live in `docs/architecture.md` and `docs/adr/` — read
those before making a structural or technology-choice change, and add/amend an ADR when you do
(see ADR-016 for a recent example of an amendment vs. a full supersession).

Governed docs (`docs/prd.md`, `docs/architecture.md`, `docs/project-management.md`, `docs/adr/`,
`docs/handoff.md`) are specs the code must satisfy, not side artifacts — keep them in sync with any
structural change in the same session, not as a follow-up.

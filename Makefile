# Single entrypoint for standing up the local stack (ADR-016): Docker Compose manages `app` +
# `postgres`; LM Studio runs host-installed (already on this machine, not containerized) and is
# checked/started/loaded here instead. `make up` is the operator-facing command - see
# docs/setup.md for the one-time setup this assumes (Docker installed, LM Studio installed with
# its CLI enabled, .env populated per .env.example).
#
# Requires a `make` binary and, on Windows, Git for Windows installed at its default location
# (macOS/Linux just need a Unix-like Terminal). These recipes themselves are written in Unix shell
# syntax; on Windows this file forces SHELL to Git for Windows' bundled bash.exe (see below) so
# `make up` works the same from PowerShell, cmd.exe, or Git Bash - not just Git Bash.
#
# Honesty note (same spirit as docs/spikes/f-002-lm-studio-throughput-benchmark.md): the exact
# `lms` CLI flags below are LM Studio's documented commands as of this Makefile's authoring, not
# independently verified against a live install in this environment. If a target fails with an
# "unknown flag" style error, run `lms --help` / `lms <subcommand> --help` for your installed
# version and adjust the recipe - don't assume the tool is broken.

.PHONY: help up dev down compose-up compose-down check-env check-docker check-lmstudio load-model stop-lmstudio logs status health

# On Windows, GNU Make normally picks its recipe shell by searching the invoking process's PATH
# for sh.exe. That search succeeds from a Git Bash session (which adds Git's own bin dirs to PATH
# on launch) but fails from a plain PowerShell/cmd.exe window - Git for Windows' installer does not
# add those dirs to the persistent system PATH by default - silently falling back to cmd.exe, which
# cannot parse these recipes' Unix syntax ('test', '[ ... ]', '{ ... }') and fails with "'test' is
# not recognized as an internal or external command". Point SHELL at Git for Windows' bundled
# bash.exe directly (its standard install path - Git Bash is already a hard prerequisite here, see
# docs/setup.md) so `make up` works the same from any Windows terminal, not just Git Bash -
# verified live via a clean-PATH cmd.exe subprocess reproducing the exact failure above, then
# succeeding after this override. Deliberately unconditional (no "does this path exist" probe): an
# `if exist`-style check runs into the same problem it's trying to solve - it needs to be written
# in the syntax of whichever shell Make already happens to be using at that point, which is exactly
# what's unknown here, and every workaround tried (an `if exist`/cmd-only probe, a `where`-based
# probe meant to be shell-agnostic) either broke Git Bash's own case or `where.exe`'s own argument
# parsing. If Git isn't installed at this path, this fails loudly and specifically (Make reporting
# it can't find/run the SHELL executable) rather than silently - an acceptable trade for a
# documented hard prerequisite. Doesn't fire on macOS/Linux (OS is only "Windows_NT" there).
ifeq ($(OS),Windows_NT)
SHELL := C:\Program Files\Git\bin\bash.exe
.SHELLFLAGS := -c
endif

# Load .env if present so its values (LMSTUDIO_MODEL, POSTGRES_DB, ...) actually take effect here,
# not just in docker-compose.yml's own separate .env support - a plain `.env` line has no effect
# on this file's recipes without this. `include` treats each KEY=value line as a Makefile
# variable; `export` makes those available to the shell recipes below (e.g. `lms load`).
ifneq (,$(wildcard ./.env))
    include .env
    export
endif

# LMSTUDIO_PORT/LMSTUDIO_IDENTIFIER/LMSTUDIO_MODEL and POSTGRES_DB/POSTGRES_USER/POSTGRES_PORT
# come from .env only (see .env.example, which is the single source of truth for their defaults)
# - deliberately no `?=` fallback here that would just re-hardcode a second copy of the same
# literal. check-env below fails fast with a clear message if any of them (or .env itself) is
# missing, instead of silently limping along on an empty value. Override any of them at the
# command line, e.g.: make up LMSTUDIO_MODEL=llama-3.2-1b-instruct - this still wins over .env.
#
# These are Makefile-only tuning knobs with no .env.example equivalent - defaulting them here is
# fine, there's only ever one place they're defined. APP_PORT matches docker-compose.yml's own
# fixed "8080:8080" port mapping (not env-driven there either - it's the one place that value is
# defined; this is just a second reference to it, not a second definition of it).
LMSTUDIO_CONTEXT_LENGTH ?= 8192
DOCKER_WAIT_SECS ?= 60
LMSTUDIO_WAIT_SECS ?= 30
APP_PORT ?= 8080

help:
	@echo "make up      - check Docker, start docker compose (app+postgres), check/start LM Studio, load the model"
	@echo "make dev     - fast local inner loop: Postgres in Docker + LM Studio on host, app run bare (no rebuild-per-change)"
	@echo "make down    - stop docker compose (app+postgres, or just postgres if 'make dev' was used); LM Studio on the host is left running"
	@echo "make status  - show whether Docker, Compose services, and LM Studio are up"
	@echo "make health  - probe every component's actual endpoint (dashboard, app /health, /api/ping, Postgres, LM Studio)"
	@echo "make logs    - tail the app container's logs"
	@echo ""
	@echo "Once 'make up' finishes, the web dashboard (F-011 - Discovery Feed, Hidden Gems,"
	@echo "Trending, Categories) is at http://localhost:$(APP_PORT)/ - it's the Angular build"
	@echo "served as static assets by the same app container, not a separate service/port."
	@echo ""
	@echo "'make up' rebuilds the whole app image on every change - fine for a demo/final check,"
	@echo "slow for active UI/backend iteration. Use 'make dev' instead while actively developing:"
	@echo "it only containerizes Postgres, and you run the backend/frontend bare so both hot-reload."
	@echo ""
	@echo "Override LMSTUDIO_MODEL, LMSTUDIO_PORT, LMSTUDIO_CONTEXT_LENGTH as needed, e.g.:"
	@echo "  make up LMSTUDIO_MODEL=<catalog-identifier>   (run 'lms ls' to see what's downloaded)"

# .env.example is the single source of truth for every default below - .env (copied from it) is
# the only place any of these values should ever need to be typed. Everything except
# POSTGRES_PASSWORD/GITHUB_TOKEN already ships with a working value in .env.example, so this check
# only ever bites on a genuinely missing/edited-out .env, not routine use.
check-env:
	@test -f .env || { \
		echo ".env not found. Copy .env.example to .env, then fill in POSTGRES_PASSWORD and"; \
		echo "GITHUB_TOKEN (see docs/setup.md) - every other value already ships with a working"; \
		echo "default in .env.example."; \
		exit 1; \
	}
	@test -n "$(LMSTUDIO_PORT)" || { echo "LMSTUDIO_PORT is not set in .env - see .env.example."; exit 1; }
	@test -n "$(LMSTUDIO_IDENTIFIER)" || { echo "LMSTUDIO_IDENTIFIER is not set in .env - see .env.example."; exit 1; }
	@test -n "$(LMSTUDIO_MODEL)" || { \
		echo "LMSTUDIO_MODEL is not set in .env (or was explicitly overridden empty). Run 'lms ls'"; \
		echo "to see what's downloaded in LM Studio, then either set it in .env (see"; \
		echo ".env.example) or run: make up LMSTUDIO_MODEL=<confirmed-identifier>"; \
		exit 1; \
	}
	@test -n "$(POSTGRES_DB)" || { echo "POSTGRES_DB is not set in .env - see .env.example."; exit 1; }
	@test -n "$(POSTGRES_USER)" || { echo "POSTGRES_USER is not set in .env - see .env.example."; exit 1; }
	@test -n "$(POSTGRES_PORT)" || { echo "POSTGRES_PORT is not set in .env - see .env.example."; exit 1; }

up: check-env check-docker compose-up check-lmstudio load-model
	@echo ""
	@echo "Stack is up:"
	@echo "  dashboard       -> http://localhost:$(APP_PORT)/ (Discovery Feed, Hidden Gems, Trending, Categories)"
	@echo "  app + postgres  -> docker compose (see 'make logs')"
	@echo "  LM Studio       -> host-installed, model '$(LMSTUDIO_MODEL)' loaded on port $(LMSTUDIO_PORT)"

# Fast inner loop for active development: only Postgres runs in Docker (the one piece that's
# genuinely infra, not app code); the backend and frontend run bare on the host so both get instant
# reload on save instead of a full `docker compose up -d --build` (Angular npm ci + build, .NET
# publish, image rebuild) per change. This isn't new plumbing - Program.cs already bridges .env into
# a `localhost:$$POSTGRES_PORT` connection string when it detects it's not running under Compose
# (see its top-of-file comments), and src/frontend/proxy.conf.json + angular.json's `serve` target
# already proxy /api/* to the backend's dev port - both exist for exactly this. `make dev` only adds
# the missing piece: starting Postgres (+ LM Studio, since summarization needs it) without also
# building/starting the `app` container, then pointing at the two commands to run it bare.
#
# Doesn't launch the backend/frontend itself: two long-running watch processes need two terminals
# for visible logs and a clean Ctrl+C each, which isn't something a single `make` recipe can give
# you portably - so this hands you the exact commands instead of backgrounding them itself.
dev: check-env check-docker
	@docker compose up -d postgres
	@echo "Waiting for Postgres to become healthy..."
	@i=0; \
	until docker compose ps postgres 2>/dev/null | grep -q healthy; do \
		i=$$((i+2)); \
		if [ $$i -ge $(DOCKER_WAIT_SECS) ]; then \
			echo "Postgres did not become healthy within $(DOCKER_WAIT_SECS)s. Check 'docker compose logs postgres'."; \
			exit 1; \
		fi; \
		sleep 2; \
	done
	@$(MAKE) check-lmstudio load-model
	@echo ""
	@echo "Dependencies are up (Postgres in Docker, LM Studio on host). Run these in two separate"
	@echo "terminals for the fast local dev loop - both hot-reload on save, no container rebuild:"
	@echo ""
	@echo "  backend:   cd src/backend/GitCrawler.Api && dotnet watch run --launch-profile http"
	@echo "  frontend:  cd src/frontend && npm start"
	@echo ""
	@echo "Dashboard  -> http://localhost:4200/ (Angular dev server; proxies /api/* to the backend)"
	@echo "Backend API-> http://localhost:5073/ (direct, no proxy)"
	@echo ""
	@echo "If the 'app' container from 'make up' is still running, stop it first (make down) -"
	@echo "otherwise it and the bare backend will both be processing the same Postgres data."
	@echo "When done: make down (stops whatever Compose has running - just postgres here)."

# --- Docker ---------------------------------------------------------------

check-docker:
	@command -v docker >/dev/null 2>&1 || { \
		echo "Docker is not installed or not on PATH. Install Docker Desktop: https://docs.docker.com/get-docker/"; \
		exit 1; \
	}
	@if docker info >/dev/null 2>&1; then \
		echo "Docker is already running."; \
	else \
		echo "Docker daemon not responding - attempting to start Docker Desktop..."; \
		case "$$(uname -s)" in \
			Darwin) open -a Docker ;; \
			MINGW*|MSYS*|CYGWIN*) \
				if [ -x "/c/Program Files/Docker/Docker/Docker Desktop.exe" ]; then \
					"/c/Program Files/Docker/Docker/Docker Desktop.exe" & \
				else \
					echo "Could not find Docker Desktop.exe in the default install path."; \
					echo "Start Docker Desktop manually, then re-run 'make up'."; \
					exit 1; \
				fi ;; \
			Linux) \
				if command -v systemctl >/dev/null 2>&1; then \
					sudo systemctl start docker; \
				else \
					echo "Start the Docker daemon manually (no systemd detected), then re-run 'make up'."; \
					exit 1; \
				fi ;; \
			*) echo "Unrecognized OS - start Docker manually, then re-run 'make up'."; exit 1 ;; \
		esac; \
		echo "Waiting up to $(DOCKER_WAIT_SECS)s for Docker to come up..."; \
		i=0; \
		while ! docker info >/dev/null 2>&1; do \
			i=$$((i+2)); \
			if [ $$i -ge $(DOCKER_WAIT_SECS) ]; then \
				echo "Docker did not become ready within $(DOCKER_WAIT_SECS)s. Check Docker Desktop and re-run 'make up'."; \
				exit 1; \
			fi; \
			sleep 2; \
		done; \
		echo "Docker is up."; \
	fi

compose-up:
	docker compose up -d --build

compose-down:
	docker compose down

down: compose-down

# --- LM Studio (host-installed, ADR-016) -----------------------------------

check-lmstudio: check-env
	@if curl -sf "http://localhost:$(LMSTUDIO_PORT)/v1/models" >/dev/null 2>&1; then \
		echo "LM Studio server already responding on port $(LMSTUDIO_PORT)."; \
	else \
		command -v lms >/dev/null 2>&1 || { \
			echo "LM Studio's 'lms' CLI is not on PATH."; \
			echo "Open LM Studio -> Settings -> Developer, and enable the CLI (exact wording may"; \
			echo "differ by version - see https://lmstudio.ai/docs for your installed version),"; \
			echo "then re-run 'make up'."; \
			exit 1; \
		}; \
		echo "Starting LM Studio's local server on port $(LMSTUDIO_PORT)..."; \
		lms server start --port $(LMSTUDIO_PORT); \
		echo "Waiting up to $(LMSTUDIO_WAIT_SECS)s for LM Studio to come up..."; \
		i=0; \
		while ! curl -sf "http://localhost:$(LMSTUDIO_PORT)/v1/models" >/dev/null 2>&1; do \
			i=$$((i+2)); \
			if [ $$i -ge $(LMSTUDIO_WAIT_SECS) ]; then \
				echo "LM Studio server did not respond within $(LMSTUDIO_WAIT_SECS)s."; \
				echo "Open LM Studio manually and check its server status, then re-run 'make up'."; \
				exit 1; \
			fi; \
			sleep 2; \
		done; \
		echo "LM Studio server is up."; \
	fi

load-model: check-env
	@if command -v lms >/dev/null 2>&1 && lms ps 2>/dev/null | grep -q "$(LMSTUDIO_IDENTIFIER)"; then \
		echo "Model already loaded as '$(LMSTUDIO_IDENTIFIER)'."; \
	else \
		echo "Loading $(LMSTUDIO_MODEL) (context length $(LMSTUDIO_CONTEXT_LENGTH), GPU offload max)..."; \
		lms load "$(LMSTUDIO_MODEL)" --identifier $(LMSTUDIO_IDENTIFIER) --context-length $(LMSTUDIO_CONTEXT_LENGTH) --gpu max -y; \
	fi

stop-lmstudio:
	@command -v lms >/dev/null 2>&1 && lms unload $(LMSTUDIO_IDENTIFIER) || echo "'lms' not found or model not loaded - nothing to stop."

# --- Diagnostics -------------------------------------------------------------

status:
	@echo "Docker:"; docker info >/dev/null 2>&1 && echo "  running" || echo "  not running"
	@echo "Compose services:"; docker compose ps 2>/dev/null || echo "  (compose not started)"
	@echo "LM Studio:"; curl -sf "http://localhost:$(LMSTUDIO_PORT)/v1/models" >/dev/null 2>&1 && echo "  running on port $(LMSTUDIO_PORT)" || echo "  not responding on port $(LMSTUDIO_PORT)"

# `status` above only checks whether processes/containers are running; `health` actually probes
# each component's own endpoint, matching docs/test-runbook.md's happy-path verify steps (TC-003-04
# and the F-002 spike's live checks) so there's one command for what that runbook otherwise has you
# run by hand. Prints every result (doesn't stop at the first failure) and exits non-zero if
# anything failed, so it's usable both interactively and as a script/CI gate.
health: check-env
	@status=0; \
	if curl -sf "http://localhost:$(APP_PORT)/health" >/dev/null 2>&1; then \
		echo "App /health          : OK"; \
	else \
		echo "App /health          : FAIL (is 'make up' running? see 'make logs')"; status=1; \
	fi; \
	if curl -sf "http://localhost:$(APP_PORT)/api/ping" >/dev/null 2>&1; then \
		echo "App /api/ping        : OK"; \
	else \
		echo "App /api/ping        : FAIL"; status=1; \
	fi; \
	if curl -sf "http://localhost:$(APP_PORT)/" | grep -qi "<app-root" >/dev/null 2>&1; then \
		echo "Dashboard (F-011)    : OK (http://localhost:$(APP_PORT)/)"; \
	else \
		echo "Dashboard (F-011)    : FAIL (Angular build not served from wwwroot - see docs/test-runbook.md F-011 TC-011-12)"; status=1; \
	fi; \
	if docker compose exec -T postgres pg_isready -U "$(POSTGRES_USER)" -d "$(POSTGRES_DB)" >/dev/null 2>&1; then \
		echo "Postgres             : OK"; \
	else \
		echo "Postgres             : FAIL"; status=1; \
	fi; \
	if curl -sf "http://localhost:$(LMSTUDIO_PORT)/v1/models" >/dev/null 2>&1; then \
		echo "LM Studio /v1/models : OK (port $(LMSTUDIO_PORT))"; \
	else \
		echo "LM Studio /v1/models : FAIL (port $(LMSTUDIO_PORT))"; status=1; \
	fi; \
	exit $$status

logs:
	docker compose logs -f app

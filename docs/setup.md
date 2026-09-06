# Local Setup: GitHub Hidden Gems Discovery Platform

> Last updated: 2026-08-04

One-time setup for running this platform locally via `make up`. See `docs/architecture.md` §7 and
ADR-016 for why the stack is split between Docker Compose (`app` + `postgres`) and a host-installed
LM Studio.

## Prerequisites

| Requirement                                                | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                       |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Docker Desktop                                             | `make up` will try to start it for you if it's installed but not running. Also required for `dotnet test`: the backend suite starts a real PostgreSQL container via Testcontainers (see `docs/test-runbook.md`). Install: https://docs.docker.com/get-docker/                                                                                                                                                                                                                               |
| LM Studio                                                  | Must already be installed on this machine (ADR-016 — not containerized). Install: https://lmstudio.ai/download                                                                                                                                                                                                                                                                                                                                                                              |
| LM Studio's `lms` CLI                                      | Bundled with LM Studio but may need enabling once — open LM Studio → Settings → Developer, and enable the CLI. Confirm it's on your `PATH` with `lms --version`.                                                                                                                                                                                                                                                                                                                            |
| `make`                                                     | Included on macOS/Linux. On Windows, install it separately (e.g. `choco install make`) — the `Makefile` itself forces its recipe shell to Git for Windows' bundled `bash.exe`, so it runs the same from PowerShell, `cmd.exe`, or Git Bash, as long as Git for Windows is installed at its default location.                                                                                                                                                                                |
| Llama 3.2 3B Instruct downloaded in LM Studio              | Run `lms ls` to check. If it's not there, use LM Studio's "Discover" tab (or `lms get <model>`) to download it. `llama-3.2-3b-instruct` is the identifier confirmed present as of this doc's last update (ADR-017 — chosen over the original Gemma 4 E4B pin after live testing found Gemma 4 E4B wasted most of its output budget on internal reasoning; see `docs/spikes/f-002-lm-studio-throughput-benchmark.md` §9-§10) — re-check with `lms ls`, since LM Studio's catalog can change. |
| `gitleaks` (optional — only needed for `make secret-scan`) | Not required for `make up`/`make dev`. Install via `go install github.com/gitleaks/gitleaks/v8@v8.30.1` or a downloaded release binary (https://github.com/gitleaks/gitleaks/releases/tag/v8.30.1) — pinned to match the version `.github/workflows/quality.yml`'s `secret-scan` job installs, so a local pass/fail means the same thing a CI run would. See §5 below.                                                                                                                      |

## 1. Create a GitHub Personal Access Token

The Crawler (F-005, not yet built) authenticates to GitHub's API with a PAT — not because private
data is needed (this platform only reads public repositories, per the PRD's Non-Goals), but because
an authenticated request gets a far higher API rate limit than an anonymous one (see
`docs/spikes/f-001-github-graphql-rate-limit-budget.md`).

**Recommended: fine-grained personal access token**

1. Go to **github.com → your profile photo (top right) → Settings**.
2. In the left sidebar, scroll down to **Developer settings**.
3. Click **Personal access tokens → Fine-grained tokens**.
4. Click **Generate new token**.
5. Fill in:
   - **Token name**: something identifiable, e.g. `git-crawler-local`.
   - **Expiration**: fine-grained tokens require an expiration (max 1 year) — pick a date you're
     willing to come back and rotate it.
   - **Resource owner**: your personal account.
   - **Repository access**: choose **Public Repositories (read-only)** — this platform never reads
     or writes anything in your own or your org's private repos.
   - **Permissions**: you should not need to grant any repository permissions beyond the read-only
     access "Public Repositories" already implies for public data (contents/metadata are publicly
     readable regardless). Leave the Permissions section at its defaults unless GitHub's UI
     specifically prompts you that a scope is required for a request you're making.
6. Click **Generate token** and copy it immediately — GitHub only shows it once.

**Fallback: classic personal access token** (if you hit a case the fine-grained flow doesn't cover
— fine-grained tokens have historically had narrower GraphQL API coverage than classic tokens for
some endpoints; verify this is still true for your GitHub account/version before switching)

1. **Settings → Developer settings → Personal access tokens → Tokens (classic)**.
2. **Generate new token (classic)**.
3. Name it, set an expiration.
4. **Do not select any scopes.** Reading public repository data doesn't require `public_repo` or
   any other scope — an unscoped classic token still gets the authenticated rate limit. Only add
   scopes later if a specific feature genuinely needs them (e.g. writing something back to GitHub —
   not part of this platform's v1 scope per the PRD).
5. Generate and copy the token.

## 2. Configure the token (and other config)

1. Copy `.env.example` to `.env` in the repo root: `cp .env.example .env` (this file is
   git-ignored — never commit it).
2. Open `.env` and set the values below. **`.env.example` is the single source of truth for every
   default** — neither `docker-compose.yml`, the `Makefile`, nor `Program.cs`'s bare `dotnet run`
   bridge re-hardcodes a fallback copy of any of these values, so there's exactly one place to
   change if a default ever needs to change. `docker-compose.yml`'s `${VAR:?...}` guards and the
   Makefile's `check-env` target both fail loudly, pointing back at `.env.example`, if a value is
   missing — nothing silently falls back to a guessed default anymore. **Every value here drives
   both run modes** — Docker Compose (via `docker-compose.yml`'s own `.env` handling, translated to
   hierarchical `Section__Key` env vars there) and a bare `dotnet run` outside Docker (via
   `Program.cs`, which loads `.env` through the `DotNetEnv` package and bridges each flat name to
   its config key). One `.env`, both run modes; no separate `dotnet user-secrets` setup needed.
   Only `POSTGRES_PASSWORD` and `GITHUB_TOKEN` ship blank below — every other value already has a
   working default, so `cp .env.example .env` plus those two real values is enough to run `make up`.

   | `.env` variable       | Config key it drives                    | Notes                                                                                                                                                                                              |
   | --------------------- | --------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
   | `POSTGRES_DB`         | `ConnectionStrings:Postgres` (database) | Ships as `gitcrawler` — required by `docker-compose.yml`/`make check-env`, only change alongside those files.                                                                                      |
   | `POSTGRES_USER`       | `ConnectionStrings:Postgres` (username) | Ships as `gitcrawler` — same as above.                                                                                                                                                             |
   | `POSTGRES_PASSWORD`   | `ConnectionStrings:Postgres` (password) | Any password for local dev; ships blank — `docker-compose.yml` fails loudly if missing.                                                                                                            |
   | `POSTGRES_PORT`       | _(host-published port only)_            | Port Postgres is reachable at from the host (DB client, or the app itself in bare mode) — the app container always talks to `postgres:5432` internally regardless of this value. Ships as `5432`.  |
   | `GITHUB_TOKEN`        | `GitHub:Token`                          | Paste the token from step 1. Ships blank — fails loudly if missing.                                                                                                                                |
   | `LMSTUDIO_PORT`       | `LmStudio:BaseUrl`                      | Port LM Studio's local server listens on. Ships as `1234` (LM Studio's own default).                                                                                                               |
   | `LMSTUDIO_IDENTIFIER` | `LmStudio:Model`                        | The fixed alias `make up` assigns the loaded model via `lms load --identifier` — what the app sends as `"model"` in LM Studio API calls. Ships as `gitcrawler-summarizer`; rarely needs to change. |
   | `LMSTUDIO_MODEL`      | _(Makefile only, not app config)_       | The catalog model `make up` loads under the identifier above (ADR-017). Ships as `llama-3.2-3b-instruct`.                                                                                          |

   `ConnectionStrings:Postgres` isn't consumed by anything yet — no `DbContext` exists until F-004
   — but it's fully wired now so F-004 doesn't also have to solve config sourcing.

## 3. Bring the stack up

```bash
make up
```

This:

1. Checks `.env` exists and has every required variable set (`check-env`) — fails fast with a
   pointer back to `.env.example` if not.
2. Checks Docker is running (starts Docker Desktop if it's installed but not running).
3. Runs `docker compose up -d --build` for `app` + `postgres`.
4. Checks LM Studio's local server is responding on port 1234 (starts it via `lms server start` if
   not).
5. Loads the configured model with the right context length and GPU offload settings.

The default model is `llama-3.2-3b-instruct` (ADR-017), read from `.env`'s `LMSTUDIO_MODEL` — no
flag needed for the default case, since `.env.example` already ships this value. Override on the
command line if you want a different model (this wins over `.env`):

```bash
make up LMSTUDIO_MODEL=<identifier>
```

Run `lms ls` first if you're not sure of the exact identifier for what you have downloaded.

## 3a. Faster inner loop for active development

`make up` rebuilds the whole app image (Angular `npm ci`+build, .NET publish, Docker image build)
on every change — fine for a demo or final check, slow if you're iterating on UI or backend code.
For active development, run:

```bash
make dev
```

This starts Postgres and Mailpit in Docker plus LM Studio on the host, and prints the two commands to
run the backend and frontend bare, each in its own terminal, so both hot-reload on save instead of
waiting on a container rebuild:

```bash
# terminal 1
cd src/backend/GitCrawler.Api && dotnet watch run --launch-profile http

# terminal 2
cd src/frontend && npm start
```

The dashboard is then at `http://localhost:4400/` (Angular's own dev server — `ng serve`), which
proxies every `/api/*` call through to the backend at `http://localhost:5073/`
(`src/frontend/proxy.conf.json`, wired into `angular.json`'s `serve` target). The backend itself
picks up `localhost:$POSTGRES_PORT` for its connection string when it detects it isn't running
under Compose (see the top of `Program.cs`) — no separate config needed, it reads the same `.env`
`make up` does.

If `make up`'s `app` container is still running from an earlier session, stop it first
(`make down`) — otherwise it and the bare backend end up processing the same Postgres data
concurrently (duplicate crawls, duplicate Hangfire jobs).

**Testing the F-013 digest email locally**: `appsettings.Development.json` points `Smtp:Host` at
`localhost:1025` — Mailpit's SMTP port, published by `make dev` alongside Postgres — so a bare
`dotnet watch run` sends real digest emails there instead of needing a live mailbox. Open
`http://localhost:8025/` to see what `SendDigestCommand` actually composed and sent (subject, top
hidden gems, trend summaries) — Mailpit catches everything sent to it and never delivers anywhere
external, so it's safe to trigger the digest repeatedly while iterating. Mailpit is the actively-
maintained successor to MailHog (which has seen no meaningful releases since 2022); same idea, still
developed. Both `make up` and `make dev` start it.

**The digest under `make up`**: `.env.example` ships `SMTP_HOST=mailpit`, `SMTP_PORT=1025`,
`SMTP_ENABLE_SSL=false`, so the containerized app delivers the digest into that same Mailpit — open
`http://localhost:8025/` to read it. `mailpit` is the Compose service's DNS name and is the address
that matters here: inside the app container `localhost` is the app itself, not Mailpit, which is why
`appsettings.Development.json`'s `localhost:1025` is correct for `make dev`'s bare backend and wrong
for the container.

To send to a real mailbox, point `SMTP_HOST`/`SMTP_PORT`/`SMTP_ENABLE_SSL` at your provider, set
`SMTP_USERNAME`/`SMTP_PASSWORD` (an app password, not the account password), and set
`SMTP_FROM_ADDRESS`/`DIGEST_RECIPIENT_EMAIL` to real addresses. `docker-compose.yml` passes all seven
through to the app container as `Smtp__*`/`Digest__RecipientEmail`, and `Program.cs` bridges the same
flat names for a bare `dotnet run`. Blank `SMTP_HOST`, `SMTP_FROM_ADDRESS`, or
`DIGEST_RECIPIENT_EMAIL` means the 06:00 UTC job runs, logs a warning, and skips. `appsettings.json`
ships the `Smtp` section blank on purpose and holds no `Password` key at all; the password only ever
comes from the environment, the same as `GitHub:Token`.

When nothing arrives: editing `.env` does not affect a container that is already running — env vars
are fixed at create time, so `make down && make up`. A send failure shows as a **Failed** job in the
Hangfire dashboard carrying the SMTP error, and Hangfire retries it; `make logs` has the full
exception. Mailpit keeps captured mail in `./data/mailpit` (git-ignored), so it survives a restart.

## 4. Verify

```bash
make health
```

Probes every component's actual endpoint (app `/health`, `/api/ping`, Postgres via `pg_isready`,
LM Studio's `/v1/models`) and exits non-zero if any of them failed — unlike `make status`, which
only reports whether the underlying processes/containers are running, not whether they're actually
answering requests. Equivalent manual commands, if you want to check one component at a time:

```bash
curl http://localhost:8080/health              # expect: Healthy
curl http://localhost:8080/api/ping            # expect: {"status":"ok",...}
curl http://localhost:1234/v1/models           # expect: your loaded model listed
pg_isready -h localhost -p 5432 -U gitcrawler  # expect: accepting connections (use POSTGRES_PORT if changed)
```

## 5. Scanning for secrets before you push

`.github/workflows/quality.yml`'s `secret-scan` job runs [gitleaks](https://github.com/gitleaks/gitleaks)
against every pull request and push to `main`/`master` (NFR-002) — it scans the full git commit
history plus the current working-tree diff for accidentally-committed API keys, tokens, and
credentials, and fails the workflow on any finding. Run the identical check locally, before pushing,
with:

```bash
make secret-scan
```

This requires `gitleaks` on your `PATH` (see the Prerequisites table above) — it isn't installed or
invoked automatically the way Docker/LM Studio are for `make up`, since it's optional tooling for a
pre-push check rather than something the running stack itself depends on. Config comes from
`.gitleaks.toml` at the repo root (extends gitleaks' own default ruleset; currently has no allowlist
entries — this repo's scan is clean as of this doc's last update). If gitleaks ever does flag a
genuine false positive (e.g. an example/placeholder value in a doc or config fixture), add a
narrowly-scoped, commented allowlist entry to `.gitleaks.toml` rather than disabling the rule
outright.

## Tearing down

`make down` stops the Docker Compose services (`app` + `postgres`). LM Studio is left running on
the host — it's your machine's own application, not something this project should stop out from
under you. Use `make stop-lmstudio` if you specifically want to unload the model this Makefile
loaded.

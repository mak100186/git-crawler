# Code Review — GitCrawler (Tranche v1)

**Reviewed at:** commit `54fbfa1` (branch `main`)
**Date:** 2026-09-05
**Scope:** full codebase — `src/backend/` (8,854 LOC excl. migrations), `src/frontend/`, Docker/Compose, CI, and configuration.

## Verification performed

Every claim below was checked against the code. The following commands were run before writing:

| Command                       | Result                          |
| ----------------------------- | ------------------------------- |
| `dotnet build GitCrawler.sln` | 0 errors, 0 warnings            |
| `dotnet test`                 | 142 passed, 0 failed, 0 skipped |
| `npm run lint`                | All files pass linting          |
| `npm test -- --watch=false`   | 45 passed across 11 files       |

The build and both suites are genuinely green. Nothing in this review is a broken build; the findings are about behaviour under conditions the suites do not exercise.

## Summary

| Severity | Count | Theme                                                                                                                                                            |
| -------- | ----- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| High     | 5     | Config/comment drift, a dead feature in the shipped deployment, whole-table loads in the pipeline, an untested production code path, unauthenticated job control |
| Medium   | 8     | Retry safety, N+1, missing resilience on one GitHub call, error contract, unvalidated input, a frontend race, container hardening, unbounded data growth         |
| Low      | 11    | Efficiency, consistency, and hygiene items                                                                                                                       |

**Remediation status (2026-09-05):** 15 of the 24 findings were fixed in the pass recorded as
changelog revision 19 — H-1, H-2, H-5, M-1, M-2, M-4, M-5, M-6, M-7, L-2, L-5, L-7, L-8, L-9, L-10.
The nine that remain open (H-3, H-4, M-3, M-8, L-1, L-3, L-4, L-6, L-11) are the ones that need
design work rather than a bounded edit: the pipeline rewrite, the PostgreSQL integration suite,
score retention, a shared resilience pipeline, a facets endpoint, and two product decisions. Each
finding below carries its own status line.

The dominant pattern: **F-017's scalability work fixed the read path thoroughly and correctly, but the write-path pipeline stages were left on the pre-F-017 "load everything into memory" shape.** The dashboard scales to 100k repositories; the jobs that populate it do not.

The second pattern: **several carefully reasoned code comments no longer match the shipped configuration.** The comments were right when written. `appsettings.json` moved and the comments did not.

---

## High

### H-1 — `Summarization:BatchSize` ships at 200, but the code and its comment are built around 20

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`src/backend/GitCrawler.Api/appsettings.json:26` sets `"BatchSize": 200`. The fallback in `GenerateSummariesCommandHandler` (`Features/Summarization/GenerateSummaries/GenerateSummariesCommand.cs:48`) is `20`, and the comment above it justifies that number explicitly:

> "20 keeps a single run's worst case in the low minutes even at several seconds/repo."

`appsettings.json` wins, so the effective batch is 200 — a 10× increase over the value the safety reasoning was based on. Each repository costs **two sequential LM Studio calls** (`LmStudioRepositorySummarizer.SummarizeAsync`, lines 109-110), which are deliberately not parallelised. At 200 repositories that is 400 sequential local-inference calls in one run.

This collides with two other settings:

- `Hangfire:SummarizationCronSchedule` is `"0 * * * *"` — hourly.
- `GenerateSummariesJob:41` carries `[DisableConcurrentExecution(timeoutInSeconds: 30 * 60)]`.

If a run exceeds 30 minutes, the next hourly trigger blocks on the distributed lock, waits out the 30-minute timeout, and then throws `DistributedLockTimeoutException` — a failed job plus Hangfire's default automatic retries, each repeating the same wait. At 400 sequential inference calls, a per-call latency above roughly 4.5 seconds is enough to cross that line.

I did **not** measure actual inference latency on your hardware, so I cannot tell you whether you are already over the threshold. What I can tell you is that the margin the comment claims does not exist at the shipped value.

**Recommendation:** decide which number is right and make one follow the other. If 200 is intentional, update the comment, raise the `DisableConcurrentExecution` timeout above the realistic worst case, and consider moving summarization off an hourly schedule. If 20 is right, fix `appsettings.json`. Either way, watch the run duration — the observability middleware already emits `ElapsedMs`, so this is a matter of reading it, not building it.

### H-2 — The daily digest cannot send in the `make up` deployment

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`SendDigestCommandHandler` requires `Digest:RecipientEmail`; `SmtpEmailSender` requires `Smtp:Host` and `Smtp:FromAddress`. In the shipped production path all three are empty:

- `appsettings.json` ships `Smtp.Host`, `Smtp.FromAddress`, and `Digest.RecipientEmail` blank.
- `docker-compose.yml` passes no `Smtp__*` or `Digest__RecipientEmail` environment variable to the `app` service — only `ConnectionStrings__Postgres`, `GitHub__Token`, and the two `LmStudio__*` values.
- `.env.example` defines no SMTP variables at all, so there is nothing for Compose to interpolate even if it tried.

Only `appsettings.Development.json` configures them, pointing at the Mailpit container — which sits behind `profiles: ["dev"]` and is therefore never started by `make up`.

The consequence is not a crash. `SendDigestCommandHandler` degrades gracefully by design and logs `"Digest:RecipientEmail is not configured; skipping the daily digest send."` — so the job runs at 06:00 UTC every day, skips, and reports success. The README advertises the daily digest as a shipped feature; in the documented deployment it is permanently inert, and quietly so.

**Recommendation:** add `SMTP_HOST`, `SMTP_PORT`, `SMTP_USERNAME`, `SMTP_PASSWORD`, `SMTP_FROM_ADDRESS`, and `DIGEST_RECIPIENT_EMAIL` to `.env.example`, bridge them in `docker-compose.yml` as `Smtp__Host` etc. (and in `Program.cs`'s flat-name bridge for the bare `dotnet run` path), and raise the skip log to something the operator actually notices — or document in the README that the digest requires manual SMTP configuration.

### H-3 — Three of the four pipeline stages load whole tables into memory

**Status: open.**

F-017 rewrote the read path to push `ORDER BY` and `LIMIT/OFFSET` into SQL. The write path was not touched. Three handlers still materialise their entire working set:

| Handler                                 | Query                                                                | Loaded                                                                   |
| --------------------------------------- | -------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `ComputeScoresCommandHandler:44-46`     | `Repositories.Include(r => r.Scores).ToListAsync()`                  | **Every repository and every score row in the database**                 |
| `GenerateSummariesCommandHandler:67-70` | `Repositories.Include(r => r.Scores).Where(r => !r.Summaries.Any())` | Every unsummarised repository with its full score history                |
| `SendDigestCommandHandler:82`           | `RepositoryCardQuery.IncludeForCards(eligible).ToListAsync()`        | Every scored+summarised repository with scores, summaries, and bookmarks |

`ComputeScoresCommandHandler` is the worst: it has no `Where` clause at all. It loads the entire `Repositories` table joined to the entire `Scores` table, then filters in LINQ-to-Objects to decide which repositories need re-scoring. The comment acknowledges this ("revisit if the Repositories table grows large enough for that to matter") — that condition has arrived, because F-017 built and measured against a 100k-repository seed set.

This compounds with M-8 below: `Scores` is append-only, one row per repository per re-crawl. At 100k repositories re-crawled daily for a year, `ComputeScoresCommandHandler` would attempt to materialise roughly 36.5M rows into process memory in a single query.

The summarizer's version is subtler but the same shape: it loads every unsummarised repository purely to sort them by score and take a batch. On a fresh database — or after any period where summarization falls behind — that is most of the table, to select twenty rows.

**Recommendation:** push the work into SQL, the same way F-017 did for the read path. For scoring, the "needs re-scoring" predicate is expressible as a correlated subquery (`!r.Scores.Any(s => s.ComputedAtUtc >= r.LastCrawledAtUtc)`) — the composite index on `(RepositoryId, ComputedAtUtc DESC)` added by F-017 already supports it. For the summarizer, apply `ApplySort(..., Score, Desc).Take(batchSize)` before materialising. Both should also process in batches with `SaveChangesAsync` per batch rather than accumulating a whole run in the change tracker.

Note the constraint this runs into: the SQLite test provider cannot translate `DateTimeOffset` in `ORDER BY`, which is exactly why these handlers resolve "latest score" in memory. That constraint is the real blocker, and it is the same one behind H-4.

### H-4 — The production sort/pagination path has no automated test coverage

**Status: open.**

`GetHiddenGemsQueryHandler:112` branches on the runtime provider:

```csharp
var isSqlite = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
```

SQLite takes the client-side `IncludeForCards → Rank → Paginate` path. Everything else takes the server-side `ApplySort → Skip → Take` path.

Every test in the suite uses SQLite — verified across all ten handler test fixtures, each calling `new DbContextOptionsBuilder<GitCrawlerDbContext>().UseSqlite(_connection)`. So all 26 `GetHiddenGemsQueryHandlerTests` exercise the fallback branch. **The server-side branch — the one that actually runs in production, and the entire deliverable of F-017 — is never executed by a test.**

The two paths are not equivalent implementations of one algorithm. The server-side path relies on Npgsql translating a correlated subquery inside `ORDER BY`, plus `LIMIT/OFFSET` interacting correctly with the `ThenBy(r.Id)` tie-break. A translation regression, an ordering difference in how PostgreSQL sorts null score values versus LINQ's `?? 0.0` fallback, or an off-by-one in the offset arithmetic would all pass CI.

The same gap covers the EF Core migrations: tests use `EnsureCreated`, so the migration chain — including the GIN index on `Topics` and the `INCLUDE` columns on the `Score` composite index, both PostgreSQL-only — is never applied by a test.

`Program.cs:354` exposes `public partial class Program` with the comment _"so integration tests can bootstrap the app via WebApplicationFactory&lt;Program&gt; in later features."_ No such test was ever written. There are no integration tests and no E2E tests in the repository.

**Recommendation:** add a small integration suite against a real PostgreSQL instance — Testcontainers, or a Compose-provided database in CI — covering at minimum: each of the four sort fields in both directions, pagination boundaries including the beyond-last-page case, and the migration chain applying cleanly to an empty database. Once that exists, the `isSqlite` branch can be deleted and the handler simplified, which also removes the portability constraint blocking H-3.

### H-5 — Unauthenticated job control, published on all network interfaces

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`Program.cs:255-258` mounts the Hangfire dashboard with authorization explicitly disabled:

```csharp
app.UseHangfireDashboard("/hangfire", new DashboardOptions { Authorization = [] });
```

The comment documents why (a prior shared-secret filter broke the dashboard's own asset and polling requests) and recommends restricting exposure at the network layer instead. That mitigation was not applied: `docker-compose.yml:24` publishes `"8080:8080"` and line 62 publishes `"${POSTGRES_PORT}:5432"`, both of which bind `0.0.0.0` — every interface on the host.

The dashboard is not read-only. It permits triggering recurring jobs immediately, deleting and re-queueing jobs, and inspecting job arguments and failure stack traces. Anyone who can route to the host — anyone on the same LAN, VPN, or coffee-shop Wi-Fi — has full control of the pipeline and a view of its internals, with no credential. PostgreSQL is directly reachable on the same terms, protected only by `POSTGRES_PASSWORD`.

For a laptop-only deployment this is low-consequence. It stops being low-consequence the moment the machine joins any untrusted network, and nothing in the setup prevents that.

**Recommendation:** the one-line fix is to bind both published ports to loopback — `"127.0.0.1:8080:8080"` and `"127.0.0.1:${POSTGRES_PORT}:5432"`. This costs nothing (the operator reaches both from the host anyway; the app container reaches Postgres over the internal Compose network regardless) and closes the whole exposure. If remote access is ever wanted, put it behind a reverse proxy with authentication rather than reinstating a query-key filter.

---

## Medium

### M-1 — Rate-limit retries are unbounded and can spin with no delay

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`DiscoverRepositoriesCommandHandler:162-178` configures the rate-limit pathway with `MaxRetryAttempts = int.MaxValue` and no minimum delay. Two paths lead to a zero-delay retry:

1. `ResetDelay` (lines 198-202) returns `TimeSpan.Zero` whenever `resetAtUtc` is not in the future. A stale or clock-skewed reset timestamp yields immediate retry.
2. The `DelayGenerator` switch (lines 166-172) ends in `_ => TimeSpan.Zero`. Any future `GitHubRateLimitException` subtype that is not one of the three handled cases retries instantly and forever.

Combined: unbounded attempts × zero delay = a tight loop hammering the GitHub API, inside a job whose only bound is `[DisableConcurrentExecution(timeoutInSeconds: 3600)]` — which is a lock-acquisition timeout, not an execution timeout, so it does not stop the running job.

The `OnRetry` handler logs each attempt, so this would be visible in the logs rather than silent. That is the saving grace, not a fix.

**Recommendation:** floor the delay (`Math.Max(wait, TimeSpan.FromSeconds(30))`), give the pathway a generous but finite attempt cap, and make the `_ =>` fallback a sensible non-zero default rather than `Zero`.

### M-2 — N+1 query in the crawl loop

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`DiscoverRepositoriesCommandHandler:76` issues one `SingleOrDefaultAsync` per discovered repository, inside the `foreach` over each page:

```csharp
var existing = await dbContext.Repositories.SingleOrDefaultAsync(r => r.GitHubId == discovered.GitHubId, ct);
```

At the configured page size of 50, that is 50 database round-trips per page, plus one `SaveChangesAsync`.

**Recommendation:** load the page's existing rows in one query before the loop — collect the page's `GitHubId` values, then `Where(r => ids.Contains(r.GitHubId)).ToDictionaryAsync(r => r.GitHubId)` — and look up from the dictionary. One query per page instead of fifty, and the unique index on `GitHubId` already supports it.

### M-3 — The README fetch bypasses the resilience pipeline built for the same API

**Status: open.**

ADR-018 wrapped the crawler's GitHub calls in a Polly pipeline that handles primary rate limits, secondary rate limits, and transient failures. `GenerateSummariesCommandHandler.TryFetchReadmeAsync:147-174` calls the _same GitHub REST API_, on the _same shared rate-limit budget_, through the _same named HttpClient_ — with no pipeline at all. It handles `404` and then calls `EnsureSuccessStatusCode()`.

A `403` rate-limit response therefore throws, is caught by the per-repository handler at line 115, and is logged as a summarization failure. With `BatchSize` at 200 (H-1), a rate-limited window burns through up to 200 repositories in one run, logging 200 failures for a single root cause, then repeats the next hour.

**Recommendation:** extract the crawler's `BuildResiliencePipeline` into something both slices can use, or at minimum detect a rate-limit response here and abort the batch cleanly rather than failing each repository individually.

### M-4 — Bookmarking a nonexistent repository returns 500, and there is no global error handler

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`CreateBookmarkCommandHandler:51-53` inserts a `Bookmark` without checking that `command.RepositoryId` refers to a real repository. The foreign key rejects it, EF Core raises `DbUpdateException`, and — because `Program.cs` registers no `UseExceptionHandler` and no `AddProblemDetails` — it surfaces as an unhandled 500.

`POST /api/repositories/999999/bookmark` should be a 404. It is a 500 with, in Development, a full stack trace.

More broadly: no endpoint in the application has an error contract. Any handler exception is a bare 500.

**Recommendation:** add `builder.Services.AddProblemDetails()` and `app.UseExceptionHandler()` for a consistent RFC 7807 contract, and have `CreateBookmarkCommandHandler` check repository existence and return a result the endpoint can map to 404.

### M-5 — Array filter parameters are unbounded

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`GetHiddenGemsEndpoint:11-22` binds `string[]? language`, `string[]? topic`, and `string[]? license` with no cap on element count or string length. Each becomes an element of a SQL `IN (...)` list (`RepositoryCardQuery.ApplyFilters`). A request with ten thousand `language=` parameters produces a ten-thousand-element `IN` clause.

`page` and `pageSize` are properly clamped (`ClampPage`/`ClampPageSize`, max 100) — the scalar parameters were thought about; the array ones were not. On an endpoint with no authentication (H-5), that asymmetry matters.

**Recommendation:** cap each array at a sane length (say 50) and reject or truncate beyond it, in the same spirit as the existing `MaxPageSize`.

### M-6 — The dashboard can render stale results

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`hidden-gems.ts:73-88` calls `this.repositoryApi.getHiddenGems(...).subscribe(...)` directly on every filter change, sort change, and page change. Nothing cancels the previous request.

Two requests in flight can complete out of order, so a slow response to an older filter overwrites the newer one. The UI then shows results that do not match the visible filter state — and because `loading` is set false by whichever finishes last, there is no indication anything is wrong. Rapidly toggling language filters is enough to trigger it.

The subscriptions are also never unsubscribed (no `takeUntilDestroyed`), so an in-flight request writes to signals after the component is destroyed.

**Recommendation:** drive fetches through a `Subject` piped with `switchMap`, which cancels the previous request automatically, and add `takeUntilDestroyed(inject(DestroyRef))`. Given this is Angular 22 with signals throughout, `httpResource` or `rxResource` would handle both concerns plus the loading/error state in one construct.

### M-7 — The container runs as root

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`src/backend/Dockerfile:34` bases the runtime stage on `mcr.microsoft.com/dotnet/aspnet:10.0` and never sets a `USER`. The process runs as root inside the container.

Nothing else in the image needs root at runtime — the app binds port 8080 (non-privileged) and writes nothing to disk.

**Recommendation:** add `USER $APP_UID` after the `COPY`. The .NET base images define `APP_UID` precisely for this, so it is a one-line change with no other adjustment needed.

### M-8 — Score history grows without bound and nothing prunes it

**Status: fixed** in the remediation pass recorded as changelog revision 21. `ComputeScoresCommandHandler.PruneScoreHistoryAsync` runs at the end of every scoring run and deletes everything past the `Scoring:ScoreHistoryRetentionCount` most recent rows per repository (default 10, clamped to a floor of 2 so TrendGrowth keeps its comparison row). One window-function statement, one round trip; run inline rather than as its own recurring job because this handler is the only thing that adds `Score` rows.

`ComputeScoresCommandHandler` appends a new `Score` row on every re-crawl by design — that history is what `GetHiddenGemsQueryHandler` reads to compute per-repository trend growth. But it only ever reads the **latest two rows**, and nothing deletes the rest.

At the daily crawl schedule, each repository accrues 365 score rows a year, of which two are ever used. This is the multiplier behind H-3's memory problem and L-1's over-fetch.

**Recommendation:** add a retention step — either a periodic job that keeps the N most recent rows per repository, or a rolling window keyed on `ComputedAtUtc`. The composite index on `(RepositoryId, ComputedAtUtc DESC)` makes either cheap to implement.

---

## Low

### L-1 — `GetHiddenGems` fetches full score history for every repository on the page

**Status: fixed** in the remediation pass recorded as changelog revision 21 — by giving the estimate a mechanism rather than by rewriting the query. M-8's retention caps history at the configured count per repository, so the worst case is genuinely that count × `MaxPageSize` (default ~1000 rows) instead of one row per crawl since the repo was first seen. The comment now states the bound and names the setting that controls it.

`GetHiddenGemsQueryHandler:157-161` loads **all** `Score` rows for the page's repositories and then uses `scores[0]` and `scores[1]`. The comment estimates "~10 rows per repo × ≤100 repos = ~1000 rows max" — that estimate has no mechanism behind it (see M-8). After a year of daily crawls it is ~36,500 rows fetched per page request to read 200 of them.

**Recommendation:** fetch only what is used — two rows per repository via a lateral join, or restrict by `ComputedAtUtc` to a recent window.

### L-2 — `GitHubDiscoveryClient` uses `DateTimeOffset.UtcNow` while everything around it uses `TimeProvider`

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`TimeProvider` is injected into every handler specifically so tests can control the clock. `GitHubDiscoveryClient` bypasses it in two places: line 159 (`BuildSearchQuery`'s lookback window) and line 241 (`IsRestSecondaryRateLimited`'s `Retry-After` date calculation). Both are untestable as written, and the second feeds a Polly delay.

**Recommendation:** inject `TimeProvider` here too — it is already a registered singleton.

### L-3 — Observability middleware: a leakable static dictionary and uncached reflection

**Status: fixed** in the remediation pass recorded as changelog revision 21. The property scan is resolved once per result type into a cached `Func<object, int>`, so the hot path is a dictionary lookup plus one reflective read. The stopwatch dictionary is unchanged, as recommended — the assumption it rests on (every chain Wolverine compiles gets one of the postprocessors that removes the entry) is now recorded in a comment alongside the symptom to look for if it ever stops holding.

`ObservabilityMiddleware:59` keeps a `static ConcurrentDictionary<Guid, Stopwatch>`, populated in `Before` and removed in `ElapsedMillisecondsSince`. Any path where neither the success nor the exception postprocessor runs leaves the entry permanently. The dictionary is static and never swept, so entries accumulate for the process lifetime.

Separately, `ExtractRecordsProcessed` (from line 150) calls `result.GetType().GetProperties()` and LINQ-filters it on **every** command and query invocation, including every HTTP request. The result type per chain is fixed, so this is recomputed work on the hot path.

**Recommendation:** cache the resolved `PropertyInfo` per result type in a static dictionary. The leak is low-probability enough to leave as-is, but worth a comment recording the assumption.

### L-4 — License and topic filter options only cover pages already viewed

**Status: fixed** in the remediation pass recorded as changelog revision 22. New `GET /api/facets` (`Features/Facets/GetFacetOptions/`) returns the catalog's distinct licenses and topics, filtered by the same `Scores.Any()` eligibility rule Hidden Gems itself uses so no option can be selected that could never return a result. `FacetOptionsService.recordRepositories` is gone; the service now loads all three facets from the backend. Languages were not duplicated onto the new endpoint - `/api/categories` already is that list.

`FacetOptionsService:54-69` accumulates license and topic options from repository cards the session has fetched. Language options come from the real `/api/categories` endpoint; the other two do not.

So a user can only filter by a license or topic that happens to appear in the cards currently loaded. The service documents this as an accepted approximation, and it is — but it means two of the three facets are effectively unusable for discovery, which is the product's whole point.

**Recommendation:** add a `/api/facets` endpoint returning distinct licenses and topics. The `LicenseIdentifier` index exists already; topics would need a GIN-backed distinct query.

### L-5 — `Summarization:MaxSummaryLength` drift

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`appsettings.json:27` sets `180`; `LmStudioRepositorySummarizer:65` defaults to `220` with a comment explaining that 220 was chosen to fill the card's 3-line clamp without overflowing. Same class of drift as H-1, much lower stakes — the cards are just shorter than designed.

**Recommendation:** pick one and align the other.

### L-6 — Repositories with unavailable contributor lists are scored as having zero contributors

**Status: fixed** in the scoring reshape recorded as changelog revision 20. `ComputeScoresCommandHandler` distinguishes "GitHub refused to enumerate" from "never fetched" using the existing `ContributorCount is null && ContributorCountFetchedAtUtc is not null` state — no schema change — and `ScoringWeights.ComputeTotalScore` reads that null as full marks for the signal. See ADR-019.

When GitHub returns "too large to list contributors", `DiscoverRepositoriesCommandHandler:99-107` logs, stamps `ContributorCountFetchedAtUtc`, and moves on — leaving `ContributorCount` at its previous value, or `null` for a newly discovered repository. `ComputeScoresCommandHandler:83` then coerces `null` to `0`, and the 22.5%-weighted contributor signal contributes nothing.

The repositories this affects are, by definition, the ones with the _most_ contributors. They are systematically under-scored, and the freshness stamp means it persists for seven days at a time.

Worth noting this is a small population the platform is not aimed at (they are not hidden gems), so product impact is limited. But the scoring is wrong in a knowable direction.

**Recommendation:** distinguish "unknown" from "zero" — either exclude the contributor signal and renormalise the remaining weights for those repositories, or store a sentinel the scorer recognises.

### L-7 — README truncation can split a surrogate pair

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`LmStudioRepositorySummarizer.TruncateReadme:122` slices with `readmeContent[.._maxReadmeCharacters]`. If character 6000 falls between the two halves of a surrogate pair — plausible for a README with emoji, which is most of them — the result ends in an unpaired surrogate that serialises to U+FFFD.

Harmless (the model sees one replacement character), but trivially avoidable.

**Recommendation:** step back one character when the slice ends on a high surrogate.

### L-8 — Build and tooling hygiene

**Status: fixed** in the remediation pass recorded as changelog revision 19.

- `src/backend/Directory.Build.props` sets `EnforceCodeStyleInBuild` but not `TreatWarningsAsErrors`. The build is currently at zero warnings — locking that in costs one line and prevents drift.
- `src/frontend/package.json`'s `format` and `format:check` scripts glob `"e2e/**/*.ts"`. There is no `e2e` directory. Leftover from a removed suite.

### L-9 — `UseHttpsRedirection` is a no-op in the container

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`Program.cs:318` calls `app.UseHttpsRedirection()`, but the container sets `ASPNETCORE_HTTP_PORTS=8080` only and exposes no HTTPS port. ASP.NET Core cannot determine a redirect target, logs a warning on first request, and skips redirection.

**Recommendation:** remove it, or guard it behind an environment check — as configured it can only produce a startup warning.

### L-10 — `Smtp:Password` is a committed configuration key

**Status: fixed** in the remediation pass recorded as changelog revision 19.

`appsettings.json` includes `"Password": ""` under `Smtp`. It is empty, so nothing is leaked, and gitleaks runs over full history in CI. But a committed key named `Password` is an invitation to fill it in locally and commit it — the exact accident F-015's scanning was added to catch, and it would catch it only after the fact.

**Recommendation:** drop the key from the committed file and source it from the environment, the way `GitHub:Token` already is.

### L-11 — Summaries are never regenerated

**Status: open.**

`GenerateSummariesCommandHandler` selects repositories with `!r.Summaries.Any()`, and the unique index on `Summary.RepositoryId` enforces one per repository forever. A repository that pivots, rewrites its README, or changes purpose keeps its original summary permanently.

The reasoning (don't re-spend inference budget) is sound for v1 and correctly documented. Flagging it as a product limitation rather than a defect.

**Recommendation:** for v2, consider regenerating when `PushedAtUtc` moves substantially past `GeneratedAtUtc`, bounded by the same batch cap.

---

## What is working well

Genuinely, and worth saying because it is not the default state of a solo project.

**The architecture holds up.** Vertical slices with Wolverine are applied consistently — every operation is a command/query plus handler in its own folder, with no service layer leaking across slices. `RepositoryCardQuery` is the one piece of shared code, and its header comment explains exactly why sharing is permitted there (ADR-015 governs the message/handler boundary, not ordinary helpers). That is a distinction most codebases blur.

**Stage isolation through the database is the right call and is honoured.** No pipeline stage holds a reference to another. The `IScoringContinuationLink` / `ISummarizationContinuationLink` / `ITrendsContinuationLink` seams exist specifically so Hangfire's static `BackgroundJob` API can be tested — a real seam solving a real testability problem, not speculative abstraction.

**Idempotency is thought through per stage, and differently where it should differ.** Four distinct persistence patterns — upsert-by-`GitHubId` for repositories, append-history for scores, create-once for summaries, upsert-by-natural-key for trends, plus `DigestSendLog` for sequential-retry dedupe — each with a comment explaining why that stage needs that pattern. F-016 then backed three of them with database constraints rather than relying on single-threadedness.

**F-017's read-path work is careful and measured.** The indexes are justified individually against `EXPLAIN ANALYZE` on a 100k seed set, including the `INCLUDE` columns for an index-only scan and the GIN index for array overlap. The old index it replaced was removed rather than left as write overhead. That is genuine performance work, not cargo cult.

**The digest email HTML is properly encoded.** Every interpolated value — repository name, owner, URL, LLM-generated summary, trend category — goes through `WebUtility.HtmlEncode`. LLM output flowing into HTML is a classic injection path and it was closed.

**Failure handling degrades rather than cascades.** A single repository's summarization failure does not abort the batch. A missing README is a valid input, not an error. A send failure is logged, not swallowed and not fatal. Each of these is commented with its reasoning.

**Secrets hygiene is right.** `.env` is git-ignored and docker-ignored, `.env.example` carries no real values, `.env` is not tracked (verified), and CI runs gitleaks over full history with `fetch-depth: 0` — the shallow-clone mistake that makes most history scans useless was explicitly avoided.

**CI covers more than tests.** Lint, format (including markdown for the governed docs), tests, and Snyk on both stacks, plus the repo-wide secret scan as its own job.

**The comments are unusually good.** They explain _why_, cite the ADR or spike that drove the decision, and frequently record what was tried and rejected — the Wolverine codegen scoping bug workaround, the `rgba()`-to-hex email-client story, the `n_keep >= n_ctx` failure that produced the README cap. That is real institutional memory. The drift in H-1 and L-5 is worth fixing precisely because the comments are otherwise trustworthy enough to rely on.

---

## Recommended order of work

Sequenced by value per unit of effort, not by severity alone.

Steps 1-5 of the original sequence are done (revision 19). What remains, in order:

1. **H-4** — the integration suite against a real PostgreSQL instance. The unlock for everything below it: once the production path is tested, the `isSqlite` branch can go, and with it the portability constraint that forces H-3's in-memory resolution.
2. **H-3, M-8** — the pipeline rewrite plus score retention. Best done after H-4 exists to catch regressions. L-1 falls out of the same work.
3. **M-3** — extract the crawler's Polly pipeline so the README fetch shares it.
4. **L-4** — a `/api/facets` endpoint, so license and topic filters cover more than the current page.
5. **L-3, L-6, L-11** — opportunistically; L-6 and L-11 are product decisions as much as code.

Item 1 is the one that pays for itself.

---

## Method and limits

Every finding was read in source at the cited file and line. Build, both test suites, and lint were executed; results are in the table at the top.

**Not covered by this review:**

- No runtime profiling or load testing. H-1's latency arithmetic and H-3's memory projections are derived from the code and configuration, not measured on your hardware.
- The Angular SCSS and templates were read for structure, not audited for accessibility or visual correctness.
- The twelve EF Core migration files were not reviewed individually.
- `src/backend/tools/SeedHarness/` was not reviewed — it is a development tool, not shipped code.
- No dependency CVE audit was run beyond noting that CI runs Snyk on both stacks.

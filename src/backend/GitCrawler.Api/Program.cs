using System.Net.Http.Headers;

using DotNetEnv;

using GitCrawler.Api.Data;
using GitCrawler.Api.Features.Bookmarks.CreateBookmark;
using GitCrawler.Api.Features.Bookmarks.DeleteBookmark;
using GitCrawler.Api.Features.Bookmarks.ListBookmarks;
using GitCrawler.Api.Features.Categories.GetCategories;
using GitCrawler.Api.Features.Categories.GetCategoryRepositories;
using GitCrawler.Api.Features.Crawling.DiscoverRepositories;
using GitCrawler.Api.Features.Diagnostics.Ping;
using GitCrawler.Api.Features.Repositories.GetDiscoveryFeed;
using GitCrawler.Api.Features.Repositories.GetHiddenGems;
using GitCrawler.Api.Features.Scoring.ComputeScores;
using GitCrawler.Api.Features.Summarization.GenerateSummaries;
using GitCrawler.Api.Features.Trends.AggregateTrends;
using GitCrawler.Api.Features.Trends.GetTrending;

using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;

using Microsoft.EntityFrameworkCore;

using Octokit.GraphQL;

using Wolverine;

// Loads the repo-root .env (walking up from this project's directory) into real process
// environment variables, so `dotnet run` outside Docker sees the same config Docker Compose
// already gets via docker-compose.yml's own .env handling. Verified via a standalone reflection
// probe of DotNetEnv 3.2.0 that Load() returns an empty result rather than throwing when no .env
// is found anywhere up the path - safe to call unconditionally, including inside the Docker image
// and in CI, where no .env exists and env vars are injected directly instead.
Env.TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

// .env uses flat, infra-facing names (GITHUB_TOKEN, LMSTUDIO_PORT, ...) that match
// docker-compose.yml's ${VAR} substitution syntax; ASP.NET Core's config keys are hierarchical
// (GitHub:Token, LmStudio:BaseUrl, ...). Bridge each one the app reads. Every bridge below is
// guarded so it's a no-op under Docker Compose: the flat names are only ever used there for ${...}
// interpolation into the already-correct GitHub__Token/LmStudio__BaseUrl/etc. env vars (see
// docker-compose.yml) - the flat names themselves are never set as raw env vars in the container,
// so these checks never fire there, and .env itself is excluded from the image (.dockerignore).
var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
if (!string.IsNullOrEmpty(githubToken))
{
    builder.Configuration["GitHub:Token"] = githubToken;
}

// LM Studio always runs on the host (ADR-016), so a bare `dotnet run` reaches it at localhost -
// unlike Docker Compose, which goes via host.docker.internal (see docker-compose.yml).
var lmStudioPort = Environment.GetEnvironmentVariable("LMSTUDIO_PORT");
if (!string.IsNullOrEmpty(lmStudioPort))
{
    builder.Configuration["LmStudio:BaseUrl"] = $"http://localhost:{lmStudioPort}";
}

// LMSTUDIO_IDENTIFIER is the fixed alias `make up`/`lms load --identifier` assigns the loaded
// model (see Makefile) - this is what the app must send as "model" in LM Studio API calls, not
// LMSTUDIO_MODEL (the catalog name used only to load it).
var lmStudioIdentifier = Environment.GetEnvironmentVariable("LMSTUDIO_IDENTIFIER");
if (!string.IsNullOrEmpty(lmStudioIdentifier))
{
    builder.Configuration["LmStudio:Model"] = lmStudioIdentifier;
}

// Host is "localhost" here since a bare `dotnet run` isn't on the Compose network - it reaches
// Postgres through docker-compose.yml's published port (POSTGRES_PORT) instead of the "postgres"
// container hostname docker-compose.yml uses for the app service. Consumed below by
// GitCrawlerDbContext's AddDbContext registration (F-004). Deliberately no "gitcrawler"/"5432"
// fallback literals here - .env.example is the single source of truth for those defaults; if any
// of the four is missing while the others are present, that's a malformed .env worth surfacing
// (via the resulting bridge no-op) rather than silently papering over with a second guess at the
// same default docker-compose.yml already encodes.
var postgresDb = Environment.GetEnvironmentVariable("POSTGRES_DB");
var postgresUser = Environment.GetEnvironmentVariable("POSTGRES_USER");
var postgresPassword = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
var postgresPort = Environment.GetEnvironmentVariable("POSTGRES_PORT");
if (!string.IsNullOrEmpty(postgresDb) && !string.IsNullOrEmpty(postgresUser)
    && !string.IsNullOrEmpty(postgresPassword) && !string.IsNullOrEmpty(postgresPort))
{
    builder.Configuration["ConnectionStrings:Postgres"] =
        $"Host=localhost;Port={postgresPort};Database={postgresDb};Username={postgresUser};Password={postgresPassword}";
}

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Read once, before Build(), so both the DbContext registration below and the Hangfire
// registration further down (which must also happen pre-Build, unlike the post-Build migration
// guard further below) see the same value instead of re-querying Configuration redundantly.
var postgresConnectionString = builder.Configuration.GetConnectionString("Postgres");

// F-004: the Data Store, shared by every Phase 1 pipeline stage (see GitCrawlerDbContext's
// header comment for the Hangfire schema-separation note - Hangfire's own storage, wired by
// F-006, coexists in this same database without conflicting with these migrations).
builder.Services.AddDbContext<GitCrawlerDbContext>(options =>
    options.UseNpgsql(postgresConnectionString));

// F-005: named HttpClient for the Crawler's REST fallback call (contributor count - GraphQL has
// no cheap equivalent field, F-001 spike §2). A named client via IHttpClientFactory rather than a
// dedicated REST client package, since this is the only REST endpoint the Crawler needs (Task
// Packet's REST-client-choice constraint).
builder.Services.AddHttpClient(GitHubDiscoveryClient.GitHubRestClientName, (sp, client) =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitCrawler/1.0");
    client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");

    var token = sp.GetRequiredService<IConfiguration>()["GitHub:Token"];
    if (!string.IsNullOrEmpty(token))
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }
});

// F-008: named HttpClient for LM Studio's OpenAI-compatible /v1/chat/completions endpoint
// (ADR-007), mirroring the GitHubRest named-client pattern above (F-005). No auth header needed -
// LM Studio's local server has no credential of its own (ADR-016: host-installed, reached only from
// this app's own process/Docker network, never exposed externally).
builder.Services.AddHttpClient(LmStudioRepositorySummarizer.LmStudioClientName, (sp, client) =>
{
    var baseUrl = sp.GetRequiredService<IConfiguration>()["LmStudio:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
    {
        client.BaseAddress = new Uri(baseUrl);
    }
});
builder.Services.AddScoped<IRepositorySummarizer, LmStudioRepositorySummarizer>();

// F-005: Octokit.GraphQL's Connection is safe to share as a singleton - the token is fixed for
// this single-operator system's lifetime (F-001 spike assumption 1), so there's no per-request
// identity to scope it to.
builder.Services.AddSingleton(sp =>
    new Connection(new Octokit.GraphQL.ProductHeaderValue("GitCrawler"), sp.GetRequiredService<IConfiguration>()["GitHub:Token"] ?? string.Empty));

// F-005/ADR-018: TimeProvider backs DiscoverRepositoriesCommandHandler's Polly resilience pipeline
// (both "now" for rate-limit reset math and the pipeline's own retry-delay timer) - injected so
// tests can substitute a fake clock instead of waiting out real minutes/hours.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IGitHubDiscoveryClient, GitHubDiscoveryClient>();

// Wolverine is the in-process command/query bus for every vertical slice (ADR-015) - this
// scans the assembly for handlers by convention (e.g. PingQueryHandler) rather than requiring
// manual registration per slice.
builder.Host.UseWolverine(opts =>
{
    // GitCrawlerDbContext is registered via AddDbContext, so its DbContextOptions<> dependency is
    // an "opaque" lambda factory (EF Core's own CreateDbContextOptions) that Wolverine's code
    // generator cannot construct inline - it can only be resolved via the DI container at
    // runtime. Wolverine 6 defaults to disallowing that ("service location") for anything not
    // explicitly opted in, since silent service location elsewhere would hide a real DI
    // misconfiguration; this is the documented, targeted opt-in for exactly this EF Core case
    // (wolverinefx.net/guide/codegen.html), scoped to this one type rather than relaxing the
    // policy for every handler dependency.
    opts.CodeGeneration.AlwaysUseServiceLocationFor<GitCrawlerDbContext>();
});

// F-006: Hangfire is the Job Scheduler (Architecture §3) that triggers each pipeline stage on
// schedule. PostgreSQL-backed storage (ADR-009) is what makes a scheduled recurring job's
// next-fire time, and any in-flight job's state, survive an app restart (AC2/NFR-003) - that's
// Hangfire's own persistence, not something this app builds separately. Guarded the same way as
// the DbContext migration further below: a no-op when no Postgres connection is configured (e.g.
// a design-time host build) rather than a startup crash.
if (!string.IsNullOrEmpty(postgresConnectionString))
{
    builder.Services.AddHangfire(config => config
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(postgresConnectionString)));

    // The recurring trigger registered below only *schedules* runs against storage - without a
    // server, nothing actually dequeues and executes them.
    builder.Services.AddHangfireServer();

    // Hangfire's AspNetCoreJobActivator resolves the job type from this DI container per
    // execution - it must be registered like any other constructor-injected service, not just be
    // constructible. ComputeScoresJob (F-007, link 2 of the chain) is resolved the same way, since
    // DiscoverRepositoriesJob now takes it as a constructor dependency to chain into via
    // IScoringContinuationLink (see DiscoverRepositoriesJob's header comment) - it has no
    // RecurringJob registration of its own below, since it's triggered only via that chain.
    // GenerateSummariesJob (F-008, link 3) follows the identical pattern: ComputeScoresJob now
    // takes it as a constructor dependency to chain into via ISummarizationContinuationLink (see
    // ComputeScoresJob's own header comment), so it likewise gets no RecurringJob registration
    // below. AggregateTrendsJob (F-009, link 4) follows the same pattern again: GenerateSummariesJob
    // now takes it as a constructor dependency to chain into via ITrendsContinuationLink (see
    // GenerateSummariesJob's own header comment), so it too gets no RecurringJob registration below.
    builder.Services.AddScoped<DiscoverRepositoriesJob>();
    builder.Services.AddScoped<ComputeScoresJob>();
    builder.Services.AddScoped<IScoringContinuationLink, HangfireScoringContinuationLink>();
    builder.Services.AddScoped<GenerateSummariesJob>();
    builder.Services.AddScoped<ISummarizationContinuationLink, HangfireSummarizationContinuationLink>();
    builder.Services.AddScoped<AggregateTrendsJob>();
    builder.Services.AddScoped<ITrendsContinuationLink, HangfireTrendsContinuationLink>();
}

// Liveness-only check for the Docker Compose health check (TC-003-04). Deliberately does not
// probe PostgreSQL/EF Core here: this scaffold has no schema yet (that's F-004), and coupling the
// app's own health check to a DB dependency would make local `dotnet run` (outside Docker Compose)
// fail to report healthy even though the process itself is fine.
builder.Services.AddHealthChecks();

var app = builder.Build();

// Apply pending migrations against a fresh PostgreSQL instance on every startup (F-004 AC2) so
// there's no separate manual migration step for the Docker Compose / bare `dotnet run` cases this
// file already bridges config for above. Guarded on a non-empty connection string, same pattern as
// those bridges - this is a no-op rather than a startup crash when Postgres isn't configured (e.g.
// a design-time host build), and keeps this out of the way of any future WebApplicationFactory
// test host that swaps in a different connection string/provider before startup.
if (!string.IsNullOrEmpty(postgresConnectionString))
{
    using var migrationScope = app.Services.CreateScope();
    migrationScope.ServiceProvider.GetRequiredService<GitCrawlerDbContext>().Database.Migrate();
}

// F-006: dashboard and recurring-job registration, guarded the same way as the migration above
// (both need the same live Postgres-backed Hangfire storage configured earlier).
if (!string.IsNullOrEmpty(postgresConnectionString))
{
    // Unauthenticated by operator decision (single-operator v1, no auth system elsewhere in this
    // app; a prior shared-secret query-key filter was removed after it broke the dashboard's own
    // CSS/JS/stats requests - relative URLs and polling XHRs don't carry the page's `?key=`
    // forward, so gating them the same as the page itself left the UI unstyled and stats
    // polling erroring). Restrict exposure at the network layer instead if this ever needs to
    // leave localhost (e.g. don't publish the container port beyond the host).
    //
    // `Authorization = []` is required, not cosmetic: DashboardOptions defaults to a
    // LocalRequestsOnlyAuthorizationFilter when this is left unset, and Docker Desktop's
    // port-publishing proxy doesn't preserve 127.0.0.1 as the apparent remote address for a
    // host-browser request through it - that default would 401 every request that reaches this
    // app via `make up`'s published port, not just non-local ones. An empty filter list means no
    // filter ever runs, so every request is authorized.
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = [],
    });

    // AC1: the recurring trigger that starts the pipeline chain. This is link 1 of "RecurringJob +
    // ContinueJobWith" (Architecture §3) - link 2 (Scoring Engine, F-007) now exists and is chained
    // in from inside DiscoverRepositoriesJob.RunAsync itself (see that class's header comment), not
    // registered as its own RecurringJob here, since its schedule IS "right after crawl finishes".
    // AddOrUpdate is idempotent on the "discover-repositories" job id, so re-running this on every
    // startup (including after a mid-run restart, AC2) updates the existing schedule rather than
    // creating a duplicate.
    //
    // `null!` for the PerformContext argument: Hangfire special-cases this parameter type and
    // substitutes the real per-execution context at invocation time, discarding whatever value is
    // supplied when building this expression - the same pattern documented in
    // DiscoverRepositoriesJob's header comment for the RunAsync signature itself.
    var crawlerCronSchedule = builder.Configuration.GetValue("Hangfire:CrawlerCronSchedule", "0 3 * * *");
    RecurringJob.AddOrUpdate<DiscoverRepositoriesJob>(
        "discover-repositories",
        job => job.RunAsync(null!),
        crawlerCronSchedule);
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapPingEndpoint();

// F-010: the Web API's four required dashboard views plus bookmark create/list/delete, all
// Wolverine command/query slices dispatched via IMessageBus (ADR-015), matching MapPingEndpoint's
// pattern above.
app.MapGetDiscoveryFeedEndpoint();
app.MapGetHiddenGemsEndpoint();
app.MapGetTrendingEndpoint();
app.MapGetCategoriesEndpoint();
app.MapGetCategoryRepositoriesEndpoint();
app.MapCreateBookmarkEndpoint();
app.MapDeleteBookmarkEndpoint();
app.MapListBookmarksEndpoint();

// Serve the Angular production build from wwwroot (populated by `dotnet publish` / the Docker
// image build - see GitCrawler.Api.csproj's BuildAngularApp/CopyAngularApp targets and
// src/backend/Dockerfile). MapFallbackToFile lets the Angular router handle client-side routes.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so integration tests can bootstrap the app via WebApplicationFactory<Program> in later
// features.
public partial class Program;
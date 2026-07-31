using DotNetEnv;

using GitCrawler.Api.Features.Diagnostics.Ping;

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
// container hostname docker-compose.yml uses for the app service. No DbContext consumes this yet
// - that's F-004 (Data Store schema)'s job, see docs/handoff.md - but the value is wired through
// now so F-004 doesn't also need to solve config sourcing. Deliberately no "gitcrawler"/"5432"
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

// Wolverine is the in-process command/query bus for every vertical slice (ADR-015) - this
// scans the assembly for handlers by convention (e.g. PingQueryHandler) rather than requiring
// manual registration per slice.
builder.Host.UseWolverine();

// Liveness-only check for the Docker Compose health check (TC-003-04). Deliberately does not
// probe PostgreSQL/EF Core here: this scaffold has no schema yet (that's F-004), and coupling the
// app's own health check to a DB dependency would make local `dotnet run` (outside Docker Compose)
// fail to report healthy even though the process itself is fine.
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");

app.MapPingEndpoint();

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
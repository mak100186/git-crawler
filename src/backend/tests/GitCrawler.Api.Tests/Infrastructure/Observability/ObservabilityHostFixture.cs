using GitCrawler.Api.Infrastructure.Observability;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Wolverine;

namespace GitCrawler.Api.Tests.Infrastructure.Observability;

// Builds one lightweight Wolverine host per test class (IClassFixture, not per-test - runtime
// codegen compilation is the expensive part of standing one up, so sharing it across a class's tests
// keeps the suite fast; nothing here is mutated between tests). Wired the exact same way Program.cs
// wires the real app - opts.Policies.AddMiddleware<ObservabilityMiddleware>() and
// opts.Policies.Add<RecordsProcessedPolicy>() inside UseWolverine(...), no per-handler opt-in
// anywhere - against a set of local, disposable fake handler types (see FakeHandlers.cs) standing in
// for real pipeline areas (Crawling, Scoring, Summarization, Digest, Web-API queries).
//
// Deliberately does NOT point Wolverine's own assembly discovery at the real GitCrawler.Api.dll
// (via opts.Discovery.IncludeAssembly(typeof(Program).Assembly)) to exercise the actual production
// handler graph - see this feature's Assumptions/Deviations for why: driving Wolverine's reflection-
// based type scan over that assembly from a bare test host (rather than the full ASP.NET Core app
// that already boots it successfully every day via `make dev`/`make up`) risks tripping over its
// Hangfire/Npgsql/Octokit.GraphQL transitive dependencies during discovery, which a minimal test
// host has no reason to carry and no way to stub out cleanly. These fakes exercise the exact same
// ObservabilityMiddleware/RecordsProcessedPolicy production code, registered exactly the way
// Program.cs registers it, so they prove the same platform-wide wiring without that risk.
public sealed class ObservabilityHostFixture : IAsyncLifetime
{
    private IHost _appHost = null!;

    public IMessageBus Bus => _appHost.Services.GetRequiredService<IMessageBus>();

    public CapturingLoggerProvider Logs { get; } = new();

    public async Task InitializeAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(Logs);
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.UseWolverine(opts =>
        {
            opts.Discovery.IncludeAssembly(typeof(ObservabilityHostFixture).Assembly);
            opts.Policies.AddMiddleware<ObservabilityMiddleware>();
            opts.Policies.Add<RecordsProcessedPolicy>();
        });

        _appHost = builder.Build();
        await _appHost.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _appHost.StopAsync();
        _appHost.Dispose();
    }
}
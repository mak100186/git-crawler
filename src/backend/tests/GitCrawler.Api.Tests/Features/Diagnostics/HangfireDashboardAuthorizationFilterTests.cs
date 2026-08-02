using GitCrawler.Api.Features.Diagnostics;

using Hangfire;
using Hangfire.Dashboard;
using Hangfire.Storage;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GitCrawler.Api.Tests.Features.Diagnostics;

// Hangfire's DashboardContext has no lighter-weight test seam than its ASP.NET Core-specific
// subclass (AspNetCoreDashboardContext), which itself requires a real HttpContext and JobStorage
// instance to construct (confirmed by inspecting the installed Hangfire.AspNetCore 1.8.24 package -
// there is no in-memory/no-op JobStorage shipped in Hangfire.Core to reach for instead). FakeStorage
// below supplies the one abstract member the constructor requires; Authorize itself never touches
// it, so throwing on use keeps the fake honest rather than fabricating a working store.
public class HangfireDashboardAuthorizationFilterTests
{
    [Fact]
    public void Authorize_MatchingKey_ReturnsTrue()
    {
        var filter = CreateFilter(configuredKey: "s3cret");

        Assert.True(filter.Authorize(CreateContext(queryKey: "s3cret")));
    }

    [Fact]
    public void Authorize_NoAccessKeyConfigured_ReturnsFalse()
    {
        // Fails closed (NFR-003): a blank/unset secret must not silently open the dashboard to
        // anyone who can reach the port, even if a request happens to supply some "key" value.
        var filter = CreateFilter(configuredKey: null);

        Assert.False(filter.Authorize(CreateContext(queryKey: "anything")));
    }

    [Fact]
    public void Authorize_MissingKeyQueryParameter_ReturnsFalse()
    {
        // Fails closed: a secret is configured, but the request supplies none at all.
        var filter = CreateFilter(configuredKey: "s3cret");

        Assert.False(filter.Authorize(CreateContext(queryKey: null)));
    }

    [Fact]
    public void Authorize_WrongKey_ReturnsFalse()
    {
        var filter = CreateFilter(configuredKey: "s3cret");

        Assert.False(filter.Authorize(CreateContext(queryKey: "wrong")));
    }

    private static HangfireDashboardAuthorizationFilter CreateFilter(string? configuredKey)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configuredKey is null
                ? []
                : new Dictionary<string, string?> { ["Hangfire:DashboardAccessKey"] = configuredKey })
            .Build();

        return new HangfireDashboardAuthorizationFilter(configuration);
    }

    private static DashboardContext CreateContext(string? queryKey)
    {
        var httpContext = new DefaultHttpContext
        {
            // AspNetCoreDashboardContext's constructor resolves internal services off
            // HttpContext.RequestServices - a null provider throws, so an empty (but real)
            // container is enough since none of those internal lookups are exercised by Authorize.
            RequestServices = new ServiceCollection().BuildServiceProvider(),
        };
        httpContext.Request.QueryString = queryKey is null
            ? QueryString.Empty
            : new QueryString($"?key={queryKey}");

        return new AspNetCoreDashboardContext(new FakeStorage(), new DashboardOptions(), httpContext);
    }

    private sealed class FakeStorage : JobStorage
    {
        public override IStorageConnection GetConnection() => throw new NotSupportedException();

        public override IMonitoringApi GetMonitoringApi() => throw new NotSupportedException();
    }
}
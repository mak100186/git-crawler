using Wolverine;

namespace GitCrawler.Api.Features.Trends.GetTrending;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Diagnostics/Ping/PingEndpoint.cs.
public static class GetTrendingEndpoint
{
    public static IEndpointRouteBuilder MapGetTrendingEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/trending", async (IMessageBus bus) =>
                await bus.InvokeAsync<GetTrendingResult>(new GetTrendingQuery()))
            .WithName("GetTrending");

        return app;
    }
}
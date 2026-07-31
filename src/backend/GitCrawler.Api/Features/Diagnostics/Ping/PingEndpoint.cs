using Wolverine;

namespace GitCrawler.Api.Features.Diagnostics.Ping;

// Endpoint registration lives with the slice it belongs to, instead of a central controller/route
// file, per the vertical-slice convention (ADR-015). Web API operations dispatch through
// Wolverine's in-process command bus (IMessageBus.InvokeAsync) rather than calling a service
// method directly.
public static class PingEndpoint
{
    public static IEndpointRouteBuilder MapPingEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ping", async (IMessageBus bus) =>
                await bus.InvokeAsync<PingResult>(new PingQuery()))
            .WithName("Ping");

        return app;
    }
}
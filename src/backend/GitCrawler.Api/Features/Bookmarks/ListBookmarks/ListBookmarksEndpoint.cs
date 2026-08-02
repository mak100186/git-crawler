using Wolverine;

namespace GitCrawler.Api.Features.Bookmarks.ListBookmarks;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Diagnostics/Ping/PingEndpoint.cs.
public static class ListBookmarksEndpoint
{
    public static IEndpointRouteBuilder MapListBookmarksEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/bookmarks", async (IMessageBus bus) =>
                await bus.InvokeAsync<ListBookmarksResult>(new ListBookmarksQuery()))
            .WithName("ListBookmarks");

        return app;
    }
}
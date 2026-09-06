using Wolverine;

namespace GitCrawler.Api.Features.Bookmarks.CreateBookmark;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Diagnostics/Ping/PingEndpoint.cs.
public static class CreateBookmarkEndpoint
{
    public static IEndpointRouteBuilder MapCreateBookmarkEndpoint(this IEndpointRouteBuilder app)
    {
        // 200 OK for both "created" and "already bookmarked" (see CreateBookmarkCommandHandler's
        // comment) - idempotent by design, not a 409/500 on a repeat call.
        // 404 when the repository doesn't exist - the foreign key would otherwise turn that into a
        // 500 (see CreateBookmarkCommandHandler).
        app.MapPost("/api/repositories/{repositoryId:int}/bookmark", async (int repositoryId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<CreateBookmarkResult>(new CreateBookmarkCommand(repositoryId));
                return result.Bookmark is null ? Results.NotFound() : Results.Ok(result.Bookmark);
            })
            .WithName("CreateBookmark");

        return app;
    }
}
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
        app.MapPost("/api/repositories/{repositoryId:int}/bookmark", async (int repositoryId, IMessageBus bus) =>
                Results.Ok(await bus.InvokeAsync<BookmarkDto>(new CreateBookmarkCommand(repositoryId))))
            .WithName("CreateBookmark");

        return app;
    }
}
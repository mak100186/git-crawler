using Wolverine;

namespace GitCrawler.Api.Features.Bookmarks.DeleteBookmark;

// Endpoint registration lives with the slice it belongs to (ADR-015), matching
// Features/Diagnostics/Ping/PingEndpoint.cs.
public static class DeleteBookmarkEndpoint
{
    public static IEndpointRouteBuilder MapDeleteBookmarkEndpoint(this IEndpointRouteBuilder app)
    {
        // 204 No Content whether a row was actually deleted or none existed (see
        // DeleteBookmarkCommandHandler's comment) - idempotent by design, not a 404 on a repeat call.
        app.MapDelete("/api/repositories/{repositoryId:int}/bookmark", async (int repositoryId, IMessageBus bus) =>
            {
                await bus.InvokeAsync<DeleteBookmarkResult>(new DeleteBookmarkCommand(repositoryId));
                return Results.NoContent();
            })
            .WithName("DeleteBookmark");

        return app;
    }
}
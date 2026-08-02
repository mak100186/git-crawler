using GitCrawler.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Bookmarks.DeleteBookmark;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder.
public record DeleteBookmarkCommand(int RepositoryId);

public record DeleteBookmarkResult(bool Deleted);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class DeleteBookmarkCommandHandler(GitCrawlerDbContext dbContext)
{
    // F-010 Constraints: deleting a non-existent bookmark is treated as a no-op success (Deleted =
    // false, but no exception) rather than an error - the endpoint maps this to 204 either way (see
    // DeleteBookmarkEndpoint), for the same idempotency reasoning CreateBookmarkCommandHandler
    // applies to its own already-bookmarked case: a client retrying a delete after a dropped
    // response shouldn't see a 404 on the retry just because the first attempt already succeeded.
    public async Task<DeleteBookmarkResult> HandleAsync(DeleteBookmarkCommand command, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Bookmarks.SingleOrDefaultAsync(b => b.RepositoryId == command.RepositoryId, cancellationToken);
        if (existing is null)
        {
            return new DeleteBookmarkResult(false);
        }

        dbContext.Bookmarks.Remove(existing);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteBookmarkResult(true);
    }
}
using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Bookmarks.CreateBookmark;

public record BookmarkDto(int Id, int RepositoryId, DateTimeOffset CreatedAtUtc);

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder.
public record CreateBookmarkCommand(int RepositoryId);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class CreateBookmarkCommandHandler(GitCrawlerDbContext dbContext, TimeProvider timeProvider)
{
    // F-010 Constraints: bookmarking an already-bookmarked repo must not throw the unique-index
    // violation on Bookmark.RepositoryId (GitCrawlerDbContext.OnModelCreating) - this
    // check-then-create makes the operation idempotent, returning the existing row instead of
    // attempting (and failing) a second insert. The endpoint maps this to 200 either way (see
    // CreateBookmarkEndpoint) rather than distinguishing "created" from "already existed" with a
    // 201/200 split, since the caller's intent - "make sure this is bookmarked" - is satisfied
    // identically in both cases.
    public async Task<BookmarkDto> HandleAsync(CreateBookmarkCommand command, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Bookmarks.SingleOrDefaultAsync(b => b.RepositoryId == command.RepositoryId, cancellationToken);
        if (existing is not null)
        {
            return new BookmarkDto(existing.Id, existing.RepositoryId, existing.CreatedAtUtc);
        }

        var bookmark = new Bookmark { RepositoryId = command.RepositoryId, CreatedAtUtc = timeProvider.GetUtcNow() };
        dbContext.Bookmarks.Add(bookmark);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new BookmarkDto(bookmark.Id, bookmark.RepositoryId, bookmark.CreatedAtUtc);
    }
}
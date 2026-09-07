using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Bookmarks.CreateBookmark;

public record BookmarkDto(int Id, int RepositoryId, DateTimeOffset CreatedAtUtc);

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder.
public record CreateBookmarkCommand(int RepositoryId);

// Bookmark is null when RepositoryId matches no repository, which the endpoint maps to 404. A
// nullable wrapper rather than a nullable BookmarkDto straight off HandleAsync: Wolverine's
// InvokeAsync<T> expects a response, and "no such repository" is a real answer, not a missing one.
public record CreateBookmarkResult(BookmarkDto? Bookmark);

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
    public async Task<CreateBookmarkResult> HandleAsync(CreateBookmarkCommand command, CancellationToken cancellationToken)
    {
        var existing = await dbContext.Bookmarks.SingleOrDefaultAsync(b => b.RepositoryId == command.RepositoryId, cancellationToken);
        if (existing is not null)
        {
            return new CreateBookmarkResult(new BookmarkDto(existing.Id, existing.RepositoryId, existing.CreatedAtUtc));
        }

        // Checked before the insert because Bookmark.RepositoryId is a real foreign key: without
        // this, an unknown id reaches the database, comes back as a DbUpdateException, and the
        // caller gets a 500 for what is plainly a 404.
        if (!await dbContext.Repositories.AnyAsync(r => r.Id == command.RepositoryId, cancellationToken))
        {
            return new CreateBookmarkResult(null);
        }

        var bookmark = new Bookmark { RepositoryId = command.RepositoryId, CreatedAtUtc = timeProvider.GetUtcNow() };
        dbContext.Bookmarks.Add(bookmark);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateBookmarkResult(new BookmarkDto(bookmark.Id, bookmark.RepositoryId, bookmark.CreatedAtUtc));
    }
}
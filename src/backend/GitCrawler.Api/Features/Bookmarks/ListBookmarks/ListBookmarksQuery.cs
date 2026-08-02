using GitCrawler.Api.Data;
using GitCrawler.Api.Features.Repositories;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Bookmarks.ListBookmarks;

// Wolverine message + result for this slice. Returns the shared repo-card shape (F-010 D5) rather
// than a bare list of Bookmark rows, since a bookmarked repo's own card fields (summary, topics,
// etc.) are what a client actually needs to render - not paginated (Task Packet gives no D4-style
// contract for this endpoint, only "create/list/delete"), ordered most-recently-bookmarked first.
// No shared service/repository layer per ADR-015 - everything this operation needs lives in this
// folder beyond the reused RepositoryCardQuery/RepositoryCardDto helper (Features/Repositories).
public record ListBookmarksQuery;

public record ListBookmarksResult(IReadOnlyList<RepositoryCardDto> Repositories);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class ListBookmarksQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<ListBookmarksResult> HandleAsync(ListBookmarksQuery query, CancellationToken cancellationToken)
    {
        var bookmarkedRepositories = await RepositoryCardQuery.IncludeForCards(dbContext.Repositories.Where(r => r.Bookmarks.Any()))
            .ToListAsync(cancellationToken);

        var ordered = bookmarkedRepositories
            .OrderByDescending(r => r.Bookmarks.Max(b => b.CreatedAtUtc))
            .Select(r => RepositoryCardQuery.ToCardDto(new RankedRepository(
                r,
                r.Scores.OrderByDescending(s => s.ComputedAtUtc).FirstOrDefault(),
                r.Summaries.OrderByDescending(s => s.GeneratedAtUtc).FirstOrDefault())))
            .ToList();

        return new ListBookmarksResult(ordered);
    }
}
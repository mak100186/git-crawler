using GitCrawler.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Facets.GetFacetOptions;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder.
//
// Why this exists (review finding L-4): the dashboard's License and Topic filters used to build
// their option lists client-side, accumulating values off whatever repository cards the session had
// already fetched. That meant you could only filter by a license or topic that happened to be on
// screen - two of the three facets were unusable for discovery, which is the product's whole point.
// Language never had the problem because GET /api/categories already returns the catalog's distinct
// languages; this is the missing equivalent for the other two, and it deliberately does not
// duplicate languages - the frontend still reads those from /api/categories.
public record GetFacetOptionsQuery;

public record GetFacetOptionsResult(IReadOnlyList<string> Licenses, IReadOnlyList<string> Topics);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GetFacetOptionsQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<GetFacetOptionsResult> HandleAsync(GetFacetOptionsQuery query, CancellationToken cancellationToken)
    {
        // Scores.Any() matches GetHiddenGemsQueryHandler's eligibility filter and GetCategories-
        // QueryHandler's, for the same reason both use it: an option only belongs in this list if
        // selecting it could actually return a Hidden Gems result. An unscored repository's license
        // would otherwise offer a filter that always comes back empty.
        //
        // Distinct() runs server-side in both cases, so what crosses the wire is the option list
        // itself rather than one row per repository - this endpoint is called once per session and
        // must not become the kind of full-table fetch review finding H-3 is about.
        var licenses = await dbContext.Repositories
            .Where(r => r.Scores.Any() && r.LicenseIdentifier != null)
            .Select(r => r.LicenseIdentifier!)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Topics is a primitive collection, so flattening it requires unnesting one row into many.
        // Npgsql translates that to unnest() over the text[] column and does the Distinct() in SQL,
        // which is what production runs. The SQLite provider the test suite uses rejects it outright
        // ("Translating this query requires the SQL APPLY operation, which is not supported on
        // SQLite"), so it falls back to pulling the arrays and flattening in memory - the same
        // provider-capability split GetHiddenGemsQueryHandler already carries for its sort path, and
        // it goes away with review finding H-4's move to PostgreSQL Testcontainers.
        var scoredRepositories = dbContext.Repositories.Where(r => r.Scores.Any());

        var topics = dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite"
            ? (await scoredRepositories.Select(r => r.Topics).ToListAsync(cancellationToken))
                .SelectMany(t => t)
                .Distinct()
                .ToList()
            : await scoredRepositories
                .SelectMany(r => r.Topics)
                .Distinct()
                .ToListAsync(cancellationToken);

        // Ordinal sort, matching GetCategoriesQueryHandler - stable across cultures, and the
        // frontend renders the order it is given rather than re-sorting.
        return new GetFacetOptionsResult(
            licenses.OrderBy(l => l, StringComparer.Ordinal).ToList(),
            topics.OrderBy(t => t, StringComparer.Ordinal).ToList());
    }
}
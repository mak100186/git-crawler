using GitCrawler.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Categories.GetCategories;

// F-010 D2: Category = Repository.PrimaryLanguage, not GitHub topics. Reads Repository directly as
// of 2026-08-04 - originally read TrendAggregate.Category instead (F-009's per-category rollup),
// but that meant fetching every TrendAggregate row's RepositoryCount/AverageScore/PeriodStart/
// PeriodEnd over the wire just to discard all four and keep only the Category string (the frontend's
// FacetOptionsService only ever read `.category` off each row - confirmed by reading it directly,
// not assumed) - a whole scheduled rollup pipeline in the critical path of what's really just "the
// distinct-language option list for a dropdown." TrendAggregate/AggregateTrendsCommand themselves
// are unchanged and still run - operator-directed decision to keep them reserved for F-013's planned
// digest (PRD Goal 3) rather than remove them now that this, their last UI-facing consumer, no
// longer needs them.
public record CategoryDto(string Category);

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder.
public record GetCategoriesQuery;

public record GetCategoriesResult(IReadOnlyList<CategoryDto> Categories);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GetCategoriesQueryHandler(GitCrawlerDbContext dbContext)
{
    public async Task<GetCategoriesResult> HandleAsync(GetCategoriesQuery query, CancellationToken cancellationToken)
    {
        // Scores.Any() matches GetHiddenGemsQueryHandler's own eligibility filter exactly - a
        // language only belongs in this option list if selecting it could actually return a Hidden
        // Gems result. This is also a latent-bug fix over the old TrendAggregate-based version: that
        // one required *both* a Score and a Summary to exist (AggregateTrendsCommandHandler's own,
        // narrower "scored + summarized" rollup criteria) before a language appeared here, but Hidden
        // Gems itself only ever required a Score - so a newly-scored-but-not-yet-summarized repo's
        // language was invisible to this filter until the next nightly Trend Aggregator run, even
        // though the repo was already showing up on Hidden Gems.
        var languages = await dbContext.Repositories
            .Where(r => r.Scores.Any() && r.PrimaryLanguage != null)
            .Select(r => r.PrimaryLanguage!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var categories = languages
            .OrderBy(c => c, StringComparer.Ordinal)
            .Select(c => new CategoryDto(c))
            .ToList();

        return new GetCategoriesResult(categories);
    }
}
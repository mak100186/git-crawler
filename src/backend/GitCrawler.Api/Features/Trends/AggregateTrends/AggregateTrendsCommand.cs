using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Trends.AggregateTrends;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder. Triggered by F-006's Job Scheduler via
// AggregateTrendsJob.RunAsync's IMessageBus.InvokeAsync call, chained after GenerateSummariesJob
// finishes (see that class) - so, like ComputeScoresCommand/GenerateSummariesCommand, this is a
// plain command with no HTTP endpoint.
public record AggregateTrendsCommand;

public record AggregateTrendsResult(int TrendCount, int RepositoryCount);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
//
// Pure computation, no external calls (Architecture §3, same constraint as Scoring Engine's own
// handler comment): this only reads/writes GitCrawlerDbContext over data every earlier stage has
// already persisted (Repository/Score/Summary) - no GitHub or LM Studio calls of any kind.
public class AggregateTrendsCommandHandler(GitCrawlerDbContext dbContext, IConfiguration configuration, TimeProvider timeProvider)
{
    // No spec exists anywhere upstream for the rollup window (Task Packet: judgment call, same gap
    // as F-005's DiscoveryLookbackDays/DiscoveryMinimumStars and F-008's MinimumScore/BatchSize).
    // PeriodDays=1 is the v1 default: PeriodStart == PeriodEnd == today, i.e. a daily snapshot taken
    // on this run's date, rather than a rolling window. Operator-tunable so a future run can widen
    // this to a multi-day window without a code change.
    private readonly int _periodDays = configuration.GetValue("Trends:PeriodDays", 1);

    public async Task<AggregateTrendsResult> HandleAsync(AggregateTrendsCommand command, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var periodEnd = today;
        var periodStart = today.AddDays(-(_periodDays - 1));

        // "Scored + summarized" = has cleared the full pipeline (crawled -> scored -> summarized),
        // not just scored - both Scores.Any() and Summaries.Any() must hold. Any() on each
        // navigation collection translates to a SQL EXISTS, so this filter runs relationally on
        // both the xUnit suite's SQLite provider and the real Npgsql/Postgres provider; only the
        // Scores themselves need to be pulled into memory afterward (see the latest-by-time
        // resolution below).
        //
        // Category = Repository.PrimaryLanguage (Task Packet's explicit judgment call): the schema
        // has no separate "framework"/"ecosystem" field beyond PrimaryLanguage, so this is the
        // pragmatic v1 proxy for "technology/framework/ecosystem" until a richer taxonomy exists.
        // A null PrimaryLanguage means there's no meaningful category to attribute the repo to, so
        // it's excluded here rather than lumped into a fake "Unknown" bucket that would otherwise
        // silently blend genuinely-uncategorized repos into the trend data.
        var candidates = await dbContext.Repositories
            .Include(r => r.Scores)
            .Where(r => r.PrimaryLanguage != null && r.Scores.Any() && r.Summaries.Any())
            .ToListAsync(cancellationToken);

        // The "latest" Score is the one with the max ComputedAtUtc (chronologically most recent),
        // not the one with the highest-ever TotalScore - the same distinction
        // ComputeScoresCommandHandler and GenerateSummariesCommandHandler each draw for their own
        // Score reads (see those classes' comments). A repo's trend contribution must reflect its
        // current standing, not a historical peak that no longer holds. Resolved client-side (not a
        // correlated-subquery projection) for the same portability reason those handlers give:
        // relational providers vary in translating ORDER BY/First over a DateTimeOffset column, and
        // every candidate's Scores are already loaded into memory here regardless.
        var trendsByCategory = candidates
            .Select(r => new
            {
                Category = r.PrimaryLanguage!,
                LatestScore = r.Scores.OrderByDescending(s => s.ComputedAtUtc).First().TotalScore,
            })
            .GroupBy(x => x.Category);

        var trendCount = 0;
        var repositoryCount = 0;

        foreach (var group in trendsByCategory)
        {
            var category = group.Key;
            var count = group.Count();
            var averageScore = group.Average(x => x.LatestScore);

            // Idempotency (NFR-003): re-running this command for the same period must not create
            // duplicate TrendAggregate rows per category. This is a third, distinct persistence
            // pattern from the two this codebase already has - Score is intentional append-history
            // (one row per re-score, ComputeScoresCommandHandler) and Summary is create-once, never
            // regenerated (GenerateSummariesCommandHandler) - TrendAggregate instead needs
            // upsert-by-natural-key semantics: for a given (Category, PeriodStart, PeriodEnd),
            // update the existing row's RepositoryCount/AverageScore/CreatedAtUtc if one already
            // exists for this run's period, rather than inserting a second row. There's no unique
            // database constraint enforcing this (TrendAggregate's schema already has only a
            // non-unique index on (Category, PeriodStart) - Task Packet's explicit instruction not
            // to add a migration for this feature), so the single-threaded nature of this Hangfire
            // job is what makes the query-then-upsert below safe rather than a schema-level
            // guarantee.
            var existing = await dbContext.TrendAggregates.SingleOrDefaultAsync(
                t => t.Category == category && t.PeriodStart == periodStart && t.PeriodEnd == periodEnd,
                cancellationToken);

            if (existing is null)
            {
                existing = new TrendAggregate
                {
                    Category = category,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                };
                dbContext.TrendAggregates.Add(existing);
            }

            existing.RepositoryCount = count;
            existing.AverageScore = averageScore;
            existing.CreatedAtUtc = timeProvider.GetUtcNow();

            trendCount++;
            repositoryCount += count;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AggregateTrendsResult(trendCount, repositoryCount);
    }
}
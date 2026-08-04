using System.Text;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Repositories;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Digest.SendDigest;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder. Triggered by F-006's Job Scheduler via
// SendDigestJob.RunAsync's IMessageBus.InvokeAsync call, on its own daily RecurringJob rather than
// chained onto AggregateTrendsJob (see SendDigestJob's header comment for why) - so, like every
// other pipeline-stage command in this codebase, this is a plain command with no HTTP endpoint.
public record SendDigestCommand;

public record SendDigestResult(bool Sent, int RepositoryCount, int TrendCount);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class SendDigestCommandHandler(
    GitCrawlerDbContext dbContext,
    IEmailSender emailSender,
    IConfiguration configuration,
    ILogger<SendDigestCommandHandler> logger,
    TimeProvider timeProvider)
{
    // No spec exists anywhere upstream for how many gems the digest should list (same judgment-call
    // gap as every other stage's own tunable - see AggregateTrendsCommandHandler._periodDays,
    // GenerateSummariesCommandHandler._batchSize/._minimumScore). 10 keeps the email skimmable as a
    // "passive daily pulse" (Task Packet's own framing) rather than reproducing the whole Hidden Gems
    // list inline. Operator-tunable via config.
    private readonly int _topN = configuration.GetValue("Digest:TopN", 10);

    // No recipient can be safely assumed - unlike GitHub:Token (optional, falls back to
    // unauthenticated) or LmStudio:Model (throws at construction, since summarization is unusable
    // without it), an unconfigured recipient here is a legitimate "digest not set up yet" v1 state -
    // no operator onboarding flow exists anywhere in this codebase to collect this. Treated as a
    // no-op with a log entry below, not a startup crash: this command runs unattended on a schedule,
    // so it must degrade gracefully rather than take the host down over missing opt-in config.
    private readonly string _recipientEmail = configuration.GetValue("Digest:RecipientEmail", string.Empty) ?? string.Empty;

    public async Task<SendDigestResult> HandleAsync(SendDigestCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_recipientEmail))
        {
            logger.LogWarning("Digest:RecipientEmail is not configured; skipping the daily digest send.");
            return new SendDigestResult(Sent: false, RepositoryCount: 0, TrendCount: 0);
        }

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // "Scored + summarized" eligibility - the same rule AggregateTrendsCommandHandler applies for
        // its own rollup (Task Packet's explicit precedent): a digest entry needs Summary.ShortContent
        // to be worth including, and Score.TotalScore to be worth ranking by.
        var eligibleRepositories = dbContext.Repositories.Where(r => r.Scores.Any() && r.Summaries.Any());

        // Reuses RepositoryCardQuery.IncludeForCards/Rank (Features/Repositories) rather than
        // re-implementing latest-Score resolution here - that helper's own header comment explicitly
        // notes it's shared "ordinary code" ADR-015 permits (only the message/handler boundary is
        // off-limits to sharing), and ranking by RepositorySortField.Score already resolves each
        // repo's *latest* Score by ComputedAtUtc, not its historical peak - this Task Packet's own
        // constraint, already enforced by that shared helper.
        var candidates = await RepositoryCardQuery.IncludeForCards(eligibleRepositories).ToListAsync(cancellationToken);
        var topGems = RepositoryCardQuery.Rank(candidates, RepositorySortField.Score, SortDirection.Desc)
            .Take(_topN)
            .ToList();

        // "Current period" = whatever TrendAggregate rows AggregateTrendsCommandHandler most recently
        // wrote for "today" (its own PeriodEnd is always the run date - see that class's comment).
        // Not re-deriving a PeriodDays window here: this consumer only ever needs today's
        // already-computed rollup, not to recompute one of its own.
        var trends = await dbContext.TrendAggregates
            .Where(t => t.PeriodEnd == today)
            .OrderByDescending(t => t.AverageScore)
            .ToListAsync(cancellationToken);

        var subject = $"GitCrawler Daily Digest — {today:yyyy-MM-dd}";
        var body = ComposeBody(today, topGems, trends);

        try
        {
            await emailSender.SendAsync(new EmailMessage(_recipientEmail, subject, body), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // FR-006: a send failure must be logged, not silently dropped, and must not crash the
            // Hangfire job/host - same bounded "log and move on" philosophy
            // GenerateSummariesCommandHandler applies to its own per-repo LM Studio failures. No
            // retry here either (Task Packet's explicit "no retry requirement") - the next scheduled
            // run tries again on its own cron.
            logger.LogError(ex, "Failed to send the daily digest email to {Recipient}", _recipientEmail);
            return new SendDigestResult(Sent: false, RepositoryCount: topGems.Count, TrendCount: trends.Count);
        }

        return new SendDigestResult(Sent: true, RepositoryCount: topGems.Count, TrendCount: trends.Count);
    }

    // Plain text, not HTML - consistent with this codebase's existing summarization prompts, which
    // deliberately steer the model away from Markdown/HTML for exactly this reason (see
    // LmStudioRepositorySummarizer's ShortSystemPrompt/DetailedSystemPrompt comments): a digest email
    // is read in an inbox, not rendered by this app's own UI, so there's no client-side formatting
    // step to rely on either way. Keeping the body itself simple avoids introducing that concern here.
    private static string ComposeBody(DateOnly today, IReadOnlyList<RankedRepository> topGems, IReadOnlyList<TrendAggregate> trends)
    {
        var body = new StringBuilder();
        body.AppendLine($"GitCrawler Daily Digest — {today:yyyy-MM-dd}");
        body.AppendLine();
        body.AppendLine("Top Hidden Gems");
        body.AppendLine("---------------");

        if (topGems.Count == 0)
        {
            body.AppendLine("No hidden gems to report today.");
        }
        else
        {
            var rank = 1;
            foreach (var gem in topGems)
            {
                // Never null: the Scores.Any()/Summaries.Any() filter above guarantees at least one
                // Score and one Summary row for every candidate, so RepositoryCardQuery.Rank's own
                // latest-by-time resolution always finds one of each here (same guarantee
                // GetHiddenGemsQueryHandler's own ToHiddenGemDto relies on for LatestScore).
                var score = gem.LatestScore!.TotalScore;
                var summary = gem.LatestSummary!.ShortContent;

                body.AppendLine($"{rank}. {gem.Repository.Owner}/{gem.Repository.Name} (score: {Math.Round(score)})");
                body.AppendLine($"   {summary}");
                body.AppendLine();
                rank++;
            }
        }

        body.AppendLine("Trend Summary");
        body.AppendLine("-------------");

        if (trends.Count == 0)
        {
            body.AppendLine("No trend data available for the current period.");
        }
        else
        {
            foreach (var trend in trends)
            {
                body.AppendLine($"{trend.Category}: {trend.RepositoryCount} repositories, average score {Math.Round(trend.AverageScore)}");
            }
        }

        return body.ToString();
    }
}
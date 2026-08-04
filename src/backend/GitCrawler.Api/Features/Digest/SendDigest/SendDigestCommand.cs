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
public record SendDigestCommand(bool isHtml = true);

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
            .OrderByDescending(t => t.RepositoryCount)
            .ThenByDescending(t => t.AverageScore)
            .Take(12)
            .ToListAsync(cancellationToken);

        var subject = $"GitCrawler Daily Digest — {today:yyyy-MM-dd}";

        var body = command.isHtml ? ComposeHtmlBody(today, topGems, trends) : ComposeBody(today, topGems, trends);

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

    private static string ComposeHtmlBody(DateOnly today, IReadOnlyList<RankedRepository> topGems, IReadOnlyList<TrendAggregate> trends)
    {
        var body = new StringBuilder();
        body.AppendLine("<!DOCTYPE html>");
        body.AppendLine("<html lang=\"en\">");
        body.AppendLine("<head>");
        body.AppendLine("  <meta charset=\"utf-8\">");
        body.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        body.AppendLine("  <title>GitCrawler Daily Digest</title>");
        body.AppendLine("</head>");
        body.AppendLine("<body style=\"margin:0;padding:0;background:#ded2bd;font-family:Arial, Helvetica, sans-serif;color:#22302a;\">");
        body.AppendLine("  <div style=\"padding:24px 16px;\">");
        body.AppendLine("    <div style=\"max-width:760px;margin:0 auto;background:#f6efe5;border:1px solid #d7c7a8;border-radius:24px;overflow:hidden;box-shadow:0 12px 28px rgba(31,41,34,0.12);\">");
        body.AppendLine("      <div style=\"background:#1f2922;padding:24px 28px;color:#f9f4ea;\">");
        body.AppendLine("        <div style=\"font-size:12px;letter-spacing:0.16em;text-transform:uppercase;color:#aebead;\">git-crawler</div>");
        body.AppendLine($"        <div style=\"font-size:28px;font-weight:700;margin-top:6px;\">Daily Digest · {today:yyyy-MM-dd}</div>");
        body.AppendLine("        <div style=\"margin-top:10px;display:flex;flex-wrap:wrap;gap:8px;\">");
        body.AppendLine("          <span style=\"padding:7px 12px;border-radius:999px;background:#d4b16a;color:#1f2922;font-size:12px;font-weight:700;\">Hidden Gems</span>");
        body.AppendLine("          <span style=\"padding:7px 12px;border-radius:999px;border:1px solid #5c745f;color:#dfe9de;font-size:12px;\">Trending</span>");
        body.AppendLine("        </div>");
        body.AppendLine("      </div>");
        body.AppendLine("      <div style=\"padding:24px 28px;\">");
        body.AppendLine("        <div style=\"background:#efe7d9;border:1px solid #d9cdb7;border-radius:18px;padding:18px 20px;margin-bottom:20px;\">");
        body.AppendLine("          <div style=\"font-size:12px;letter-spacing:0.16em;text-transform:uppercase;color:#5d6c61;\">today's pulse</div>");
        body.AppendLine($"          <div style=\"font-size:20px;font-weight:700;margin-top:6px;\">{topGems.Count} hidden gems and {trends.Count} trend buckets</div>");
        body.AppendLine("          <div style=\"font-size:14px;line-height:1.6;color:#4c5c50;margin-top:8px;\">This morning’s digest mirrors the dashboard direction with an olive-toned header, card-based sections, and a compact snapshot of the repositories showing the strongest signal.</div>");
        body.AppendLine("        </div>");

        body.AppendLine("        <div style=\"margin-bottom:20px;\">");
        body.AppendLine("          <div style=\"font-size:12px;letter-spacing:0.16em;text-transform:uppercase;color:#5d6c61;margin-bottom:8px;\">Top Hidden Gems</div>");

        if (topGems.Count == 0)
        {
            body.AppendLine("          <div style=\"background:#ffffff;border:1px solid #e3d9c7;border-radius:16px;padding:16px 18px;color:#4c5c50;\">No hidden gems to report today.</div>");
        }
        else
        {
            body.AppendLine("          <div style=\"display:grid;grid-template-columns:repeat(2, minmax(0, 1fr));gap:12px;\">");
            var rank = 1;
            foreach (var gem in topGems)
            {
                var score = gem.LatestScore!.TotalScore;
                var summary = gem.LatestSummary!.ShortContent;
                var repoLabel = $"{gem.Repository.Owner}/{gem.Repository.Name}";
                var repoUrl = string.IsNullOrWhiteSpace(gem.Repository.Url) ? string.Empty : gem.Repository.Url;
                var safeRepoLabel = System.Net.WebUtility.HtmlEncode(repoLabel);
                var safeRepoUrl = System.Net.WebUtility.HtmlEncode(repoUrl);

                body.AppendLine("            <div style=\"background:#ffffff;border:1px solid #e3d9c7;border-radius:16px;padding:16px 18px;\">");
                body.AppendLine($"              <div style=\"font-size:12px;letter-spacing:0.16em;text-transform:uppercase;color:#7c8f7d;\">#{rank}</div>");
                body.AppendLine($"              <div style=\"font-size:18px;font-weight:700;margin-top:4px;\">{safeRepoLabel}</div>");
                body.AppendLine($"              <div style=\"font-size:13px;color:#6f7f73;margin-top:4px;\">Score {Math.Round(score)} · {summary}</div>");
                if (!string.IsNullOrWhiteSpace(repoUrl))
                {
                    body.AppendLine($"              <div style=\"margin-top:8px;\"><a href=\"{safeRepoUrl}\" style=\"color:#294a3b;text-decoration:none;font-size:12px;font-weight:700;\">Open on GitHub ↗</a></div>");
                }
                body.AppendLine("            </div>");
                rank++;
            }
            body.AppendLine("          </div>");
        }

        body.AppendLine("        </div>");

        body.AppendLine("        <div>");
        body.AppendLine("          <div style=\"font-size:12px;letter-spacing:0.16em;text-transform:uppercase;color:#5d6c61;margin-bottom:8px;\">Trend Summary</div>");

        if (trends.Count == 0)
        {
            body.AppendLine("          <div style=\"background:#ffffff;border:1px solid #e3d9c7;border-radius:16px;padding:16px 18px;color:#4c5c50;\">No trend data available for the current period.</div>");
        }
        else
        {
            body.AppendLine("          <div style=\"display:grid;grid-template-columns:repeat(4, minmax(0, 1fr));gap:12px;\">");
            foreach (var trend in trends)
            {
                var trendLabel = System.Net.WebUtility.HtmlEncode(trend.Category);
                var trendCount = $"{trend.RepositoryCount} repos";
                body.AppendLine("            <div style=\"background:#ffffff;border:1px solid #e3d9c7;border-radius:16px;padding:16px 14px;display:flex;flex-direction:column;align-items:center;text-align:center;gap:10px;\">");
                body.AppendLine("              <div style=\"width:56px;height:56px;border-radius:50%;background:#d4b16a;display:grid;place-items:center;\">");
                body.AppendLine("                <svg width=\"24\" height=\"24\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"#f9f4ea\" stroke-width=\"2.75\" stroke-linecap=\"round\" stroke-linejoin=\"round\"><path d=\"M5 18V8l7-4 7 4v10\"></path><path d=\"M5 18h14\"></path></svg>");
                body.AppendLine("              </div>");
                body.AppendLine($"              <div style=\"font-family:Arial, Helvetica, sans-serif;font-size:15px;font-weight:700;color:#22302a;\">{trendLabel}</div>");
                body.AppendLine($"              <span style=\"padding:4px 10px;border-radius:999px;background:#efe7d9;color:#4c5c50;font-size:12px;\">{trendCount}</span>");
                body.AppendLine("            </div>");
            }
            body.AppendLine("          </div>");
        }

        body.AppendLine("        </div>");
        body.AppendLine("      </div>");
        body.AppendLine("    </div>");
        body.AppendLine("  </div>");
        body.AppendLine("</body>");
        body.AppendLine("</html>");

        return body.ToString();
    }
}
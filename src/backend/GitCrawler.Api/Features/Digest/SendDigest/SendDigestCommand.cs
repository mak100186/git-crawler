using System.Globalization;
using System.Text;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Repositories;
using GitCrawler.Api.Features.Scoring.ComputeScores;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Digest.SendDigest;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder. Triggered by F-006's Job Scheduler via
// SendDigestJob.RunAsync's IMessageBus.InvokeAsync call, on its own daily RecurringJob rather than
// chained onto AggregateTrendsJob (see SendDigestJob's header comment for why) - so, like every
// other pipeline-stage command in this codebase, this is a plain command with no HTTP endpoint.
public record SendDigestCommand(bool IsHtml = true);

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
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        // F-016/NFR-003: sequential-retry dedupe, checked first and before any composition/query
        // work below - Hangfire's own automatic-retry (default 10 attempts, unconfigured anywhere
        // in this codebase) re-invokes this command if the process dies right after
        // emailSender.SendAsync succeeds further down but before the job is marked Succeeded.
        // [DisableConcurrentExecution] (F-016 part A, on SendDigestJob) only closes the
        // *simultaneous*-overlap window between two truly concurrent executions; this is a
        // different, sequential scenario, so it needs its own independent guard - see
        // DigestSendLog's own header comment. Mirrors the early-return shape of the "no recipient
        // configured" check just below.
        if (await dbContext.DigestSendLogs.AnyAsync(d => d.SentForDate == today, cancellationToken))
        {
            logger.LogInformation("Daily digest for {Date} was already sent; skipping this invocation.", today);
            return new SendDigestResult(Sent: false, RepositoryCount: 0, TrendCount: 0);
        }

        if (string.IsNullOrWhiteSpace(_recipientEmail))
        {
            logger.LogWarning("Digest:RecipientEmail is not configured; skipping the daily digest send.");
            return new SendDigestResult(Sent: false, RepositoryCount: 0, TrendCount: 0);
        }

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
            .Take(8)
            .ToListAsync(cancellationToken);

        // Week-over-week growth for each trend category's card (design brief §3 Trending's
        // "▲ +18% this week" pill) - ideally compares today's RepositoryCount against the row for
        // the same category exactly 7 days ago. A brand-new pipeline (or a brand-new category)
        // won't have a full week of history yet, so rather than requiring an exact 7-days-ago match
        // and showing no pill at all until then, this picks whichever prior row for that category
        // is *closest* to 7 days back - "best you can with existing and available data" - and
        // BuildGrowthPillHtml labels the pill with the real elapsed days ("this week" only when the
        // baseline genuinely is 7 days old, "in Nd" otherwise) so a shorter comparison window is
        // never misrepresented as a full week. A category with no prior row at all (nothing before
        // today) still gets no pill - never a divide-by-zero "infinite%".
        var idealBaselineDate = today.AddDays(-7);
        var trendCategories = trends.Select(t => t.Category).ToList();
        var priorTrendRows = await dbContext.TrendAggregates
            .Where(t => t.PeriodEnd < today && trendCategories.Contains(t.Category))
            .ToListAsync(cancellationToken);
        var previousBaselineByCategory = priorTrendRows
            .GroupBy(t => t.Category)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(t => Math.Abs(t.PeriodEnd.DayNumber - idealBaselineDate.DayNumber)).First());

        var subject = $"GitCrawler Daily Digest — {today:yyyy-MM-dd}";

        var body = command.IsHtml
            ? ComposeHtmlBody(today, topGems, trends, previousBaselineByCategory)
            : ComposeBody(today, topGems, trends);

        try
        {
            await emailSender.SendAsync(new EmailMessage(_recipientEmail, subject, body, command.IsHtml), cancellationToken);
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

        // Marker written only after SendAsync above has already succeeded (Task Packet's explicit
        // ordering requirement) - if this SaveChangesAsync itself fails, or the process dies before
        // it commits, the next retry simply re-sends once more, same as this codebase's pre-F-016
        // behavior. No elaborate outbox/rollback pattern here to close that narrower residual
        // gap - this feature's Constraints explicitly call for not over-building the defense-in-depth
        // around these guards.
        dbContext.DigestSendLogs.Add(new DigestSendLog { SentForDate = today, SentAtUtc = timeProvider.GetUtcNow() });
        await dbContext.SaveChangesAsync(cancellationToken);

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

    // Palette for the two still-C#-generated repeating sections (empty-state divs, gem/trend cards)
    // - centralized so a future tweak to any of these is a one-place edit, same "single source of
    // truth" philosophy this codebase applies to config defaults (AggregateTrendsCommandHandler
    // ._periodDays and friends). The static chrome's own colors (header, wrapper, background) live
    // as plain literals directly in DigestEmailTemplate.html instead - deliberately not tokenized
    // through here, so that file stays editable as ordinary HTML/CSS (by hand or a design tool)
    // without needing to know these constant names. That is a real duplication risk between the two
    // files for colors both happen to use (e.g. the border/shadow tones) - worth knowing if either
    // one's palette changes independently, not something a build-time check catches.
    // BorderColor was originally color-mix(in srgb, #201e1d 16%, transparent), then rgba(32, 30,
    // 29, 0.16) once color-mix() proved unsupported - but rgba() itself later showed real gaps too
    // (all of Outlook Windows 2007-2019, WEB.DE, GMX, Yahoo, AOL, Apple Mail iOS 11, Orange; Gmail
    // strips the whole style attribute/<style> block outright if it sees rgba()'s whitespace
    // syntax). Now a flat solid hex instead - the real --color-neutral-300 token (styles.scss),
    // i.e. what rgba(32, 30, 29, 0.16) blends to against this email's #f5ead8 page background - so
    // every client sees the same border color instead of degrading to opaque black on the ones that
    // ignore the alpha channel. No ShadowLight constant here anymore either - box-shadow was
    // dropped from every card (see
    // DigestEmailTemplate.html's header comment) once caniemail showed it unsupported or partial
    // across most of Gmail/Yahoo/Outlook/AOL/GMX/Orange, not just Outlook desktop's usual holdout;
    // the 1px BorderColor border alone now carries card definition.
    private const string CardBg = "#f9f4ed";
    private const string ChipBg = "#ebddc5";
    private const string TrackBg = "#eee7db"; // real --color-neutral-200 (styles.scss) - progress-bar track behind ScoreBadgeBg fills
    private const string Ink = "#201e1d";
    private const string BodyText = "#474238";
    private const string Muted = "#645c50";
    private const string Accent = "#8c491a";
    private const string ScoreBadgeBg = "#c67139";
    private const string TrendBadgeBg = "#b2622d";
    // Real shipped colors (styles.scss --color-olive-800/-100) for the growth pill - the design
    // brief's own mockup used placeholder var(--color-accent-2-800/-100) names that never made it
    // into the actual app; these are what those names became once F-011 implemented the real theme.
    private const string Olive800 = "#3d472b";
    private const string Olive100 = "#f0fae1";
    private const string BorderColor = "#dcd3c4"; // real --color-neutral-300 (styles.scss) - was rgba(32, 30, 29, 0.16), see comment above

    // Loaded once (this file never changes at runtime) from the embedded DigestEmailTemplate.html -
    // resolved by suffix rather than a hardcoded full manifest-resource name, so a future
    // RootNamespace change can't silently break this without an equally-silent build change too.
    private static readonly string DigestTemplate = LoadDigestTemplate();

    private static string LoadDigestTemplate()
    {
        var assembly = typeof(SendDigestCommandHandler).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("DigestEmailTemplate.html", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    // Fills DigestEmailTemplate.html's four {{TOKEN}} placeholders. The template owns the static
    // chrome (head/style/wrapper table/header) as plain editable HTML; only the repeating/data-
    // dependent sections still need generating here.
    private static string ComposeHtmlBody(
        DateOnly today,
        IReadOnlyList<RankedRepository> topGems,
        IReadOnlyList<TrendAggregate> trends,
        IReadOnlyDictionary<string, TrendAggregate> previousBaselineByCategory)
    {
        var topGemsContent = topGems.Count == 0
            ? $"<div style=\"background:{ChipBg};border:1px solid {BorderColor};border-radius:16px;padding:16px 18px;color:{BodyText};\">No hidden gems to report today.</div>"
            : BuildRowsTable(topGems, columnsPerRow: 2, fillerColumnClass: "gem-col", BuildGemCardCell);

        var trendContent = trends.Count == 0
            ? $"<div style=\"background:{ChipBg};border:1px solid {BorderColor};border-radius:16px;padding:16px 18px;color:{BodyText};\">No trend data available for the current period.</div>"
            : BuildRowsTable(trends, columnsPerRow: 4, fillerColumnClass: "trend-col",
                trend => BuildTrendCardCell(trend, previousBaselineByCategory.TryGetValue(trend.Category, out var previousBaseline) ? previousBaseline : null));

        return DigestTemplate
            .Replace("{{DIGEST_DATE}}", today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Replace("{{HOW_WE_SCORE_CONTENT}}", BuildHowWeScoreContent())
            .Replace("{{TOP_GEMS_CONTENT}}", topGemsContent)
            .Replace("{{TREND_CONTENT}}", trendContent);
    }

    // Design reference: docs/design-briefs/dashboard-design-directions-exploration/project/
    // Dashboard Design.dc.html's "02 Hidden Gems" screen, whose score-breakdown block shows each
    // signal as a label + weight-bar + percentage. That screen renders one repo's own *computed*
    // signal strengths; this section is deliberately the methodology instead - the five
    // ScoringWeights constants themselves, read live so this can never silently drift out of sync
    // with the actual scoring formula if the weights are ever retuned.
    private static string BuildHowWeScoreContent()
    {
        var signals = new (string Label, double Weight)[]
        {
            ("License", ScoringWeights.LicenseWeight),
            ("Commits / week", ScoringWeights.CommitsPerWeekWeight),
            ("Contributors", ScoringWeights.ContributorCountWeight),
            ("Forks", ScoringWeights.ForkCountWeight),
            ("Stars", ScoringWeights.StarCountWeight),
        };

        var content = new StringBuilder();
        content.AppendLine($"<div style=\"font-size:12.5px;line-height:1.5;color:{BodyText};margin-bottom:14px;\">Every hidden gem's score blends five signals, weighted by how strongly each predicts a well-maintained, actively developed project:</div>");

        foreach (var (label, weight) in signals)
        {
            var pctLabel = (weight * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%";
            content.AppendLine("<div style=\"margin-bottom:10px;\">");
            content.AppendLine("  <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\"><tr>");
            content.AppendLine($"    <td style=\"font-size:12px;color:{Ink};\">{System.Net.WebUtility.HtmlEncode(label)}</td>");
            content.AppendLine($"    <td align=\"right\" style=\"font-size:11px;color:{Muted};\">{pctLabel}</td>");
            content.AppendLine("  </tr></table>");
            // A two-cell <table> instead of a filled inner <div> clipped via overflow:hidden -
            // caniemail shows overflow only 48% fully supported (the rest either strip it via JS or
            // simply ignore it, letting the fill bleed past the track), so a table cell sized to the
            // percentage is used instead: each <td>'s own background naturally stops at its own
            // width with no clipping property required at all. font-size:1px + line-height:5px +
            // &nbsp; is the standard email trick to force a cell to hold a specific height even
            // though it has no real text content.
            content.AppendLine($"  <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin-top:3px;\"><tr>");
            content.AppendLine($"    <td width=\"{pctLabel}\" style=\"height:5px;font-size:1px;line-height:5px;background:{ScoreBadgeBg};\">&nbsp;</td>");
            content.AppendLine($"    <td style=\"height:5px;font-size:1px;line-height:5px;background:{TrackBg};\">&nbsp;</td>");
            content.AppendLine("  </tr></table>");
            content.AppendLine("</div>");
        }

        return content.ToString();
    }

    // CSS Grid's replacement (grid-template-columns has no reliable email-client support): a
    // <table> never auto-wraps like grid/flex do, so this pairs items into <tr>s itself,
    // columnsPerRow at a time. A short final row gets blank filler <td>s so its siblings don't
    // stretch to fill the row - shared by both the gems (2/row) and trends (4/row) tables, the only
    // difference between them being the cell-builder function and the CSS class the @media query
    // hooks to collapse that column to full width on a narrow viewport.
    private static string BuildRowsTable<T>(IReadOnlyList<T> items, int columnsPerRow, string fillerColumnClass, Func<T, string> buildCell)
    {
        var columnWidthPercent = 100 / columnsPerRow;
        var table = new StringBuilder();
        table.AppendLine("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\">");
        for (var i = 0; i < items.Count; i += columnsPerRow)
        {
            table.AppendLine("  <tr>");
            for (var column = i; column < i + columnsPerRow; column++)
            {
                table.AppendLine(column < items.Count
                    ? buildCell(items[column])
                    : $"    <td class=\"{fillerColumnClass}\" width=\"{columnWidthPercent}%\">&nbsp;</td>");
            }
            table.AppendLine("  </tr>");
        }
        table.Append("</table>");
        return table.ToString();
    }

    private static string BuildGemCardCell(RankedRepository gem)
    {
        var score = gem.LatestScore!.TotalScore;
        var summary = gem.LatestSummary!.ShortContent;
        var repoUrl = string.IsNullOrWhiteSpace(gem.Repository.Url) ? string.Empty : gem.Repository.Url;
        var safeRepoLabel = System.Net.WebUtility.HtmlEncode(gem.Repository.Name);
        var safeSummary = System.Net.WebUtility.HtmlEncode(TruncateSummary(summary));
        var safeOwner = System.Net.WebUtility.HtmlEncode(gem.Repository.Owner);
        var safeRepoUrl = System.Net.WebUtility.HtmlEncode(repoUrl);

        var cell = new StringBuilder();
        cell.AppendLine("                  <td class=\"gem-col\" width=\"50%\" valign=\"top\" style=\"padding:6px;\">");
        // border-collapse:separate overrides the template's global `table { border-collapse:
        // collapse; }` reset - background/border/border-radius/box-shadow live on this single
        // content <td> rather than the <table> itself, since border-radius on a <table> with
        // collapsed borders doesn't clip reliably in Chromium/most email renderers (the collapsed
        // border paints as a sharp rectangle underneath the rounded box) - a <td>'s own border-radius
        // isn't subject to that table-border-painting quirk at all.
        cell.AppendLine("                    <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:separate;\">");
        cell.AppendLine("                      <tr>");
        cell.AppendLine($"                        <td style=\"background:{CardBg};border:1px solid {BorderColor};border-radius:16px;padding:16px 18px;\">");
        // Icon-and-label row: a nested, non-full-width <table> (badge <td> + text <td>) rather than
        // display:flex - the standard email-safe substitute for "icon next to two-line text".
        cell.AppendLine("                          <table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\"><tr>");
        cell.AppendLine($"                            <td width=\"52\" style=\"width:52px;height:52px;border-radius:50%;background:{ScoreBadgeBg};text-align:center;\">");
        // line-height matching the <td>'s own height is the classic email-safe vertical-centering
        // trick (place-items:center has ~no Outlook/Windows Mail support) - bulletproof regardless
        // of client, unlike relying on flex/grid centering.
        cell.AppendLine($"                              <span style=\"font-family:Georgia, serif;font-size:18px;color:{CardBg};line-height:52px;\">{Math.Round(score)}</span>");
        cell.AppendLine("                            </td>");
        cell.AppendLine("                            <td style=\"padding-left:12px;\" valign=\"middle\">");
        cell.AppendLine($"                              <div style=\"font-family:Georgia, serif;font-size:16px;font-weight:bold;line-height:1.2;color:{Ink};\">{safeRepoLabel}</div>");
        cell.AppendLine($"                              <div style=\"font-size:11px;color:{Muted};margin-top:2px;\">{safeOwner}</div>");
        cell.AppendLine("                            </td>");
        cell.AppendLine("                          </tr></table>");
        cell.AppendLine($"                          <div style=\"font-size:12.5px;line-height:1.5;color:{BodyText};margin-top:9px;\">{safeSummary}</div>");

        if (!string.IsNullOrWhiteSpace(repoUrl))
        {
            cell.AppendLine($"                          <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-top:1px solid {BorderColor};margin-top:9px;\"><tr>");
            cell.AppendLine($"                            <td align=\"left\" style=\"padding-top:9px;font-size:10.5px;color:{Accent};font-weight:bold;\">");
            cell.AppendLine($"                              <span>★ {FormatCount(gem.Repository.StarCount)}</span>&nbsp;&nbsp;<span>⫱ {FormatCount(gem.Repository.ForkCount)}</span>");
            cell.AppendLine("                            </td>");
            cell.AppendLine($"                            <td align=\"right\" style=\"padding-top:9px;font-size:12px;color:{Accent};font-weight:bold;\">");
            cell.AppendLine($"                              <a href=\"{safeRepoUrl}\" style=\"color:{Accent};text-decoration:none;\">Open on GitHub ↗</a>");
            cell.AppendLine("                            </td>");
            cell.AppendLine("                          </tr></table>");
        }

        cell.AppendLine("                        </td>");
        cell.AppendLine("                      </tr>");
        cell.AppendLine("                    </table>");
        cell.Append("                  </td>");
        return cell.ToString();
    }

    private static string BuildTrendCardCell(TrendAggregate trend, TrendAggregate? previousBaseline)
    {
        var safeLabel = System.Net.WebUtility.HtmlEncode(trend.Category);
        var trendCount = $"{trend.RepositoryCount} repositories";
        var growthPillHtml = BuildGrowthPillHtml(trend, previousBaseline);

        var cell = new StringBuilder();
        cell.AppendLine("                  <td class=\"trend-col\" width=\"25%\" valign=\"top\" style=\"padding:6px;\">");
        // Same border-collapse:separate + background/border/radius-on-<td>-not-<table> fix as
        // BuildGemCardCell - see that method's own comment for why (Chromium/most email renderers
        // don't clip a collapsed table's border to its own border-radius).
        cell.AppendLine("                    <table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:separate;\">");
        cell.AppendLine("                      <tr>");
        cell.AppendLine($"                        <td align=\"center\" style=\"background:{ChipBg};border:1px solid {BorderColor};border-radius:16px;padding:16px 14px;\">");
        // <td>'s own default vertical-align:middle centers the glyph inside the circle both ways
        // once the <td> has an explicit height - no place-items:center needed. line-height matching
        // the <td>'s own height is the same email-safe vertical-centering trick BuildGemCardCell's
        // score badge uses.
        cell.AppendLine($"                          <table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" style=\"margin:0 auto;\"><tr><td width=\"56\" style=\"width:56px;height:56px;border-radius:50%;background:{TrendBadgeBg};text-align:center;\"><span style=\"font-size:22px;line-height:56px;color:{Olive100};\">&#8599;</span></td></tr></table>");
        cell.AppendLine($"                          <div style=\"font-family:Georgia, serif;font-size:15px;font-weight:bold;color:{Ink};margin-top:10px;\">{safeLabel}</div>");
        // BuildGrowthPillHtml always returns a pill now (never null) - every trend card carries one,
        // so the layout doesn't have some cards a row taller than others depending on data
        // availability.
        cell.AppendLine($"                          <div style=\"margin-top:6px;\">{growthPillHtml}</div>");
        cell.AppendLine($"                          <span style=\"display:inline-block;margin-top:8px;padding:4px 10px;border-radius:999px;background:#ffffff;color:#4f4f4f;font-size:12px;\">{trendCount}</span>");
        cell.AppendLine("                        </td>");
        cell.AppendLine("                      </tr>");
        cell.AppendLine("                    </table>");
        cell.Append("                  </td>");
        return cell.ToString();
    }

    // Design reference: docs/design-briefs/.../Dashboard Design.dc.html's "03 Trending" screen's
    // "▲ +18% this week" pill - compares this category's RepositoryCount against the closest prior
    // row HandleAsync could find for that category. Always returns a pill (never null/empty) so
    // every trend card looks the same shape regardless of how much history is available - a real
    // percentage once there's a genuine 7-day-or-older baseline to compare against, otherwise a
    // neutral "No change" pill rather than either an empty gap or a percentage computed from too
    // thin a window to mean anything (a 4->6 swing over 2 days of a brand-new pipeline isn't real
    // "+50%" growth, it's noise).
    private static string BuildGrowthPillHtml(TrendAggregate current, TrendAggregate? previousBaseline)
    {
        var daysSinceBaseline = previousBaseline is null ? 0 : current.PeriodEnd.DayNumber - previousBaseline.PeriodEnd.DayNumber;

        if (previousBaseline is null || previousBaseline.RepositoryCount == 0 || daysSinceBaseline < 7)
        {
            return NoChangePillHtml;
        }

        var growthPercent = (current.RepositoryCount - previousBaseline.RepositoryCount) / (double)previousBaseline.RepositoryCount * 100.0;
        var rounded = (int)Math.Round(growthPercent, MidpointRounding.AwayFromZero);
        if (rounded == 0)
        {
            return NoChangePillHtml;
        }

        // Olive (real growth color, matching the design brief's pill) for genuine growth; the same
        // terracotta Accent used elsewhere in this email for a decline - the design brief's own
        // mockup only ever shows the positive case, so the down-state color is this feature's own
        // judgment call, not a design-brief-sourced value.
        var (arrow, background, textColor) = rounded > 0 ? ("▲", Olive800, Olive100) : ("▼", Accent, CardBg);
        var sign = rounded > 0 ? "+" : string.Empty; // negative values already print their own "-"

        // "this week" only when the baseline really is 7 days old - anything older still clears the
        // >= 7 day bar above but isn't exactly a week, so it's labeled with its real elapsed days
        // instead of overstating it as "this week".
        var periodLabel = daysSinceBaseline == 7 ? "this week" : $"in {daysSinceBaseline}d";

        return $"<span style=\"display:inline-block;padding:3px 9px;border-radius:999px;background:{background};color:{textColor};font-size:11px;font-weight:bold;\">{arrow} {sign}{rounded}% {periodLabel}</span>";
    }

    // Same olive tone BuildGrowthPillHtml uses for genuine growth ("the green pill") - a flat,
    // reassuring neutral rather than an alarming/empty state for the two cases with nothing
    // meaningful to report: no baseline at all, or a baseline younger than a full week.
    private static readonly string NoChangePillHtml =
        $"<span style=\"display:inline-block;padding:3px 9px;border-radius:999px;background:{Olive800};color:{Olive100};font-size:11px;font-weight:bold;\">No change</span>";

    // Fork count and the trend badge's arrow used to be inline <svg> glyphs; both are now plain
    // Unicode characters instead (⫱ at the fork-count call site above, &#8599; ↗ at the trend
    // badge call site in BuildTrendCardCell) - caniemail shows embedded <svg> support even worse
    // than box-shadow (58% none), and several major clients (Outlook Windows, Gmail, Yahoo, AOL)
    // don't degrade an unsupported <svg> gracefully, they strip the element and any fallback
    // content outright. A plain-text glyph can't be stripped that way - it renders via whatever
    // font/emoji support the client already has for ordinary text.

    // Was CSS max-height:38px + overflow:hidden (a 2-line-clamp emulation) on the summary <div> -
    // caniemail shows overflow only 48% fully supported, so a client that ignores it (or only
    // partially honors overflow-block/-inline) renders the full untruncated summary instead,
    // stretching that one card taller than its row-mates with no clipping to fall back on.
    // Truncating the text itself server-side guarantees every client renders an identically-sized
    // summary regardless of overflow support - no CSS property required at all.
    private static string TruncateSummary(string summary)
    {
        const int maxLength = 130;
        return summary.Length <= maxLength ? summary : string.Concat(summary.AsSpan(0, maxLength).TrimEnd(), "…");
    }

    // GitHub-style abbreviation (27,400 -> "27.4K", 27,000 -> "27K", 1,200,000 -> "1.2M") - "0.#"
    // rounds to one decimal and drops a trailing ".0" on its own, so a round thousand doesn't print
    // an empty-looking ".0". Invariant culture: this runs on whatever locale the host process has,
    // and a comma-decimal locale silently corrupting "27.4K" into "27,4K" in an outbound email isn't
    // something to risk for a value nobody's meant to localize.
    private static string FormatCount(int count) => count switch
    {
        >= 1_000_000 => $"{(count / 1_000_000.0).ToString("0.#", CultureInfo.InvariantCulture)}M",
        >= 1_000 => $"{(count / 1_000.0).ToString("0.#", CultureInfo.InvariantCulture)}K",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };
}
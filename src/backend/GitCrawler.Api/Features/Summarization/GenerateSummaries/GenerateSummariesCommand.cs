using System.Net;
using System.Net.Http.Json;
using System.Text;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Crawling.DiscoverRepositories;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Features.Summarization.GenerateSummaries;

// Wolverine message + result for this slice. No shared service/repository layer per ADR-015 -
// everything this operation needs lives in this folder. Triggered by F-006's Job Scheduler via
// GenerateSummariesJob.RunAsync's IMessageBus.InvokeAsync call, chained after ComputeScoresJob
// finishes (see that class) - so, like ComputeScoresCommand, this is a plain command with no HTTP
// endpoint.
public record GenerateSummariesCommand;

public record GenerateSummariesResult(int SummarizedCount, int SkippedCount, int FailedCount);

// Wolverine discovers this handler by convention (a public Handle/HandleAsync method on a class
// named *Handler in the same assembly) - no manual registration required.
public class GenerateSummariesCommandHandler(
    GitCrawlerDbContext dbContext,
    IRepositorySummarizer summarizer,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GenerateSummariesCommandHandler> logger,
    TimeProvider timeProvider)
{
    // "Top-scored" is only named qualitatively upstream (Architecture §3/PRD FR-003), with no
    // concrete number given anywhere - the same gap F-005's DiscoveryLookbackDays/
    // DiscoveryMinimumStars and F-007's ScoringWeights hit for their own thresholds (Task Packet:
    // "use your judgment, document your choice"). ScoringWeights.ComputeTotalScore scales to 0-100
    // (see that class); 40 is this feature's v1 default - clears repos with a genuinely weak mix
    // across all five weighted signals (license/commits/contributors/forks/stars) while not being
    // so strict that only a handful of repos ever qualify for the LM Studio throughput budget.
    // Operator-tunable via config, not a spec'd threshold.
    private readonly double _minimumScore = configuration.GetValue("Summarization:MinimumScore", 40.0);

    // Bounds how many repos get summarized in a single run. LM Studio inference is the bottleneck
    // here (NFR-001: seconds per repo, not DB I/O) - an unbounded batch would make one run's
    // duration grow unpredictably with however large the not-yet-summarized backlog is. 20 keeps a
    // single run's worst case in the low minutes even at several seconds/repo; nothing is lost by
    // deferring the rest - a repo that misses this run's cap still has no Summary row, so it's
    // picked up automatically on the next scheduled run (see the "without one" filter below).
    private readonly int _batchSize = configuration.GetValue("Summarization:BatchSize", 20);

    public async Task<GenerateSummariesResult> HandleAsync(GenerateSummariesCommand command, CancellationToken cancellationToken)
    {
        // "Without one" = no Summary row yet, full stop. Unlike Score, which intentionally keeps
        // history and is recomputed on every re-crawl (see ComputeScoresCommandHandler's own
        // comment), a Summary is never regenerated once created - re-summarizing on every run would
        // re-spend LM Studio's per-repo throughput budget (the actual bottleneck, NFR-001) for no
        // product benefit, since the README/manifest content a summary is based on doesn't change
        // often enough to justify burning inference time on repos that already have one. This is a
        // deliberate divergence from the Scoring Engine's re-scoring-on-recrawl behavior (Task
        // Packet's explicit callout).
        //
        // Loaded into memory before filtering/ranking by score, same rationale as
        // ComputeScoresCommandHandler's own "repositoriesWithScores" load: resolving each repo's
        // *latest* Score.TotalScore needs to behave identically on the xUnit suite's SQLite
        // provider and the real Npgsql/Postgres provider, and this pipeline has no pagination
        // anywhere else at this single-operator-v1 scale - see that class's comment for the full
        // portability rationale, which applies unchanged here.
        var candidates = await dbContext.Repositories
            .Include(r => r.Scores)
            .Where(r => !r.Summaries.Any())
            .ToListAsync(cancellationToken);

        // The "latest" Score is the one with the max ComputedAtUtc (chronologically most recent),
        // not the one with the highest TotalScore - the same distinction ComputeScoresCommandHandler
        // draws for its own re-scoring check (repository.Scores.Max(s => s.ComputedAtUtc)). A repo
        // that scored well once but has since gone stale (fewer commits/contributors on a re-crawl)
        // must be judged on its current standing, not its historical peak - otherwise a repo that
        // dips below MinimumScore on a later re-score could still get summarized (and, since
        // Summary is create-once, permanently so) off an old high score that no longer reflects it.
        var toSummarize = candidates
            .Select(r => (Repository: r, LatestScore: r.Scores.Count == 0 ? (double?)null : r.Scores.OrderByDescending(s => s.ComputedAtUtc).First().TotalScore))
            .Where(x => x.LatestScore is not null && x.LatestScore >= _minimumScore)
            .OrderByDescending(x => x.LatestScore)
            .Take(_batchSize)
            .Select(x => x.Repository)
            .ToList();

        var summarizedCount = 0;
        var failedCount = 0;

        foreach (var repository in toSummarize)
        {
            try
            {
                var readmeContent = await TryFetchReadmeAsync(repository.Owner, repository.Name, cancellationToken);

                var context = new RepositorySummarizationContext(
                    repository.Owner,
                    repository.Name,
                    repository.PrimaryLanguage,
                    repository.LicenseName,
                    readmeContent);

                var content = await summarizer.SummarizeAsync(context, cancellationToken);

                dbContext.Summaries.Add(new Summary
                {
                    RepositoryId = repository.Id,
                    Content = content,
                    GeneratedAtUtc = timeProvider.GetUtcNow(),
                });

                summarizedCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Same philosophy as F-005's GitHubContributorListUnavailableException handling
                // (Task Packet): LM Studio (or GitHub, for the README fetch above) being
                // unreachable/erroring for one repo must not abort the whole batch. No bespoke
                // Polly resilience pipeline here, unlike ADR-018's Crawler pipeline - that ADR was
                // specifically about GitHub's rate-limit *signals* (exact reset times), which LM
                // Studio's local API has no equivalent of; a bounded response (log and skip, zero
                // retries) is enough for a same-process local inference call. The repo is simply
                // retried on the next scheduled run - it still has no Summary row, so the "without
                // one" filter above picks it back up automatically.
                logger.LogWarning(ex, "Summarization failed for {Owner}/{Name}; skipping", repository.Owner, repository.Name);
                failedCount++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // SkippedCount covers everything that didn't even get attempted this run: repos with no
        // score yet, repos below MinimumScore, and repos that qualified but fell past BatchSize -
        // all three are "try again next run" cases, not failures, so they're not folded into
        // FailedCount (which is reserved for attempts that actually threw).
        return new GenerateSummariesResult(summarizedCount, candidates.Count - toSummarize.Count, failedCount);
    }

    // GET /repos/{owner}/{repo}/readme (GitHub REST) - the cheapest PRD-compliant way to read a
    // repo's README without a full git clone (PRD Out of Scope explicitly carves out "selective
    // cloning... to read a manifest file the API doesn't expose directly" while ruling out
    // "routine/bulk repository cloning"; this single-file REST fetch is that selective case, and
    // doesn't even need a clone). Reuses GitHubDiscoveryClient.GitHubRestClientName's named
    // HttpClient (already configured in Program.cs with base address, auth header, and API version
    // header for F-005) rather than standing up a second GitHub REST client for one more endpoint.
    private async Task<string?> TryFetchReadmeAsync(string owner, string name, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(GitHubDiscoveryClient.GitHubRestClientName);

        using var response = await client.GetAsync($"repos/{owner}/{name}/readme", cancellationToken);

        // No README is a legitimate, non-error result (Task Packet's explicit callout) - the repo
        // is still summarized from whatever else is available (PrimaryLanguage/LicenseName), not
        // failed for the whole run over one missing file.
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GitHubReadmeResponse>(cancellationToken);
        if (string.IsNullOrEmpty(payload?.Content))
        {
            return null;
        }

        // GitHub returns README content base64-encoded with embedded newlines inside the base64
        // blob itself (not just in the decoded text) - Convert.FromBase64String throws on those
        // unless they're stripped first.
        var base64 = payload.Content.Replace("\n", string.Empty);
        return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
    }

    private sealed record GitHubReadmeResponse(string? Content);
}
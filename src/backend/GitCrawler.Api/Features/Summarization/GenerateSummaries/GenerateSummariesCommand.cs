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

// StoppedOnRateLimit distinguishes "this run summarized 3 of 20 because GitHub cut us off" from
// "this run summarized 3 of 20 because 17 individual repositories failed" (review finding M-3). Both
// used to look identical in the logs, and the first is not a summarization problem at all.
public record GenerateSummariesResult(int SummarizedCount, int SkippedCount, int FailedCount, bool StoppedOnRateLimit = false);

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
        // Loaded into memory before filtering/ranking by score, same as
        // ComputeScoresCommandHandler's own "repositoriesWithScores" load. The portability
        // rationale both once gave is gone with review finding H-4; what remains is that this
        // pipeline has no pagination anywhere else at this single-operator-v1 scale. Bounding it is
        // review finding H-3.
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
        var stoppedOnRateLimit = false;

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

                var result = await summarizer.SummarizeAsync(context, cancellationToken);

                dbContext.Summaries.Add(new Summary
                {
                    RepositoryId = repository.Id,
                    ShortContent = result.ShortSummary,
                    DetailedContent = result.DetailedSummary,
                    GeneratedAtUtc = timeProvider.GetUtcNow(),
                });

                summarizedCount++;
            }
            catch (GitHubRateLimitException ex)
            {
                // Back the whole batch out rather than failing each repository in turn (review
                // finding M-3). A rate limit is one root cause affecting every remaining repository
                // in this run, not N independent failures: the old behaviour logged up to BatchSize
                // warnings for it, burned the rest of the batch against a budget that was already
                // exhausted, and then did the same thing again an hour later.
                //
                // Backing out rather than waiting is deliberate, and is why this slice does not
                // share the Crawler's Polly pipeline (ADR-018). That pipeline waits indefinitely
                // because a crawl is the whole point of its run; here the README is one optional
                // input to a summary, GitHub's reset can be the better part of an hour away, and
                // this job runs hourly anyway - so stopping is strictly better than holding an
                // LM Studio-bound job open waiting on GitHub. Everything already summarized is
                // saved below; everything else still has no Summary row and is picked back up by
                // the "without one" filter on the next run.
                logger.LogWarning(
                    ex,
                    "GitHub rate limit hit while fetching READMEs; stopping this run after {SummarizedCount} of {BatchCount} repositories. The remainder are retried on the next scheduled run",
                    summarizedCount,
                    toSummarize.Count);
                stoppedOnRateLimit = true;
                break;
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
        // score yet, repos below MinimumScore, repos that qualified but fell past BatchSize, and -
        // since M-3 - repos left unattempted when a rate limit cut the batch short. All are "try
        // again next run" cases, not failures, so they're not folded into FailedCount (which is
        // reserved for attempts that actually threw). Derived by subtraction rather than from
        // toSummarize.Count so the early-exit case is counted correctly; when the loop runs to
        // completion the two are identical, since every iteration increments exactly one counter.
        return new GenerateSummariesResult(
            summarizedCount,
            candidates.Count - summarizedCount - failedCount,
            failedCount,
            stoppedOnRateLimit);
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

        // The crawler's own REST calls run through ADR-018's Polly pipeline; this one does not, so
        // without these two checks a 403 would fall through to EnsureSuccessStatusCode, surface as
        // a generic HttpRequestException, and be logged as "summarization failed for this repo" -
        // once per repository in the batch, for a cause that has nothing to do with any of them
        // (review finding M-3). Detection is shared with GitHubDiscoveryClient rather than
        // reimplemented, so the two cannot drift apart on GitHub's header contract.
        if (GitHubDiscoveryClient.IsRestPrimaryRateLimited(response, out var resetAtUtc))
        {
            throw new GitHubRestRateLimitExceededException(resetAtUtc);
        }

        if (GitHubDiscoveryClient.IsRestSecondaryRateLimited(response, timeProvider, out var retryAfter))
        {
            throw new GitHubSecondaryRateLimitException(retryAfter);
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
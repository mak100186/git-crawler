namespace GitCrawler.Api.Features.Summarization.GenerateSummaries;

// ADR-001's provider-agnostic abstraction: the only way GenerateSummariesCommandHandler is allowed
// to reach an AI backend. LmStudioRepositorySummarizer is the sole implementation today (LM Studio
// + Llama 3.2 3B Instruct, ADR-007/ADR-017), but nothing in the handler or this contract is
// LM-Studio-specific, so a future cloud/alternate-local backend can be swapped in without touching
// the calling code.
public interface IRepositorySummarizer
{
    Task<RepositorySummaryResult> SummarizeAsync(RepositorySummarizationContext context, CancellationToken cancellationToken);
}

// Two distinct summaries per repo (operator direction: "there should be two kinds of summaries:
// short that show on the repo card and then the detailed one") - deliberately one call producing
// both rather than two separate interface methods, so GenerateSummariesCommandHandler's call site
// stays a single "summarize this repo" operation. The two texts are still generated via two
// separate LM Studio calls under LmStudioRepositorySummarizer (operator-confirmed choice over a
// single call with a parsed structured response, prioritizing reliability over halving inference
// time per repo) - that's an implementation detail this contract doesn't need to expose.
public record RepositorySummaryResult(string ShortSummary, string DetailedSummary);

// Exactly the content a summarization prompt needs - not a raw Data.Entities.Repository - so this
// contract stays decoupled from the Data Store schema. ReadmeContent is nullable rather than
// requiring callers to synthesize an empty string: a repo with no README is a legitimate, non-error
// case (PRD Out of Scope / Task Packet's README-fetching judgment call), and the implementation is
// expected to still produce a summary from PrimaryLanguage/LicenseName alone when it's null.
public record RepositorySummarizationContext(
    string Owner,
    string Name,
    string? PrimaryLanguage,
    string? LicenseName,
    string? ReadmeContent);
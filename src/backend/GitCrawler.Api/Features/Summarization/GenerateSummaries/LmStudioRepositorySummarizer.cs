using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace GitCrawler.Api.Features.Summarization.GenerateSummaries;

// Sole implementation of IRepositorySummarizer (ADR-001) for now - calls LM Studio's OpenAI-
// compatible /v1/chat/completions endpoint (ADR-007) running Llama 3.2 3B Instruct (ADR-017,
// supersedes ADR-013). The named HttpClient is registered in Program.cs; injected via
// IHttpClientFactory rather than constructor-injecting HttpClient directly, matching the pattern
// GitHubDiscoveryClient (F-005) already established for named clients in this codebase.
public class LmStudioRepositorySummarizer(IHttpClientFactory httpClientFactory, IConfiguration configuration) : IRepositorySummarizer
{
    // Name of the HttpClientFactory-registered client this class expects (see Program.cs) -
    // exposed as a constant so the registration and the consumer can't drift apart, same pattern as
    // GitHubDiscoveryClient.GitHubRestClientName.
    public const string LmStudioClientName = "LmStudio";

    // Plain-text, single-paragraph output only - no Markdown headings/bullet points/section labels
    // ("Purpose:", "## Tech Stack", etc). The dashboard card renders this in a fixed 3-line CSS
    // clamp (repository-card.scss's -webkit-line-clamp: 3) with no server-side truncation, so any
    // heading text the model emits eats into that budget and can push the actual summary content
    // out of view or crop the visible text mid-word. Asking for the target length directly in the
    // prompt (see BuildUserPrompt's use of maxLength) means the model does the fitting, not a
    // truncation step after the fact.
    private const string ShortSystemPrompt =
        "You are a repository summarizer. Respond with exactly one plain-text summary of about 3 " +
        "short sentences covering purpose, key features, and tech stack. Do not use Markdown, " +
        "headings, bullet points, or section labels (no \"Summary:\", \"Purpose:\", etc.) - return " +
        "only the summary prose itself, nothing else.";

    // The detail dialog (RepositoryDetailPane) has no line clamp - it shows this in full, so it can
    // be substantially longer than the card's short summary. Still plain-text/no-headings for the
    // same reason as the short prompt: the dialog interpolates this directly ({{ }}) rather than
    // rendering Markdown, so literal "**bold**"/"## heading" syntax would show up as-is instead of
    // being formatted (confirmed live - the original single-summary prompt did exactly this before
    // it was split in two). 2-4 short paragraphs gives the model room to actually cover notable
    // caveats, which the short prompt has no space for.
    private const string DetailedSystemPrompt =
        "You are a repository summarizer. Respond with a detailed plain-text summary, 2 to 4 short " +
        "paragraphs, covering the repository's purpose, key features, tech stack, and any notable " +
        "caveats. Do not use Markdown, headings, bullet points, or section labels (no \"Summary:\", " +
        "\"Purpose:\", etc.) - return only the summary prose itself, nothing else.";

    // F-002 spike §3.3's starting point, confirmed still appropriate by ADR-017's live comparison:
    // llama-3.2-3b-instruct finished at max_tokens: 300 with finish_reason "stop" (complete, zero
    // reasoning-token waste) - unlike the originally-pinned Gemma 4 E4B (ADR-013, superseded), which
    // burned 65-86% of this same budget on an invisible reasoning field and truncated its visible
    // summary to 30-60 words (spike §9.4). Task Packet: reuse 300 as the starting point, confirm or
    // adjust and document - this model's own benchmark is the confirmation. Config-overridable
    // (LmStudio:MaxTokens) rather than hardcoded, in case a future model swap needs more headroom.
    // Kept as the short summary's own budget now that summarization is two calls, not one.
    private readonly int _maxTokens = configuration.GetValue("LmStudio:MaxTokens", 300);

    // Separate, larger budget for the detailed call - roughly 900 target characters (see
    // _maxDetailedSummaryLength below) needs more headroom than 300 tokens comfortably allows once
    // the model's own token-to-character ratio is accounted for. Config-overridable
    // (LmStudio:MaxDetailedTokens), same pattern as _maxTokens.
    private readonly int _maxDetailedTokens = configuration.GetValue("LmStudio:MaxDetailedTokens", 500);

    // Target character length told to the model as an explicit instruction (see BuildUserPrompt),
    // not enforced by post-hoc truncation - operator-directed ("adjust prompt and ask for 3 liner
    // (x character summary) so we dont have to truncate"). 220 chosen to fit the dashboard card's
    // 3-line clamp (12.5px font, up to 420px card width) without overflowing. Config-overridable
    // (Summarization:MaxSummaryLength) same pattern as _maxTokens above.
    private readonly int _maxSummaryLength = configuration.GetValue("Summarization:MaxSummaryLength", 220);

    // Detailed summary's own target length - no fixed-height clamp to fit in the dialog, so this is
    // a generous-but-bounded target rather than a tight one, picked to comfortably fit 2-4 short
    // paragraphs. Config-overridable (Summarization:MaxDetailedSummaryLength).
    private readonly int _maxDetailedSummaryLength = configuration.GetValue("Summarization:MaxDetailedSummaryLength", 900);

    // Caps how much of a README's raw text is sent to the model - found the hard way: a live run
    // against openclaw/openclaw (a 111KB/~35,489-token README) came back as an HTTP 400 with body
    // "The number of tokens to keep from the initial prompt is greater than the context length
    // (n_keep: 35489 >= n_ctx: 8192)" - the loaded model is served with an 8192-token context
    // window, and nothing here bounded the README before that point. Any repo with a large enough
    // README hits this, not just that one. 6000 characters is deliberately conservative: the live
    // failure's own numbers put the tokenizer's ratio at ~3.1 chars/token for that README, but
    // code-heavy Markdown (fenced blocks, tables) can tokenize denser than prose - even at a
    // pessimistic 2 chars/token this is ~3000 tokens, leaving comfortable headroom under 8192 once
    // the system/header text and the largest completion budget (_maxDetailedTokens, 500) are also
    // accounted for. A README's opening section (purpose/features/install) is also where a
    // summarizer most needs signal - truncating the tail loses far less than truncating the start
    // would. Config-overridable (Summarization:MaxReadmeCharacters).
    private readonly int _maxReadmeCharacters = configuration.GetValue("Summarization:MaxReadmeCharacters", 6000);

    // Not the LM Studio catalog name (that's only used to load the model, see Makefile's
    // `lms load --identifier`) - this must be the fixed alias LMSTUDIO_IDENTIFIER assigns, bridged
    // into LmStudio:Model by Program.cs. Read once at construction, not per call: it's fixed for
    // this single-operator system's process lifetime, same assumption GitHubDiscoveryClient's own
    // config reads make.
    private readonly string _model = configuration["LmStudio:Model"] ?? throw new InvalidOperationException("LmStudio:Model is not configured.");

    // Two summaries, two separate LM Studio calls (operator-confirmed choice over a single call with
    // a parsed structured response - see RepositorySummaryResult's own comment) - each prompt is
    // tuned for its own length/purpose rather than asking one prompt to produce both and splitting
    // the response apart, which risks the model not following a delimiter format exactly. Run
    // sequentially, not via Task.WhenAll: LM Studio serves one local model instance, so two
    // concurrent requests would contend for the same GPU/CPU rather than actually run in parallel -
    // sequential keeps this simple and doesn't risk request-level contention errors for no real
    // throughput gain.
    public async Task<RepositorySummaryResult> SummarizeAsync(RepositorySummarizationContext context, CancellationToken cancellationToken)
    {
        // Truncated once and reused for both calls (rather than re-truncating per call) - the cap
        // exists solely to fit the model's context window, which doesn't vary between the short and
        // detailed prompts.
        var boundedContext = context with { ReadmeContent = TruncateReadme(context.ReadmeContent) };

        var shortSummary = await CallLmStudioAsync(ShortSystemPrompt, BuildUserPrompt(boundedContext, _maxSummaryLength), _maxTokens, context, cancellationToken);
        var detailedSummary = await CallLmStudioAsync(DetailedSystemPrompt, BuildUserPrompt(boundedContext, _maxDetailedSummaryLength), _maxDetailedTokens, context, cancellationToken);

        return new RepositorySummaryResult(shortSummary, detailedSummary);
    }

    private string? TruncateReadme(string? readmeContent)
    {
        if (readmeContent is null || readmeContent.Length <= _maxReadmeCharacters)
        {
            return readmeContent;
        }

        return readmeContent[.._maxReadmeCharacters] + "\n\n[README truncated for length]";
    }

    private async Task<string> CallLmStudioAsync(string systemPrompt, string userPrompt, int maxTokens, RepositorySummarizationContext context, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(LmStudioClientName);

        var request = new ChatCompletionRequest(
            _model,
            [
                new ChatMessage("system", systemPrompt),
                new ChatMessage("user", userPrompt),
            ],
            0.2,
            maxTokens,
            false);

        using var response = await client.PostAsJsonAsync("v1/chat/completions", request, cancellationToken);

        // EnsureSuccessStatusCode() alone discards the response body, which is exactly where LM
        // Studio puts the actually-useful detail (e.g. "n_keep: 35489 >= n_ctx: 8192" for a
        // too-long prompt) - surfacing it here is what made the openclaw/openclaw 400 diagnosable
        // without a manual repro. logger.LogWarning at the GenerateSummariesCommandHandler catch
        // site already logs the full exception, so this is the only place that needs to read it.
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"LM Studio returned {(int)response.StatusCode} for {context.Owner}/{context.Name}: {errorBody}");
        }

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken);
        var content = body?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            // Treated as a failure the same as an HTTP error - GenerateSummariesCommandHandler
            // catches this per-repo and moves on (Task Packet's failure-handling judgment call), so
            // an empty/malformed completion doesn't silently write a blank Summary row. Either call
            // failing fails the whole repo for this run (no partial short-only/detailed-only Summary
            // row) - it's picked back up on the next run same as any other failure.
            throw new InvalidOperationException($"LM Studio returned an empty completion for {context.Owner}/{context.Name}.");
        }

        return content.Trim();
    }

    private static string BuildUserPrompt(RepositorySummarizationContext context, int maxLength)
    {
        var header =
            $"Repository: {context.Owner}/{context.Name}\n" +
            $"Primary language: {context.PrimaryLanguage ?? "unknown"}\n" +
            $"License: {context.LicenseName ?? "none"}\n" +
            $"Write the summary in {maxLength} characters or fewer.\n\n";

        // No README is a legitimate, non-error input (see IRepositorySummarizer's own comment) -
        // told to the model explicitly rather than sending a blank/missing README section, which
        // would risk the model inventing README content that doesn't exist.
        return string.IsNullOrWhiteSpace(context.ReadmeContent)
            ? header + "No README is available for this repository."
            : header + "README:\n" + context.ReadmeContent;
    }

    private sealed record ChatCompletionRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<ChatMessage> Messages,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("stream")] bool Stream);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record ChatCompletionResponse([property: JsonPropertyName("choices")] IReadOnlyList<ChatChoice>? Choices);

    private sealed record ChatChoice([property: JsonPropertyName("message")] ChatMessage? Message);
}
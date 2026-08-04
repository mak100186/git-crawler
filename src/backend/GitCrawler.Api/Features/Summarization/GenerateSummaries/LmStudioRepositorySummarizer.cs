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
    // prompt (see BuildUserPrompt's use of _maxSummaryLength) means the model does the fitting, not
    // a truncation step after the fact.
    private const string SystemPrompt =
        "You are a repository summarizer. Respond with exactly one plain-text summary of about 3 " +
        "short sentences covering purpose, key features, and tech stack. Do not use Markdown, " +
        "headings, bullet points, or section labels (no \"Summary:\", \"Purpose:\", etc.) - return " +
        "only the summary prose itself, nothing else.";

    // F-002 spike §3.3's starting point, confirmed still appropriate by ADR-017's live comparison:
    // llama-3.2-3b-instruct finished at max_tokens: 300 with finish_reason "stop" (complete, zero
    // reasoning-token waste) - unlike the originally-pinned Gemma 4 E4B (ADR-013, superseded), which
    // burned 65-86% of this same budget on an invisible reasoning field and truncated its visible
    // summary to 30-60 words (spike §9.4). Task Packet: reuse 300 as the starting point, confirm or
    // adjust and document - this model's own benchmark is the confirmation. Config-overridable
    // (LmStudio:MaxTokens) rather than hardcoded, in case a future model swap needs more headroom.
    private readonly int _maxTokens = configuration.GetValue("LmStudio:MaxTokens", 300);

    // Target character length told to the model as an explicit instruction (see BuildUserPrompt),
    // not enforced by post-hoc truncation - operator-directed ("adjust prompt and ask for 3 liner
    // (x character summary) so we dont have to truncate"). 220 chosen to fit the dashboard card's
    // 3-line clamp (12.5px font, up to 420px card width) without overflowing. Config-overridable
    // (Summarization:MaxSummaryLength) same pattern as _maxTokens above.
    private readonly int _maxSummaryLength = configuration.GetValue("Summarization:MaxSummaryLength", 220);

    // Not the LM Studio catalog name (that's only used to load the model, see Makefile's
    // `lms load --identifier`) - this must be the fixed alias LMSTUDIO_IDENTIFIER assigns, bridged
    // into LmStudio:Model by Program.cs. Read once at construction, not per call: it's fixed for
    // this single-operator system's process lifetime, same assumption GitHubDiscoveryClient's own
    // config reads make.
    private readonly string _model = configuration["LmStudio:Model"] ?? throw new InvalidOperationException("LmStudio:Model is not configured.");

    public async Task<string> SummarizeAsync(RepositorySummarizationContext context, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(LmStudioClientName);

        var request = new ChatCompletionRequest(
            _model,
            [
                new ChatMessage("system", SystemPrompt),
                new ChatMessage("user", BuildUserPrompt(context)),
            ],
            0.2,
            _maxTokens,
            false);

        using var response = await client.PostAsJsonAsync("v1/chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken);
        var content = body?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            // Treated as a failure the same as an HTTP error - GenerateSummariesCommandHandler
            // catches this per-repo and moves on (Task Packet's failure-handling judgment call), so
            // an empty/malformed completion doesn't silently write a blank Summary row.
            throw new InvalidOperationException($"LM Studio returned an empty completion for {context.Owner}/{context.Name}.");
        }

        return content.Trim();
    }

    private string BuildUserPrompt(RepositorySummarizationContext context)
    {
        var header =
            $"Repository: {context.Owner}/{context.Name}\n" +
            $"Primary language: {context.PrimaryLanguage ?? "unknown"}\n" +
            $"License: {context.LicenseName ?? "none"}\n" +
            $"Write the summary in {_maxSummaryLength} characters or fewer.\n\n";

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
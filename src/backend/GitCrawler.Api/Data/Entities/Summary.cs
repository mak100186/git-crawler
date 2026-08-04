namespace GitCrawler.Api.Data.Entities;

// Two distinct summaries per repo, both generated together in the same call at create time (never
// regenerated after - see GenerateSummariesCommandHandler's own comment on that). ShortContent is
// what the repo card shows, capped to fit its fixed 3-line CSS clamp with no server-side truncation;
// DetailedContent is what the click-through detail dialog shows, with more room to be thorough.
// Operator direction: "there should be two kinds of summaries: short that show on the repo card and
// then the detailed one." All rows that predated this field were deleted by the migration that added
// it (operator-confirmed - "i dont mind if the existing summaries are deleted") rather than left with
// an empty DetailedContent to special-case client-side; every remaining row always has both fields
// populated together, since Summary's create-once design means a row only ever gets inserted once,
// atomically, by GenerateSummariesCommandHandler.
public class Summary
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }

    public Repository Repository { get; set; } = null!;

    public string ShortContent { get; set; } = string.Empty;

    public string DetailedContent { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; set; }
}
namespace GitCrawler.Api.Data.Entities;

// Each scoring signal is its own column rather than an opaque blob (FR-005) - F-007 (Scoring
// Engine) needs to read/write license presence/type, commits-per-week, contributor count, fork
// count, and star count independently to apply per-signal weights, and the Web API (F-010) needs
// to expose the breakdown, not just a single aggregate number.
public class Score
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }

    public Repository Repository { get; set; } = null!;

    public bool HasLicense { get; set; }

    public string? LicenseType { get; set; }

    public double CommitsPerWeek { get; set; }

    public int ContributorCount { get; set; }

    public int ForkCount { get; set; }

    public int StarCount { get; set; }

    public double TotalScore { get; set; }

    public DateTimeOffset ComputedAtUtc { get; set; }
}
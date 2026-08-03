namespace GitCrawler.Api.Data.Entities;

// GitHubId (not Id) is the key the Crawler (F-005) upserts by - a repository's GitHub-assigned
// numeric ID survives owner/name renames, unlike Owner+Name, so it's the only safe key for
// idempotent re-crawl upserts (see the F-001 spike finding: re-crawling must not create
// duplicate records). Uniqueness is enforced at the schema level - see
// GitCrawlerDbContext.OnModelCreating.
public class Repository
{
    public int Id { get; set; }

    public long GitHubId { get; set; }

    public string Owner { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? PrimaryLanguage { get; set; }

    public int StarCount { get; set; }

    public int ForkCount { get; set; }

    public string? LicenseIdentifier { get; set; }

    public string? LicenseName { get; set; }

    public string DefaultBranch { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? PushedAtUtc { get; set; }

    public DateTimeOffset? LastCrawledAtUtc { get; set; }

    // Cheap commit-activity signal fetched via GraphQL's history(first:1).totalCount (F-001 spike
    // §3) - a total count, not a weekly rate. Deriving CommitsPerWeek from this is F-007's job
    // (Scoring Engine is a pure-computation stage with no external calls per Architecture §3); the
    // Crawler only stores the raw number it can get for free alongside discovery.
    public int? CommitCount { get; set; }

    // Contributor count has no cheap GraphQL field (F-001 spike §2) - it's fetched via a separate
    // REST call (F-005's GitHubDiscoveryClient). ContributorCountFetchedAtUtc backs the spike §7
    // caching cadence: re-crawls skip the REST call unless this is null or older than the
    // freshness window, since contributor count is a slow-moving signal and REST is the tighter
    // rate-limit budget at scale (spike §4).
    public int? ContributorCount { get; set; }

    public DateTimeOffset? ContributorCountFetchedAtUtc { get; set; }

    // GitHub topics (F-010 D1) - a List<string> mapped to Postgres text[] via EF Core's primitive
    // collections support (EF8+; Npgsql maps it to a native array, no explicit HasColumnType
    // needed - verified against the installed Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3
    // package, which ships NpgsqlArrayMethodTranslator for translating LINQ over this shape).
    // Capped at 10/repo by the Crawler (GitHubDiscoveryClient.BuildDiscoveryQuery) to bound
    // GraphQL point cost, not enforced here.
    public List<string> Topics { get; set; } = [];

    // Set once, on first insert only (F-010 D1) - never updated on re-crawl, unlike
    // LastCrawledAtUtc which changes every run. This is what the dashboard's "Newest" sort orders
    // by, so a frequently re-crawled old repo can't look newer than a genuinely new discovery.
    public DateTimeOffset FirstDiscoveredAtUtc { get; set; }

    public ICollection<Score> Scores { get; set; } = new List<Score>();

    public ICollection<Summary> Summaries { get; set; } = new List<Summary>();

    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
}
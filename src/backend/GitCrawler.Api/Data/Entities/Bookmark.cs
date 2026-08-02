namespace GitCrawler.Api.Data.Entities;

// No user/auth model exists yet (v1 is single-operator, per PRD) - a bookmark is just a
// repository reference plus a timestamp, one per repository (enforced by a unique index, see
// GitCrawlerDbContext.OnModelCreating), not scoped to any account.
public class Bookmark
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }

    public Repository Repository { get; set; } = null!;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
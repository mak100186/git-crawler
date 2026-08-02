namespace GitCrawler.Api.Data.Entities;

public class Summary
{
    public int Id { get; set; }

    public int RepositoryId { get; set; }

    public Repository Repository { get; set; } = null!;

    public string Content { get; set; } = string.Empty;

    public DateTimeOffset GeneratedAtUtc { get; set; }
}
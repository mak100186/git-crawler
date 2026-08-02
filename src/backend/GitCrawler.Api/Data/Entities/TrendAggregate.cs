namespace GitCrawler.Api.Data.Entities;

// A rollup for one technology/framework/ecosystem category over a period (Goal 3) - deliberately
// not FK'd to individual Repository rows, since a single trend summarizes many repositories at
// once rather than referencing one.
public class TrendAggregate
{
    public int Id { get; set; }

    public string Category { get; set; } = string.Empty;

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public int RepositoryCount { get; set; }

    public double AverageScore { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
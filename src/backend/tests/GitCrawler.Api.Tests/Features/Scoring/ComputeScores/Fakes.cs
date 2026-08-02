namespace GitCrawler.Api.Tests.Features.Scoring.ComputeScores;

// Minimal fixed-clock TimeProvider - lets tests control "now" precisely (e.g. to compute an exact
// expected CommitsPerWeek, or to set up a repo's LastCrawledAtUtc relative to a Score's
// ComputedAtUtc) without pulling in a testing package for it. Same pattern as the Crawling feature's
// own fake (see Features/Crawling/DiscoverRepositories/Fakes.cs) - kept local rather than shared
// across feature test folders, mirroring the vertical-slice convention (ADR-015) applied to tests.
internal class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utcNow;
}
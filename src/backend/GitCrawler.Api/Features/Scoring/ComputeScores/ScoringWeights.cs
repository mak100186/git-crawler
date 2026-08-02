namespace GitCrawler.Api.Features.Scoring.ComputeScores;

// Pure computation, deliberately kept free of GitCrawlerDbContext/EF Core so it's testable with
// plain values (Architecture §3: Scoring Engine must be "deterministic and independently
// unit-testable"). ComputeScoresCommandHandler owns all data access; this class only turns already-
// read raw signals into a score.
//
// Weights (sum to 1.0, each independently identifiable per AC2/PRD Constraints - no numeric split
// is prescribed upstream, this is this feature's judgment call):
//   license            18% - binary signal (has one or not), so it gets a single fixed weight
//                            rather than a normalization curve.
//   commits per week   27% - the primary "is this actively maintained" signal, weighted highest.
//   contributor count  22.5% - community-health signal.
//   fork count         22.5% - community-health signal.
//   star count         10% - popularity/quality signal (Architecture §3's "existing
//                            popularity/quality signals"), added per a mid-flight operator
//                            direction after the original four-signal AC shipped. The original
//                            four weights are scaled down proportionally by 0.9 (20/30/25/25 ->
//                            18/27/22.5/22.5) to fit this in without changing their relative
//                            ratios to each other. 10% is deliberately below the smallest primary
//                            weight (license, 18%) - this platform's premise is surfacing
//                            quality/activity signals *instead of* raw popularity, so star count
//                            must be able to move a score without ever being able to dominate the
//                            four signals the PRD explicitly commits to.
//
// Commit/contributor/fork/star counts are long-tailed (a handful of repos have orders of magnitude
// more than the rest) - a raw linear scale would let those outliers dominate every other repo's
// score. Each is instead log-normalized into [0,1] against a fixed cap chosen so a "very healthy
// for a hidden gem" repo (not a mega-project) lands near 1.0, then clamped so anything beyond the
// cap still contributes at most 1.0 rather than continuing to climb unbounded.
public static class ScoringWeights
{
    public const double LicenseWeight = 0.18;
    public const double CommitsPerWeekWeight = 0.27;
    public const double ContributorCountWeight = 0.225;
    public const double ForkCountWeight = 0.225;
    public const double StarCountWeight = 0.10;

    // Caps for the log-normalization below - picked as "generous for a hidden gem" rather than
    // "matches GitHub's biggest projects", since this platform's whole purpose is surfacing
    // under-the-radar repos, not re-ranking ones that are already enormous.
    private const double CommitsPerWeekCap = 10.0;
    private const double ContributorCountCap = 25.0;
    private const double ForkCountCap = 200.0;

    // Deliberately much lower than the other caps: "hidden gems" by definition have modest star
    // counts, so a cap calibrated to mega-projects (thousands+) would leave star count nearly flat
    // (near-zero contribution) across this platform's entire target population. 50 lets star count
    // meaningfully differentiate within the 0-50-star range most hidden gems actually occupy,
    // while still saturating (clamped at 1.0) well before a repo has enough stars to no longer be
    // "hidden".
    private const double StarCountCap = 50.0;

    // Floors the elapsed-weeks denominator at 1 week so a repo created hours/days ago with a
    // handful of initial commits doesn't compute an absurd "commits per week" (e.g. 50 commits over
    // 2 days would otherwise read as ~175/week) - a genuine product-impact judgment call, not just
    // a divide-by-zero guard, per the Task Packet's explicit callout.
    private const double MinElapsedWeeks = 1.0;

    // CommitCount is the Crawler's raw GraphQL totalCount (F-005) - a total, not a rate. Deriving
    // the rate is this feature's job per Architecture's crawler/scoring split.
    public static double ComputeCommitsPerWeek(int? commitCount, DateTimeOffset createdAtUtc, DateTimeOffset nowUtc)
    {
        if (commitCount is null or <= 0)
        {
            return 0.0;
        }

        var elapsedWeeks = Math.Max((nowUtc - createdAtUtc).TotalDays / 7.0, MinElapsedWeeks);
        return commitCount.Value / elapsedWeeks;
    }

    public static double ComputeTotalScore(bool hasLicense, double commitsPerWeek, int contributorCount, int forkCount, int starCount)
    {
        var licenseComponent = hasLicense ? 1.0 : 0.0;
        var commitsComponent = NormalizeLog(commitsPerWeek, CommitsPerWeekCap);
        var contributorComponent = NormalizeLog(contributorCount, ContributorCountCap);
        var forkComponent = NormalizeLog(forkCount, ForkCountCap);
        var starComponent = NormalizeLog(starCount, StarCountCap);

        var weighted =
            (licenseComponent * LicenseWeight) +
            (commitsComponent * CommitsPerWeekWeight) +
            (contributorComponent * ContributorCountWeight) +
            (forkComponent * ForkCountWeight) +
            (starComponent * StarCountWeight);

        // Each component is in [0,1] and the weights sum to 1.0, so `weighted` is itself in [0,1] -
        // scaled to 0-100 purely for a more readable display range (FR-005 requires "a score", not
        // a specific scale).
        return weighted * 100.0;
    }

    // log1p-style normalization (Math.Log(x + 1)) compresses the long tail of count-based signals
    // instead of a hard linear scale letting a handful of extreme repos dominate. Divided by
    // Math.Log(cap + 1) so a value at the cap normalizes to ~1.0, then clamped so values beyond the
    // cap don't exceed the max contribution.
    private static double NormalizeLog(double rawValue, double cap)
    {
        if (rawValue <= 0)
        {
            return 0.0;
        }

        var normalized = Math.Log(rawValue + 1) / Math.Log(cap + 1);
        return Math.Min(normalized, 1.0);
    }
}
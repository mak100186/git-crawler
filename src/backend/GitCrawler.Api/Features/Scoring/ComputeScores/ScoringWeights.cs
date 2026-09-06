namespace GitCrawler.Api.Features.Scoring.ComputeScores;

// Pure computation, deliberately kept free of GitCrawlerDbContext/EF Core so it's testable with
// plain values (Architecture §3: Scoring Engine must be "deterministic and independently
// unit-testable"). ComputeScoresCommandHandler owns all data access; this class only turns already-
// read raw signals into a score.
//
// Weights (sum to 1.0, each independently identifiable per AC2/PRD Constraints - no numeric split
// is prescribed upstream, this is this feature's judgment call):
//   star count         50% - the dominant signal, but NOT as "more is better" (see the bucket
//                            curve below): it answers "is this repo in the popularity band where
//                            hidden gems actually live", which is a middle band, not a maximum.
//   contributor count  20% - community-health signal.
//   commits per week   15% - the "is this actively maintained" signal.
//   license present    10% - binary signal (has one or not), so it gets a single fixed weight
//                            rather than a normalization curve.
//   fork count          5% - community-health signal, the weakest of the five.
//
// Commit/contributor/fork counts are long-tailed (a handful of repos have orders of magnitude more
// than the rest) - a raw linear scale would let those outliers dominate every other repo's score.
// Each is instead log-normalized into [0,1] against a fixed cap chosen so a "very healthy for a
// hidden gem" repo (not a mega-project) lands near 1.0, then clamped so anything beyond the cap
// still contributes at most 1.0 rather than continuing to climb unbounded.
//
// Star count is the deliberate exception and does NOT use that curve: see StarBucketUpperBounds.
public static class ScoringWeights
{
    public const double LicenseWeight = 0.10;
    public const double CommitsPerWeekWeight = 0.15;
    public const double ContributorCountWeight = 0.20;
    public const double ForkCountWeight = 0.05;
    public const double StarCountWeight = 0.50;

    // Caps for the log-normalization below - picked as "generous for a hidden gem" rather than
    // "matches GitHub's biggest projects", since this platform's whole purpose is surfacing
    // under-the-radar repos, not re-ranking ones that are already enormous.
    private const double CommitsPerWeekCap = 10.0;
    private const double ContributorCountCap = 25.0;
    private const double ForkCountCap = 200.0;

    // Star count does not use the log curve above, because "more stars is better" is the wrong
    // shape for this product. A repo nobody has found yet is unproven; a repo with 100k stars is
    // not hidden. Both are equally uninteresting *to this platform*, and a monotonic curve cannot
    // express that - so star count is bucketed and scored on a bell curve instead, peaking in the
    // middle band where a genuine hidden gem sits: proven enough that someone vouched for it, not
    // so famous that surfacing it tells anyone anything new.
    //
    // 12 buckets, upper bound inclusive. Widths grow roughly geometrically because star counts do.
    private static readonly int[] StarBucketUpperBounds =
    [
        100,          // 1  0-100
        250,          // 2  101-250
        500,          // 3  251-500
        1_000,        // 4  501-1,000
        2_500,        // 5  1,001-2,500
        5_000,        // 6  2,501-5,000
        10_000,       // 7  5,001-10,000
        15_000,       // 8  10,001-15,000
        25_000,       // 9  15,001-25,000
        50_000,       // 10 25,001-50,000
        100_000,      // 11 50,001-100,000
        int.MaxValue, // 12 100,001+
    ];

    // Gaussian centred at 6.5 - the midpoint of buckets 1..12 - so buckets 6 and 7 share the peak
    // and the curve is exactly symmetric: bucket 1 and bucket 12 score identically, as do 2 and 11,
    // and so on. That symmetry is the requirement; sigma only sets how sharply the score falls away
    // from the middle. At 2.5 the edge buckets keep ~9% of the star component rather than being
    // zeroed outright - enough that a 50-star repo with strong activity can still out-score a
    // dormant one in the same bucket, while the middle bands stay decisively ahead.
    private const double StarBucketCentre = 6.5;
    private const double StarBucketSigma = 2.5;

    // Computed once rather than per repository: the curve depends only on the constants above.
    // Normalized so the peak buckets are exactly 1.0, keeping every component in [0,1] and the
    // total in [0,100] like the other four signals.
    private static readonly double[] StarBucketScores = BuildStarBucketScores();

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

    // contributorCount is null only for a repo GitHub refuses to enumerate contributors for ("too
    // large to list contributors" - see GitHubContributorListUnavailableException). That refusal
    // happens precisely because the repo has an enormous contributor base, so scoring it as 0 - what
    // this did before - inverted the signal and docked the full 20% from the repos that most
    // deserve it. Full marks instead: unknown-because-huge is the top of this bucket, not the
    // bottom.
    public static double ComputeTotalScore(bool hasLicense, double commitsPerWeek, int? contributorCount, int forkCount, int starCount)
    {
        var licenseComponent = hasLicense ? 1.0 : 0.0;
        var commitsComponent = NormalizeLog(commitsPerWeek, CommitsPerWeekCap);
        var contributorComponent = contributorCount is null ? 1.0 : NormalizeLog(contributorCount.Value, ContributorCountCap);
        var forkComponent = NormalizeLog(forkCount, ForkCountCap);
        var starComponent = NormalizeStarCount(starCount);

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

    // Which of the 12 bands a star count falls in, scored off the bell curve. Public so tests and
    // any future score-breakdown UI can explain a score without re-deriving the bucket boundaries.
    public static double NormalizeStarCount(int starCount)
    {
        for (var i = 0; i < StarBucketUpperBounds.Length; i++)
        {
            if (starCount <= StarBucketUpperBounds[i])
            {
                return StarBucketScores[i];
            }
        }

        return StarBucketScores[^1];
    }

    private static double[] BuildStarBucketScores()
    {
        var twoSigmaSquared = 2.0 * StarBucketSigma * StarBucketSigma;

        // Buckets 6 and 7 are both 0.5 from the centre, so this is the curve's maximum - dividing
        // by it puts the peak at exactly 1.0.
        var peak = Math.Exp(-0.25 / twoSigmaSquared);

        var scores = new double[StarBucketUpperBounds.Length];
        for (var i = 0; i < scores.Length; i++)
        {
            var distanceFromCentre = (i + 1) - StarBucketCentre;
            scores[i] = Math.Exp(-(distanceFromCentre * distanceFromCentre) / twoSigmaSquared) / peak;
        }

        return scores;
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
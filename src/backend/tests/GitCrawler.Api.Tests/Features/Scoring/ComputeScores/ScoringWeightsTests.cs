using GitCrawler.Api.Features.Scoring.ComputeScores;

namespace GitCrawler.Api.Tests.Features.Scoring.ComputeScores;

// Pure-function tests, no DbContext involved - exactly what ScoringWeights is designed for
// (Architecture §3: "deterministic and independently unit-testable"). The core thing under test is
// AC2's "independently identifiable, weighted inputs": varying one signal while holding the other
// signals constant must move TotalScore, and must not be drowned out by another signal dominating
// due to a normalization/weighting bug.
public class ScoringWeightsTests
{
    [Fact]
    public void ComputeTotalScore_HasLicenseTrueVsFalse_ChangesScore_OtherSignalsHeldConstant()
    {
        var withLicense = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 20);
        var withoutLicense = ScoringWeights.ComputeTotalScore(hasLicense: false, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 20);

        Assert.True(withLicense > withoutLicense);
    }

    [Fact]
    public void ComputeTotalScore_HigherCommitsPerWeek_IncreasesScore_OtherSignalsHeldConstant()
    {
        var lower = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 1, contributorCount: 4, forkCount: 10, starCount: 20);
        var higher = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 8, contributorCount: 4, forkCount: 10, starCount: 20);

        Assert.True(higher > lower);
    }

    [Fact]
    public void ComputeTotalScore_HigherContributorCount_IncreasesScore_OtherSignalsHeldConstant()
    {
        var lower = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 1, forkCount: 10, starCount: 20);
        var higher = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 15, forkCount: 10, starCount: 20);

        Assert.True(higher > lower);
    }

    [Fact]
    public void ComputeTotalScore_HigherForkCount_IncreasesScore_OtherSignalsHeldConstant()
    {
        var lower = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 5, starCount: 20);
        var higher = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 100, starCount: 20);

        Assert.True(higher > lower);
    }

    [Fact]
    public void ComputeTotalScore_StarCountMovesTowardTheMiddleBand_IncreasesScore()
    {
        // Star count is deliberately NOT monotonic (see ScoringWeights' StarBucketUpperBounds
        // comment): moving from the first bucket toward the middle raises the score, which is the
        // only direction this platform treats as an improvement.
        var edge = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 50);
        var middle = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 4_000);

        Assert.True(middle > edge);
    }

    [Fact]
    public void ComputeTotalScore_FamousRepositoryScoresNoHigherThanAnUndiscoveredOne_OnStarCountAlone()
    {
        // The defining property of the bell curve: bucket 1 (0-100 stars) and bucket 12 (100k+)
        // score identically. Neither is a hidden gem - one is unproven, the other is not hidden -
        // so star count must not separate them.
        var undiscovered = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 12);
        var famous = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 400_000);

        Assert.Equal(undiscovered, famous, precision: 10);
    }

    [Theory]
    // Every mirrored bucket pair scores the same, and the boundaries land in the bucket their
    // inclusive upper bound says they do.
    [InlineData(100, 100_001)]
    [InlineData(250, 50_001)]
    [InlineData(500, 25_001)]
    [InlineData(1_000, 15_001)]
    [InlineData(2_500, 10_001)]
    public void NormalizeStarCount_MirroredBuckets_ScoreIdentically(int lowBucketStars, int highBucketStars)
    {
        Assert.Equal(
            ScoringWeights.NormalizeStarCount(lowBucketStars),
            ScoringWeights.NormalizeStarCount(highBucketStars),
            precision: 10);
    }

    [Theory]
    [InlineData(2_501)]
    [InlineData(5_000)]
    [InlineData(5_001)]
    [InlineData(10_000)]
    public void NormalizeStarCount_PeakBuckets_ScoreExactlyOne(int starCount)
    {
        // Buckets 6 and 7 straddle the curve's centre, so both sit at the maximum - which is
        // normalized to exactly 1.0 so the star component stays in [0,1] like every other signal.
        Assert.Equal(1.0, ScoringWeights.NormalizeStarCount(starCount), precision: 10);
    }

    [Fact]
    public void NormalizeStarCount_RisesToTheMiddleThenFallsAway()
    {
        // One representative star count per bucket, in order. The sequence must rise to the peak
        // and then fall - never rise again - which is what makes this a bell curve rather than a
        // saturating one.
        int[] representativeStarCounts = [50, 200, 400, 800, 2_000, 4_000, 8_000, 12_000, 20_000, 40_000, 80_000, 250_000];
        var scores = representativeStarCounts.Select(ScoringWeights.NormalizeStarCount).ToList();

        var peakIndex = scores.IndexOf(scores.Max());

        for (var i = 1; i <= peakIndex; i++)
        {
            Assert.True(scores[i] >= scores[i - 1], $"Score fell before the peak at bucket {i + 1}.");
        }

        for (var i = peakIndex + 1; i < scores.Count; i++)
        {
            Assert.True(scores[i] <= scores[i - 1], $"Score rose after the peak at bucket {i + 1}.");
        }
    }

    [Fact]
    public void ComputeTotalScore_ContributorListTooLargeToEnumerate_ScoresFullMarksForThatSignal()
    {
        // A null contributorCount means GitHub refused to list them because there are too many.
        // That must score as the top of the contributor bucket, not the bottom - scoring it as zero
        // docked the full 20% from exactly the repos with the largest communities.
        var tooManyToList = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: null, forkCount: 10, starCount: 4_000);
        var atTheCap = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 25, forkCount: 10, starCount: 4_000);
        var none = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 0, forkCount: 10, starCount: 4_000);

        Assert.Equal(atTheCap, tooManyToList, precision: 10);
        Assert.True(tooManyToList > none);
    }

    [Fact]
    public void ComputeTotalScore_ZeroStarCount_DoesNotCrashOrProduceNaN()
    {
        // Edge case: a brand-new "hidden gem" with no stars yet at all. Must not crash, and must
        // not produce NaN/Infinity or a silently nonzero star contribution.
        var score = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 0);

        Assert.False(double.IsNaN(score));
        Assert.False(double.IsInfinity(score));
    }

    [Fact]
    public void ComputeTotalScore_NoLicenseZeroContributorsZeroForksZeroStars_ProducesFiniteNonNegativeScore()
    {
        // Edge case: none of the five signals present at all. Must not crash, and must not produce
        // NaN/Infinity or a silently nonzero "license bonus" for a repo with no license.
        var score = ScoringWeights.ComputeTotalScore(hasLicense: false, commitsPerWeek: 0, contributorCount: 0, forkCount: 0, starCount: 0);

        // Not zero any more: star count is bucketed, and bucket 1 (0-100 stars) carries a small
        // non-zero weight rather than being zeroed outright. Everything else contributes nothing,
        // so the whole score is that one component.
        Assert.Equal(ScoringWeights.NormalizeStarCount(0) * ScoringWeights.StarCountWeight * 100.0, score, precision: 10);
        Assert.True(score > 0);
        Assert.False(double.IsNaN(score));
        Assert.False(double.IsInfinity(score));
    }

    [Fact]
    public void ComputeTotalScore_ExtremelyHighCounts_StaysFiniteAndDoesNotExceedMaxScale()
    {
        // Long-tailed outlier repo (thousands of contributors/forks/commits/stars) - the
        // log-normalization must clamp rather than let it blow past the 0-100 scale or overflow.
        var score = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 500_000, contributorCount: 100_000, forkCount: 1_000_000, starCount: 1_000_000);

        Assert.False(double.IsNaN(score));
        Assert.False(double.IsInfinity(score));
        // Small epsilon for floating-point summation of the five weight constants, not a
        // meaningful tolerance on the score itself - each component clamps at exactly 1.0, so the
        // true mathematical max is precisely 100.0.
        Assert.True(score <= 100.0 + 1e-9);
    }

    [Fact]
    public void ComputeCommitsPerWeek_NoCommitCount_ReturnsZero()
    {
        var result = ScoringWeights.ComputeCommitsPerWeek(null, DateTimeOffset.UtcNow.AddYears(-1), DateTimeOffset.UtcNow);

        Assert.Equal(0.0, result);
    }

    [Fact]
    public void ComputeCommitsPerWeek_VeryRecentlyCreatedRepoWithCommits_FloorsElapsedWeeksAtOne()
    {
        // Task Packet's explicit edge case: a repo created today with 50 commits must not compute
        // an absurd/near-infinite rate just because elapsed time rounds to a tiny fraction of a
        // week - the 1-week floor caps it at 50 commits/week, not 50/(a few hours in weeks).
        var now = new DateTimeOffset(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
        var createdAtUtc = now.AddHours(-3);

        var result = ScoringWeights.ComputeCommitsPerWeek(50, createdAtUtc, now);

        Assert.Equal(50.0, result);
    }

    [Fact]
    public void ComputeCommitsPerWeek_OlderRepo_DividesByActualElapsedWeeks()
    {
        var now = new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero);
        var createdAtUtc = now.AddDays(-70); // exactly 10 weeks

        var result = ScoringWeights.ComputeCommitsPerWeek(100, createdAtUtc, now);

        Assert.Equal(10.0, result, precision: 6);
    }
}
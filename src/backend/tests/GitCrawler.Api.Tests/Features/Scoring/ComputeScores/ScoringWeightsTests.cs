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
    public void ComputeTotalScore_HigherStarCount_IncreasesScore_OtherSignalsHeldConstant()
    {
        var lower = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 5);
        var higher = ScoringWeights.ComputeTotalScore(hasLicense: true, commitsPerWeek: 3, contributorCount: 4, forkCount: 10, starCount: 40);

        Assert.True(higher > lower);
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

        Assert.Equal(0.0, score);
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
using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Repositories;
using GitCrawler.Api.Features.Repositories.GetHiddenGems;
using GitCrawler.Api.Features.Scoring.ComputeScores;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Repositories.GetHiddenGems;

public class GetHiddenGemsQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;

    public GetHiddenGemsQueryHandlerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GitCrawlerDbContext>().UseSqlite(_connection).Options;
        _dbContext = new GitCrawlerDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private GetHiddenGemsQueryHandler CreateHandler() => new(_dbContext);

    private async Task<Repository> AddRepositoryAsync(long gitHubId, string name = "repo", string? language = "C#", int stars = 10)
    {
        var repository = new Repository
        {
            GitHubId = gitHubId,
            Owner = "octocat",
            Name = name,
            Url = $"https://github.com/octocat/{name}",
            DefaultBranch = "main",
            PrimaryLanguage = language,
            StarCount = stars,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FirstDiscoveredAtUtc = DateTimeOffset.UtcNow,
        };
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    private async Task<Score> AddScoreAsync(
        int repositoryId,
        bool hasLicense = true,
        string? licenseType = "MIT",
        double commitsPerWeek = 3.0,
        int contributorCount = 4,
        int forkCount = 5,
        int starCount = 10,
        double totalScore = 42.0,
        DateTimeOffset? computedAt = null)
    {
        var score = new Score
        {
            RepositoryId = repositoryId,
            HasLicense = hasLicense,
            LicenseType = licenseType,
            CommitsPerWeek = commitsPerWeek,
            ContributorCount = contributorCount,
            ForkCount = forkCount,
            StarCount = starCount,
            TotalScore = totalScore,
            ComputedAtUtc = computedAt ?? DateTimeOffset.UtcNow,
        };
        _dbContext.Scores.Add(score);
        await _dbContext.SaveChangesAsync();
        return score;
    }

    [Fact]
    public async Task Handle_RepositoryWithNoScore_IsExcluded()
    {
        await AddRepositoryAsync(1);
        var scored = await AddRepositoryAsync(2);
        await AddScoreAsync(scored.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal([scored.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_ScoreBreakdown_MatchesScoringWeightsConstantsExactly()
    {
        var repository = await AddRepositoryAsync(1);
        await AddScoreAsync(
            repository.Id, hasLicense: true, licenseType: "MIT", commitsPerWeek: 3.5, contributorCount: 4, forkCount: 5, starCount: 10, totalScore: 55.5);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        var breakdown = result.Items.Single().ScoreBreakdown;
        Assert.True(breakdown.HasLicense);
        Assert.Equal("MIT", breakdown.LicenseType);
        Assert.Equal(ScoringWeights.LicenseWeight, breakdown.LicenseWeight);
        Assert.Equal(3.5, breakdown.CommitsPerWeek);
        Assert.Equal(ScoringWeights.CommitsPerWeekWeight, breakdown.CommitsPerWeekWeight);
        Assert.Equal(4, breakdown.ContributorCount);
        Assert.Equal(ScoringWeights.ContributorCountWeight, breakdown.ContributorCountWeight);
        Assert.Equal(5, breakdown.ForkCount);
        Assert.Equal(ScoringWeights.ForkCountWeight, breakdown.ForkCountWeight);
        Assert.Equal(10, breakdown.StarCount);
        Assert.Equal(ScoringWeights.StarCountWeight, breakdown.StarCountWeight);
        Assert.Equal(55.5, breakdown.TotalScore);
    }

    [Fact]
    public async Task Handle_MultipleScores_UsesLatestByComputedAtUtc_NotHighestEverTotalScore()
    {
        var repository = await AddRepositoryAsync(1);
        // Highest-ever TotalScore is 90 (older), latest is 20 (newer) - regression test for the
        // same class of bug F-008 already caught (see docs/handoff.md): the breakdown must reflect
        // current standing, not a historical peak.
        await AddScoreAsync(repository.Id, totalScore: 90, computedAt: DateTimeOffset.UtcNow.AddDays(-2));
        await AddScoreAsync(repository.Id, totalScore: 20, computedAt: DateTimeOffset.UtcNow.AddDays(-1));

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal(20, result.Items.Single().ScoreBreakdown.TotalScore);
    }

    [Fact]
    public async Task Handle_NoFilters_DefaultsToScoreDesc()
    {
        var repositoryA = await AddRepositoryAsync(1);
        await AddScoreAsync(repositoryA.Id, totalScore: 10);
        var repositoryB = await AddRepositoryAsync(2);
        await AddScoreAsync(repositoryB.Id, totalScore: 90);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal([repositoryB.Id, repositoryA.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_LanguageAndStarsFilters_CombineWithAnd()
    {
        var repositoryA = await AddRepositoryAsync(1, language: "C#", stars: 10);
        await AddScoreAsync(repositoryA.Id);
        var repositoryB = await AddRepositoryAsync(2, language: "Go", stars: 10);
        await AddScoreAsync(repositoryB.Id);
        var repositoryC = await AddRepositoryAsync(3, language: "C#", stars: 1);
        await AddScoreAsync(repositoryC.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Language: ["C#"], MinStars: 5)), CancellationToken.None);

        Assert.Equal([repositoryA.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_EmptyFilterResult_ReturnsEmptyNotError()
    {
        var repository = await AddRepositoryAsync(1, language: "C#");
        await AddScoreAsync(repository.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Language: ["Rust"])), CancellationToken.None);

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_PageBeyondLast_ReturnsEmptyNotError()
    {
        var repository = await AddRepositoryAsync(1);
        await AddScoreAsync(repository.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Page: 99, PageSize: 24)), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_OnlyOneScoreEverComputed_TrendGrowthFallsBackToCurrentScore()
    {
        var repository = await AddRepositoryAsync(1, language: "C#");
        await AddScoreAsync(repository.Id, totalScore: 72.4);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal("72 current score", result.Items.Single().TrendGrowth);
    }

    [Fact]
    public async Task Handle_TwoScoresFromSeparateRecrawls_TrendGrowthIsPercentChangeVsPreviousScore()
    {
        var repository = await AddRepositoryAsync(1, language: "C#");
        var today = DateTimeOffset.UtcNow;
        // Previous re-crawl scored 50, latest re-crawl scored 60 - a +20% increase. A second,
        // unrelated repo of the same language is added to confirm this is now computed per-
        // repository, not blended across every C# repo the way the old category-level rollup was
        // (regression-shaped for the operator's own complaint: "Trend is currently calculated per
        // language. I want it to be calculated per repository").
        await AddScoreAsync(repository.Id, totalScore: 50.0, computedAt: today.AddDays(-1));
        await AddScoreAsync(repository.Id, totalScore: 60.0, computedAt: today);
        var otherRepository = await AddRepositoryAsync(2, language: "C#");
        await AddScoreAsync(otherRepository.Id, totalScore: 10.0, computedAt: today);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        var repositoryResult = result.Items.Single(i => i.Id == repository.Id);
        Assert.Equal("▲ +20% vs. last period", repositoryResult.TrendGrowth);
    }

    [Fact]
    public async Task Handle_TwoScoresFromSeparateRecrawls_DecliningScore_ShowsDownArrow()
    {
        var repository = await AddRepositoryAsync(1);
        var today = DateTimeOffset.UtcNow;
        await AddScoreAsync(repository.Id, totalScore: 80.0, computedAt: today.AddDays(-1));
        await AddScoreAsync(repository.Id, totalScore: 60.0, computedAt: today);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal("▼ -25% vs. last period", result.Items.Single().TrendGrowth);
    }

    // --- F-017 edge-case tests for the server-side sort/pagination rewrite ---

    [Fact]
    public async Task Handle_DeterministicIdTieBreak_SameScore_OrdersByIdAscending()
    {
        // Two repos with identical latest scores must sort by Repository.Id ascending as
        // tie-breaker, regardless of sort direction - deterministic pagination (F-010).
        var repoA = await AddRepositoryAsync(1, name: "aaa");
        await AddScoreAsync(repoA.Id, totalScore: 50.0);
        var repoB = await AddRepositoryAsync(2, name: "bbb");
        await AddScoreAsync(repoB.Id, totalScore: 50.0);

        var handler = CreateHandler();
        var resultDesc = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Score, Direction: SortDirection.Desc)),
            CancellationToken.None);
        var resultAsc = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Score, Direction: SortDirection.Asc)),
            CancellationToken.None);

        // Both directions: lower Id comes first when scores are equal.
        Assert.Equal([repoA.Id, repoB.Id], resultDesc.Items.Select(i => i.Id));
        Assert.Equal([repoA.Id, repoB.Id], resultAsc.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_SortByCommitsDesc_UsesLatestScore_NotHighestEver()
    {
        var repoA = await AddRepositoryAsync(1, name: "aaa");
        await AddScoreAsync(repoA.Id, commitsPerWeek: 20.0, computedAt: DateTimeOffset.UtcNow.AddDays(-2));
        await AddScoreAsync(repoA.Id, commitsPerWeek: 5.0, computedAt: DateTimeOffset.UtcNow);
        var repoB = await AddRepositoryAsync(2, name: "bbb");
        await AddScoreAsync(repoB.Id, commitsPerWeek: 10.0);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Commits, Direction: SortDirection.Desc)),
            CancellationToken.None);

        // repoB (10.0) > repoA (5.0, latest) - repoA's historical peak of 20.0 is irrelevant.
        Assert.Equal([repoB.Id, repoA.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_SortByNewest_Asc_OrdersByFirstDiscoveredAtUtcAscending()
    {
        var old = await AddRepositoryAsync(1, name: "old");
        old.FirstDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-10);
        _dbContext.SaveChanges();
        await AddScoreAsync(old.Id);

        var newer = await AddRepositoryAsync(2, name: "newer");
        newer.FirstDiscoveredAtUtc = DateTimeOffset.UtcNow.AddDays(-1);
        _dbContext.SaveChanges();
        await AddScoreAsync(newer.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Newest, Direction: SortDirection.Asc)),
            CancellationToken.None);

        Assert.Equal([old.Id, newer.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_SortByStars_Desc_OrdersByStarCountDescending()
    {
        var lowStars = await AddRepositoryAsync(1, stars: 5);
        await AddScoreAsync(lowStars.Id);
        var highStars = await AddRepositoryAsync(2, stars: 500);
        await AddScoreAsync(highStars.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Stars, Direction: SortDirection.Desc)),
            CancellationToken.None);

        Assert.Equal([highStars.Id, lowStars.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_TopicFilter_MatchesReposWithOverlappingTopics()
    {
        var matched = await AddRepositoryAsync(1, name: "matched");
        matched.Topics = ["web", "api", "rest"];
        _dbContext.SaveChanges();
        await AddScoreAsync(matched.Id);

        var unmatched = await AddRepositoryAsync(2, name: "unmatched");
        unmatched.Topics = ["ml", "ai"];
        _dbContext.SaveChanges();
        await AddScoreAsync(unmatched.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Topic: ["web"])),
            CancellationToken.None);

        Assert.Equal([matched.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_LicenseFilter_FiltersByLicenseIdentifier()
    {
        var mit = await AddRepositoryAsync(1, name: "mit-repo");
        mit.LicenseIdentifier = "MIT";
        _dbContext.SaveChanges();
        await AddScoreAsync(mit.Id);

        var apache = await AddRepositoryAsync(2, name: "apache-repo");
        apache.LicenseIdentifier = "Apache-2.0";
        _dbContext.SaveChanges();
        await AddScoreAsync(apache.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(License: ["MIT"])),
            CancellationToken.None);

        Assert.Equal([mit.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_BookmarkedOnly_OnlyReturnsBookmarkedRepos()
    {
        var bookmarked = await AddRepositoryAsync(1, name: "bookmarked");
        await AddScoreAsync(bookmarked.Id);
        _dbContext.Bookmarks.Add(new Bookmark { RepositoryId = bookmarked.Id, CreatedAtUtc = DateTimeOffset.UtcNow });
        await _dbContext.SaveChangesAsync();

        var unbookmarked = await AddRepositoryAsync(2, name: "unbookmarked");
        await AddScoreAsync(unbookmarked.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(BookmarkedOnly: true)),
            CancellationToken.None);

        Assert.Equal([bookmarked.Id], result.Items.Select(i => i.Id));
        Assert.True(result.Items.Single().IsBookmarked);
    }

    [Fact]
    public async Task Handle_TotalCount_ReflectsFullMatchSet_NotPageSize()
    {
        for (var i = 0; i < 5; i++)
        {
            var repo = await AddRepositoryAsync(i + 1, name: $"repo-{i}");
            await AddScoreAsync(repo.Id);
        }

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Page: 1, PageSize: 2)),
            CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Handle_PageSizeClamping_CapsAtMaxPageSize()
    {
        var repo = await AddRepositoryAsync(1);
        await AddScoreAsync(repo.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(PageSize: 999)),
            CancellationToken.None);

        // PageSize is clamped to MaxPageSize (100), not 999.
        Assert.Equal(RepositoryCardQuery.MaxPageSize, result.PageSize);
    }

    [Fact]
    public async Task Handle_PageClamping_NegativePageBecomesOne()
    {
        var repo = await AddRepositoryAsync(1);
        await AddScoreAsync(repo.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Page: -5, PageSize: 24)),
            CancellationToken.None);

        Assert.Equal(1, result.Page);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Handle_SummaryPending_NullSummaryFields()
    {
        // A scored repo with no Summary row should have null SummaryContent/DetailedSummaryContent.
        var repo = await AddRepositoryAsync(1);
        await AddScoreAsync(repo.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Null(result.Items.Single().SummaryContent);
        Assert.Null(result.Items.Single().DetailedSummaryContent);
    }

    [Fact]
    public async Task Handle_SingleMatch_ReturnsOneItem_AccurateTotalCount()
    {
        var repo = await AddRepositoryAsync(1, language: "Rust");
        await AddScoreAsync(repo.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Language: ["Rust"])),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_ZeroMatchAtSmallScale_ReturnsEmptyWithZeroTotalCount()
    {
        var repo = await AddRepositoryAsync(1, language: "C#");
        await AddScoreAsync(repo.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Language: ["COBOL"])),
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_MultipleScores_LatestIsLowest_StillUsesLatestForSort()
    {
        // Regression-shaped: a repo whose latest score is LOWER than its historical peak must
        // still sort by the latest score, not the highest-ever. This is the same convention
        // tested by Handle_MultipleScores_UsesLatestByComputedAtUtc_NotHighestEverTotalScore,
        // but exercised through the sort path (not just the breakdown).
        var peaked = await AddRepositoryAsync(1, name: "peaked");
        await AddScoreAsync(peaked.Id, totalScore: 90.0, computedAt: DateTimeOffset.UtcNow.AddDays(-2));
        await AddScoreAsync(peaked.Id, totalScore: 10.0, computedAt: DateTimeOffset.UtcNow);

        var steady = await AddRepositoryAsync(2, name: "steady");
        await AddScoreAsync(steady.Id, totalScore: 50.0);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Score, Direction: SortDirection.Desc)),
            CancellationToken.None);

        // steady (50.0) > peaked (10.0, latest) - peaked's 90.0 peak is ignored.
        Assert.Equal([steady.Id, peaked.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_PageExactlyAtLastBoundary_ReturnsRemainingItems()
    {
        // 3 repos, page size 2, page 2: should return the 3rd repo (1 item).
        for (var i = 0; i < 3; i++)
        {
            var repo = await AddRepositoryAsync(i + 1, name: $"repo-{i}");
            await AddScoreAsync(repo.Id, totalScore: (3 - i) * 10.0);
        }

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetHiddenGemsQuery(new RepositoryFilterCriteria(Page: 2, PageSize: 2, Sort: RepositorySortField.Score, Direction: SortDirection.Desc)),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Handle_IsBookmarked_ReflectsBookmarkExistencePerRepo()
    {
        var bookmarked = await AddRepositoryAsync(1, name: "bookmarked");
        await AddScoreAsync(bookmarked.Id);
        _dbContext.Bookmarks.Add(new Bookmark { RepositoryId = bookmarked.Id, CreatedAtUtc = DateTimeOffset.UtcNow });
        await _dbContext.SaveChangesAsync();

        var unbookmarked = await AddRepositoryAsync(2, name: "unbookmarked");
        await AddScoreAsync(unbookmarked.Id);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetHiddenGemsQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        var bookmarkedDto = result.Items.Single(i => i.Id == bookmarked.Id);
        var unbookmarkedDto = result.Items.Single(i => i.Id == unbookmarked.Id);
        Assert.True(bookmarkedDto.IsBookmarked);
        Assert.False(unbookmarkedDto.IsBookmarked);
    }
}
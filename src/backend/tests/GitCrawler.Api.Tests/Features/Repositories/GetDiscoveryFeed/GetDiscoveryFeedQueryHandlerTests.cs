using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Repositories;
using GitCrawler.Api.Features.Repositories.GetDiscoveryFeed;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Repositories.GetDiscoveryFeed;

// Same SQLite-backed DbContext approach as GitCrawlerDbContextTests/
// DiscoverRepositoriesCommandHandlerTests (not the EF Core InMemory provider), so filter
// translations (including the Topics array-overlap shape) exercise real query translation, not a
// weaker in-memory approximation.
public class GetDiscoveryFeedQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;

    public GetDiscoveryFeedQueryHandlerTests()
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

    private GetDiscoveryFeedQueryHandler CreateHandler() => new(_dbContext);

    private async Task<Repository> AddRepositoryAsync(
        long gitHubId,
        string name = "repo",
        string? language = "C#",
        int stars = 10,
        string? license = "MIT",
        List<string>? topics = null,
        DateTimeOffset? firstDiscovered = null)
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
            LicenseIdentifier = license,
            LicenseName = license,
            Topics = topics ?? [],
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FirstDiscoveredAtUtc = firstDiscovered ?? DateTimeOffset.UtcNow,
        };
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    private async Task AddScoreAsync(int repositoryId, double totalScore, double commitsPerWeek = 1.0, DateTimeOffset? computedAt = null)
    {
        _dbContext.Scores.Add(new Score
        {
            RepositoryId = repositoryId,
            TotalScore = totalScore,
            CommitsPerWeek = commitsPerWeek,
            ComputedAtUtc = computedAt ?? DateTimeOffset.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task AddSummaryAsync(int repositoryId, string content)
    {
        _dbContext.Summaries.Add(new Summary { RepositoryId = repositoryId, Content = content, GeneratedAtUtc = DateTimeOffset.UtcNow });
        await _dbContext.SaveChangesAsync();
    }

    private async Task AddBookmarkAsync(int repositoryId)
    {
        _dbContext.Bookmarks.Add(new Bookmark { RepositoryId = repositoryId, CreatedAtUtc = DateTimeOffset.UtcNow });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_NoFilters_DefaultsToNewestDesc()
    {
        var older = await AddRepositoryAsync(1, name: "older", firstDiscovered: DateTimeOffset.UtcNow.AddDays(-2));
        var newer = await AddRepositoryAsync(2, name: "newer", firstDiscovered: DateTimeOffset.UtcNow.AddDays(-1));

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetDiscoveryFeedQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.Equal([newer.Id, older.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_RepositoryWithNoSummary_ReturnsNullNotEmptyString()
    {
        await AddRepositoryAsync(1);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetDiscoveryFeedQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Null(result.Items.Single().SummaryContent);
    }

    [Fact]
    public async Task Handle_RepositoryWithSummary_ReturnsLatestSummaryContent()
    {
        var repository = await AddRepositoryAsync(1);
        await AddSummaryAsync(repository.Id, "A concise summary.");

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetDiscoveryFeedQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Equal("A concise summary.", result.Items.Single().SummaryContent);
    }

    [Fact]
    public async Task Handle_RepositoryWithNoTopics_ReturnsEmptyTopicsList()
    {
        await AddRepositoryAsync(1);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetDiscoveryFeedQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.Empty(result.Items.Single().Topics);
    }

    [Fact]
    public async Task Handle_LanguageFilter_ReturnsOnlyMatchingLanguage()
    {
        var csharp = await AddRepositoryAsync(1, language: "C#");
        await AddRepositoryAsync(2, language: "Go");

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Language: ["C#"])), CancellationToken.None);

        Assert.Equal([csharp.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_StarRangeFilter_ReturnsOnlyReposWithinInclusiveRange()
    {
        await AddRepositoryAsync(1, stars: 5);
        var inRange = await AddRepositoryAsync(2, stars: 10);
        await AddRepositoryAsync(3, stars: 20);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(MinStars: 10, MaxStars: 15)), CancellationToken.None);

        Assert.Equal([inRange.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_TopicFilter_ReturnsReposWithOverlappingTopic()
    {
        var matching = await AddRepositoryAsync(1, topics: ["cli", "dotnet"]);
        await AddRepositoryAsync(2, topics: ["web"]);
        await AddRepositoryAsync(3, topics: []);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Topic: ["cli", "rust"])), CancellationToken.None);

        Assert.Equal([matching.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_LicenseFilter_ReturnsOnlyMatchingLicense()
    {
        var mit = await AddRepositoryAsync(1, license: "MIT");
        await AddRepositoryAsync(2, license: "Apache-2.0");
        await AddRepositoryAsync(3, license: null);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(License: ["MIT"])), CancellationToken.None);

        Assert.Equal([mit.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_AllFacetsTogether_CombinesWithAnd()
    {
        var matches = await AddRepositoryAsync(1, language: "C#", stars: 10, license: "MIT", topics: ["cli"]);
        await AddRepositoryAsync(2, language: "Go", stars: 10, license: "MIT", topics: ["cli"]); // wrong language
        await AddRepositoryAsync(3, language: "C#", stars: 1, license: "MIT", topics: ["cli"]); // wrong stars
        await AddRepositoryAsync(4, language: "C#", stars: 10, license: "Apache-2.0", topics: ["cli"]); // wrong license
        await AddRepositoryAsync(5, language: "C#", stars: 10, license: "MIT", topics: ["web"]); // wrong topic

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(
                Language: ["C#"], MinStars: 5, MaxStars: 20, Topic: ["cli"], License: ["MIT"])),
            CancellationToken.None);

        Assert.Equal([matches.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_BookmarkedOnly_ReturnsOnlyBookmarkedRepos()
    {
        var bookmarked = await AddRepositoryAsync(1);
        await AddBookmarkAsync(bookmarked.Id);
        await AddRepositoryAsync(2);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(BookmarkedOnly: true)), CancellationToken.None);

        Assert.Equal([bookmarked.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_IsBookmarked_ReflectsBookmarkStateCorrectly()
    {
        var bookmarked = await AddRepositoryAsync(1);
        await AddBookmarkAsync(bookmarked.Id);
        var notBookmarked = await AddRepositoryAsync(2);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetDiscoveryFeedQuery(new RepositoryFilterCriteria()), CancellationToken.None);

        Assert.True(result.Items.Single(i => i.Id == bookmarked.Id).IsBookmarked);
        Assert.False(result.Items.Single(i => i.Id == notBookmarked.Id).IsBookmarked);
    }

    [Fact]
    public async Task Handle_EmptyFilterResult_ReturnsEmptyNotError()
    {
        await AddRepositoryAsync(1, language: "C#");

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Language: ["Rust"])), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Theory]
    [InlineData(SortDirection.Desc)]
    [InlineData(SortDirection.Asc)]
    public async Task Handle_SortByStars_RespectsDirection(SortDirection direction)
    {
        var low = await AddRepositoryAsync(1, stars: 5);
        var high = await AddRepositoryAsync(2, stars: 50);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Stars, Direction: direction)),
            CancellationToken.None);

        var expected = direction == SortDirection.Desc ? new[] { high.Id, low.Id } : [low.Id, high.Id];
        Assert.Equal(expected, result.Items.Select(i => i.Id));
    }

    [Theory]
    [InlineData(SortDirection.Desc)]
    [InlineData(SortDirection.Asc)]
    public async Task Handle_SortByScore_UsesLatestScoreNotHighestEver_RespectsDirection(SortDirection direction)
    {
        var repoA = await AddRepositoryAsync(1);
        // Repo A's highest-ever score is 90, but its latest (most recent ComputedAtUtc) is 20 - a
        // regression test for the exact class of bug F-008 already caught (see docs/handoff.md):
        // sorting must reflect current standing, not a historical peak.
        await AddScoreAsync(repoA.Id, totalScore: 90, computedAt: DateTimeOffset.UtcNow.AddDays(-2));
        await AddScoreAsync(repoA.Id, totalScore: 20, computedAt: DateTimeOffset.UtcNow.AddDays(-1));

        var repoB = await AddRepositoryAsync(2);
        await AddScoreAsync(repoB.Id, totalScore: 50, computedAt: DateTimeOffset.UtcNow);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Score, Direction: direction)),
            CancellationToken.None);

        var expected = direction == SortDirection.Desc ? new[] { repoB.Id, repoA.Id } : [repoA.Id, repoB.Id];
        Assert.Equal(expected, result.Items.Select(i => i.Id));
    }

    [Theory]
    [InlineData(SortDirection.Desc)]
    [InlineData(SortDirection.Asc)]
    public async Task Handle_SortByCommits_UsesLatestScoreCommitsPerWeek_RespectsDirection(SortDirection direction)
    {
        var repoA = await AddRepositoryAsync(1);
        await AddScoreAsync(repoA.Id, totalScore: 1, commitsPerWeek: 2.0);
        var repoB = await AddRepositoryAsync(2);
        await AddScoreAsync(repoB.Id, totalScore: 1, commitsPerWeek: 8.0);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Commits, Direction: direction)),
            CancellationToken.None);

        var expected = direction == SortDirection.Desc ? new[] { repoB.Id, repoA.Id } : [repoA.Id, repoB.Id];
        Assert.Equal(expected, result.Items.Select(i => i.Id));
    }

    [Theory]
    [InlineData(SortDirection.Desc)]
    [InlineData(SortDirection.Asc)]
    public async Task Handle_SortByNewest_UsesFirstDiscoveredAtUtc_RespectsDirection(SortDirection direction)
    {
        var older = await AddRepositoryAsync(1, firstDiscovered: DateTimeOffset.UtcNow.AddDays(-5));
        var newer = await AddRepositoryAsync(2, firstDiscovered: DateTimeOffset.UtcNow);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Newest, Direction: direction)),
            CancellationToken.None);

        var expected = direction == SortDirection.Desc ? new[] { newer.Id, older.Id } : [older.Id, newer.Id];
        Assert.Equal(expected, result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_SortWithTiedValues_BreaksTiesByIdAscending()
    {
        var first = await AddRepositoryAsync(1, stars: 10);
        var second = await AddRepositoryAsync(2, stars: 10);
        var third = await AddRepositoryAsync(3, stars: 10);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Sort: RepositorySortField.Stars, Direction: SortDirection.Desc)),
            CancellationToken.None);

        Assert.Equal([first.Id, second.Id, third.Id], result.Items.Select(i => i.Id));
    }

    [Fact]
    public async Task Handle_FirstPage_ReturnsExpectedSlice()
    {
        for (var i = 1; i <= 5; i++)
        {
            await AddRepositoryAsync(i, name: $"repo{i}", firstDiscovered: DateTimeOffset.UtcNow.AddMinutes(i));
        }

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Page: 1, PageSize: 2)), CancellationToken.None);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task Handle_LastPage_ReturnsRemainder()
    {
        for (var i = 1; i <= 5; i++)
        {
            await AddRepositoryAsync(i, name: $"repo{i}");
        }

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Page: 3, PageSize: 2)), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Handle_PageBeyondLast_ReturnsEmptyNotError()
    {
        await AddRepositoryAsync(1);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(Page: 99, PageSize: 24)), CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_PageSizeAboveMax_IsClampedToMax()
    {
        await AddRepositoryAsync(1);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(
            new GetDiscoveryFeedQuery(new RepositoryFilterCriteria(PageSize: 10_000)), CancellationToken.None);

        Assert.Equal(RepositoryCardQuery.MaxPageSize, result.PageSize);
    }
}
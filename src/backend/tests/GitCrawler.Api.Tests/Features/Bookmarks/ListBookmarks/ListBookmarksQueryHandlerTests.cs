using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Bookmarks.ListBookmarks;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Bookmarks.ListBookmarks;

public class ListBookmarksQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;

    public ListBookmarksQueryHandlerTests()
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

    private ListBookmarksQueryHandler CreateHandler() => new(_dbContext);

    private async Task<Repository> AddRepositoryAsync(long gitHubId, string name = "repo")
    {
        var repository = new Repository
        {
            GitHubId = gitHubId,
            Owner = "octocat",
            Name = name,
            Url = $"https://github.com/octocat/{name}",
            DefaultBranch = "main",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FirstDiscoveredAtUtc = DateTimeOffset.UtcNow,
        };
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    private async Task AddBookmarkAsync(int repositoryId, DateTimeOffset createdAt)
    {
        _dbContext.Bookmarks.Add(new Bookmark { RepositoryId = repositoryId, CreatedAtUtc = createdAt });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_NoBookmarks_ReturnsEmptyList()
    {
        var handler = CreateHandler();
        var result = await handler.HandleAsync(new ListBookmarksQuery(), CancellationToken.None);

        Assert.Empty(result.Repositories);
    }

    [Fact]
    public async Task Handle_OnlyReturnsBookmarkedRepositories()
    {
        var bookmarked = await AddRepositoryAsync(1);
        await AddBookmarkAsync(bookmarked.Id, DateTimeOffset.UtcNow);
        await AddRepositoryAsync(2);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new ListBookmarksQuery(), CancellationToken.None);

        Assert.Equal([bookmarked.Id], result.Repositories.Select(r => r.Id));
        Assert.True(result.Repositories.Single().IsBookmarked);
    }

    [Fact]
    public async Task Handle_OrdersByMostRecentlyBookmarkedFirst()
    {
        var olderBookmark = await AddRepositoryAsync(1);
        await AddBookmarkAsync(olderBookmark.Id, DateTimeOffset.UtcNow.AddDays(-2));
        var newerBookmark = await AddRepositoryAsync(2);
        await AddBookmarkAsync(newerBookmark.Id, DateTimeOffset.UtcNow);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new ListBookmarksQuery(), CancellationToken.None);

        Assert.Equal([newerBookmark.Id, olderBookmark.Id], result.Repositories.Select(r => r.Id));
    }
}
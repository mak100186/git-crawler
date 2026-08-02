using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Bookmarks.CreateBookmark;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Bookmarks.CreateBookmark;

public class CreateBookmarkCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

    public CreateBookmarkCommandHandlerTests()
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

    private CreateBookmarkCommandHandler CreateHandler() => new(_dbContext, _timeProvider);

    private async Task<Repository> AddRepositoryAsync(long gitHubId)
    {
        var repository = new Repository
        {
            GitHubId = gitHubId,
            Owner = "octocat",
            Name = "hello-world",
            Url = "https://github.com/octocat/hello-world",
            DefaultBranch = "main",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            FirstDiscoveredAtUtc = DateTimeOffset.UtcNow,
        };
        _dbContext.Repositories.Add(repository);
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    [Fact]
    public async Task Handle_NewRepository_CreatesBookmark()
    {
        var repository = await AddRepositoryAsync(1);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new CreateBookmarkCommand(repository.Id), CancellationToken.None);

        Assert.Equal(repository.Id, result.RepositoryId);
        Assert.Equal(_timeProvider.GetUtcNow(), result.CreatedAtUtc);
        Assert.Equal(1, await _dbContext.Bookmarks.CountAsync());
    }

    [Fact]
    public async Task Handle_AlreadyBookmarkedRepository_IsIdempotent_DoesNotThrow_ReturnsExistingBookmark()
    {
        var repository = await AddRepositoryAsync(1);
        var handler = CreateHandler();
        var first = await handler.HandleAsync(new CreateBookmarkCommand(repository.Id), CancellationToken.None);

        // F-010 Constraints: bookmarking an already-bookmarked repo must not throw the unique-index
        // violation on Bookmark.RepositoryId - it must return the existing row instead.
        var second = await handler.HandleAsync(new CreateBookmarkCommand(repository.Id), CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _dbContext.Bookmarks.CountAsync());
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Bookmarks.DeleteBookmark;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Bookmarks.DeleteBookmark;

public class DeleteBookmarkCommandHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;

    public DeleteBookmarkCommandHandlerTests()
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

    private DeleteBookmarkCommandHandler CreateHandler() => new(_dbContext);

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
    public async Task Handle_ExistingBookmark_DeletesIt()
    {
        var repository = await AddRepositoryAsync(1);
        _dbContext.Bookmarks.Add(new Bookmark { RepositoryId = repository.Id, CreatedAtUtc = DateTimeOffset.UtcNow });
        await _dbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DeleteBookmarkCommand(repository.Id), CancellationToken.None);

        Assert.True(result.Deleted);
        Assert.Equal(0, await _dbContext.Bookmarks.CountAsync());
    }

    [Fact]
    public async Task Handle_NonExistentBookmark_IsIdempotent_DoesNotThrow()
    {
        var repository = await AddRepositoryAsync(1);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DeleteBookmarkCommand(repository.Id), CancellationToken.None);

        // F-010 Constraints: deleting a non-existent bookmark is a no-op success, not an error - see
        // DeleteBookmarkCommandHandler's own comment for the idempotency rationale.
        Assert.False(result.Deleted);
    }
}
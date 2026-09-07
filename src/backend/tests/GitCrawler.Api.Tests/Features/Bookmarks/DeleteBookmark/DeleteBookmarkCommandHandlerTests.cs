using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Bookmarks.DeleteBookmark;
using GitCrawler.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Bookmarks.DeleteBookmark;

public class DeleteBookmarkCommandHandlerTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private DeleteBookmarkCommandHandler CreateHandler() => new(DbContext);

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
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();
        return repository;
    }

    [Fact]
    public async Task Handle_ExistingBookmark_DeletesIt()
    {
        var repository = await AddRepositoryAsync(1);
        DbContext.Bookmarks.Add(new Bookmark { RepositoryId = repository.Id, CreatedAtUtc = DateTimeOffset.UtcNow });
        await DbContext.SaveChangesAsync();

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new DeleteBookmarkCommand(repository.Id), CancellationToken.None);

        Assert.True(result.Deleted);
        Assert.Equal(0, await DbContext.Bookmarks.CountAsync());
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
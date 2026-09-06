using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Bookmarks.CreateBookmark;
using GitCrawler.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Bookmarks.CreateBookmark;

public class CreateBookmarkCommandHandlerTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.Zero));

    private CreateBookmarkCommandHandler CreateHandler() => new(DbContext, _timeProvider);

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
    public async Task Handle_NewRepository_CreatesBookmark()
    {
        var repository = await AddRepositoryAsync(1);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new CreateBookmarkCommand(repository.Id), CancellationToken.None);

        Assert.NotNull(result.Bookmark);
        Assert.Equal(repository.Id, result.Bookmark.RepositoryId);
        Assert.Equal(_timeProvider.GetUtcNow(), result.Bookmark.CreatedAtUtc);
        Assert.Equal(1, await DbContext.Bookmarks.CountAsync());
    }

    [Fact]
    public async Task Handle_UnknownRepository_ReturnsNoBookmark_AndInsertsNothing()
    {
        // Bookmark.RepositoryId is a foreign key: without the existence check in the handler this
        // reaches the database and comes back as a DbUpdateException, which the endpoint can only
        // surface as a 500 for what is plainly a 404.
        var handler = CreateHandler();

        var result = await handler.HandleAsync(new CreateBookmarkCommand(999999), CancellationToken.None);

        Assert.Null(result.Bookmark);
        Assert.Equal(0, await DbContext.Bookmarks.CountAsync());
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

        Assert.NotNull(first.Bookmark);
        Assert.NotNull(second.Bookmark);
        Assert.Equal(first.Bookmark.Id, second.Bookmark.Id);
        Assert.Equal(1, await DbContext.Bookmarks.CountAsync());
    }

    private sealed class FakeTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
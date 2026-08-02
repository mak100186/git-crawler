using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Features.Categories.GetCategories;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Features.Categories.GetCategories;

public class GetCategoriesQueryHandlerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _dbContext;

    public GetCategoriesQueryHandlerTests()
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

    private GetCategoriesQueryHandler CreateHandler() => new(_dbContext);

    private async Task AddTrendAggregateAsync(string category, int repositoryCount, double averageScore, DateTimeOffset createdAt)
    {
        _dbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = category,
            PeriodStart = DateOnly.FromDateTime(createdAt.UtcDateTime),
            PeriodEnd = DateOnly.FromDateTime(createdAt.UtcDateTime),
            RepositoryCount = repositoryCount,
            AverageScore = averageScore,
            CreatedAtUtc = createdAt,
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Handle_NoTrendAggregates_ReturnsEmptyList()
    {
        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        Assert.Empty(result.Categories);
    }

    [Fact]
    public async Task Handle_ReturnsOneEntryPerCategory()
    {
        await AddTrendAggregateAsync("C#", 3, 50, DateTimeOffset.UtcNow);
        await AddTrendAggregateAsync("Go", 2, 40, DateTimeOffset.UtcNow);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        Assert.Equal(["C#", "Go"], result.Categories.Select(c => c.Category));
    }

    [Fact]
    public async Task Handle_MultiplePeriodsForSameCategory_ReturnsLatestByCreatedAtUtc()
    {
        await AddTrendAggregateAsync("C#", repositoryCount: 3, averageScore: 40, createdAt: DateTimeOffset.UtcNow.AddDays(-2));
        await AddTrendAggregateAsync("C#", repositoryCount: 5, averageScore: 60, createdAt: DateTimeOffset.UtcNow);

        var handler = CreateHandler();
        var result = await handler.HandleAsync(new GetCategoriesQuery(), CancellationToken.None);

        var category = Assert.Single(result.Categories);
        Assert.Equal(5, category.RepositoryCount);
        Assert.Equal(60, category.AverageScore);
    }
}
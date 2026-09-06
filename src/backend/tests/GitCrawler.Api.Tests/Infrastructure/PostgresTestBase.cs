using GitCrawler.Api.Data;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Tests.Infrastructure;

// Base class for every database-backed test class. Gives each test a fresh DbContext against the
// shared container (see PostgresFixture) and an empty schema.
//
// Reset is TRUNCATE rather than a per-test database or a transaction rollback: it is fast, it keeps
// the migrated schema (including the PostgreSQL-only indexes) rather than recreating it per test,
// and unlike a rollback it does not interfere with handlers that manage their own transactions or
// issue raw SQL - ComputeScoresCommandHandler's retention delete, for one.
[Collection(PostgresCollection.Name)]
public abstract class PostgresTestBase : IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    protected PostgresTestBase(PostgresFixture fixture)
    {
        _fixture = fixture;
        DbContext = fixture.CreateDbContext();
    }

    protected GitCrawlerDbContext DbContext { get; }

    protected GitCrawlerDbContext CreateSeparateDbContext() => _fixture.CreateDbContext();

    // Built from the EF model rather than hand-listed. A hand-written list silently stops resetting
    // any table added later, and the failure mode is not a missing table - it is one test class's
    // rows leaking into the next, which shows up as an unrelated assertion failing somewhere else.
    private static readonly string TruncateSql = BuildTruncateSql();

    private static string BuildTruncateSql()
    {
        using var model = new GitCrawlerDbContext(
            new DbContextOptionsBuilder<GitCrawlerDbContext>().UseNpgsql("Host=none").Options);

        var tables = model.Model.GetEntityTypes()
            .Select(e => e.GetTableName())
            .Where(name => name is not null)
            .Distinct()
            .Select(name => $"\"{name}\"");

        // RESTART IDENTITY so generated keys are predictable per test; CASCADE because the tables
        // are FK-linked and the truncation order would otherwise matter.
        return $"TRUNCATE TABLE {string.Join(", ", tables)} RESTART IDENTITY CASCADE";
    }

    public async Task InitializeAsync() => await DbContext.Database.ExecuteSqlRawAsync(TruncateSql);

    public Task DisposeAsync()
    {
        DbContext.Dispose();
        return Task.CompletedTask;
    }
}
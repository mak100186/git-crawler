using GitCrawler.Api.Data;

using Microsoft.EntityFrameworkCore;

using Testcontainers.PostgreSql;

namespace GitCrawler.Api.Tests.Infrastructure;

// One PostgreSQL container for the whole test assembly (review finding H-4).
//
// The suite used to run every handler test against in-memory SQLite. That was cheap, but it meant
// the tests and production were exercising different code: GetHiddenGemsQueryHandler branched on
// the runtime provider and SQLite took a client-side rank/paginate fallback, so the server-side
// sort/pagination path - the entire deliverable of F-017, and the one that actually runs - was
// never executed by a single test. The same gap covered the schema: EnsureCreated() skips the
// migration chain, so the GIN index on Topics and the INCLUDE columns on the Score composite index,
// both PostgreSQL-only, were never applied by a test either.
//
// This fixture runs the same major version as docker-compose.yml and applies the real migrations,
// so a migration that does not apply cleanly now fails the suite rather than production startup.
//
// Cost of the trade: `dotnet test` now requires a running Docker daemon. That is a deliberate
// exchange of convenience for the tests actually testing the thing that ships.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18.4")
        .WithDatabase("gitcrawler_tests")
        .WithUsername("gitcrawler")
        .WithPassword("gitcrawler")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // MigrateAsync, not EnsureCreated: applying the real migration chain to an empty database is
        // itself one of the things H-4 asked to be covered.
        await using var dbContext = CreateDbContext();
        await dbContext.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public GitCrawlerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<GitCrawlerDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new GitCrawlerDbContext(options);
    }
}

// Assembly-wide collection: every database-backed test class joins this one collection, so the
// container starts once and the classes run sequentially against it. Sequential execution is what
// makes the truncate-between-tests reset in PostgresTestBase safe.
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
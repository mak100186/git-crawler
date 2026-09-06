using System.Linq.Expressions;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;
using GitCrawler.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GitCrawler.Api.Tests.Data;

// Runs against the real PostgreSQL the application ships on (see PostgresFixture), not SQLite and
// not the EF Core InMemory provider. Unique indexes and foreign keys are enforced by the same
// engine production uses, and the schema comes from the real migration chain rather than
// EnsureCreated() - which is what makes the PostgreSQL-only parts of the model (the GIN index on
// Topics, the INCLUDE columns on the Score composite index) covered at all. Review finding H-4.
public class GitCrawlerDbContextTests(PostgresFixture fixture) : PostgresTestBase(fixture)
{

    [Fact]
    public void Context_ExposesDbSetForEveryEntity()
    {
        Assert.NotNull(DbContext.Repositories);
        Assert.NotNull(DbContext.Scores);
        Assert.NotNull(DbContext.Summaries);
        Assert.NotNull(DbContext.TrendAggregates);
        Assert.NotNull(DbContext.Bookmarks);
        Assert.NotNull(DbContext.DigestSendLogs);
    }

    [Fact]
    public void Model_DefinesEveryEntityType()
    {
        var model = DbContext.Model;

        Assert.NotNull(model.FindEntityType(typeof(Repository)));
        Assert.NotNull(model.FindEntityType(typeof(Score)));
        Assert.NotNull(model.FindEntityType(typeof(Summary)));
        Assert.NotNull(model.FindEntityType(typeof(TrendAggregate)));
        Assert.NotNull(model.FindEntityType(typeof(Bookmark)));
        Assert.NotNull(model.FindEntityType(typeof(DigestSendLog)));
    }

    [Fact]
    public async Task Score_PersistsAndLoads_ThroughRepositoryForeignKey()
    {
        var repository = NewRepository(gitHubId: 1);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        DbContext.Scores.Add(new Score
        {
            RepositoryId = repository.Id,
            HasLicense = true,
            LicenseType = "MIT",
            CommitsPerWeek = 3.5,
            ContributorCount = 2,
            ForkCount = 1,
            TotalScore = 42.0,
            ComputedAtUtc = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        var reloaded = await DbContext.Scores.Include(s => s.Repository).SingleAsync();

        Assert.Equal(repository.Id, reloaded.Repository.Id);
        Assert.True(reloaded.HasLicense);
        Assert.Equal("MIT", reloaded.LicenseType);
    }

    [Fact]
    public async Task Summary_PersistsAndLoads_ThroughRepositoryForeignKey()
    {
        var repository = NewRepository(gitHubId: 2);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        DbContext.Summaries.Add(new Summary
        {
            RepositoryId = repository.Id,
            ShortContent = "A concise summary.",
            DetailedContent = "A more detailed summary.",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        var reloaded = await DbContext.Summaries.Include(s => s.Repository).SingleAsync();

        Assert.Equal(repository.Id, reloaded.Repository.Id);
    }

    [Fact]
    public async Task Bookmark_PersistsAndLoads_ThroughRepositoryForeignKey()
    {
        var repository = NewRepository(gitHubId: 3);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        DbContext.Bookmarks.Add(new Bookmark
        {
            RepositoryId = repository.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        var reloaded = await DbContext.Bookmarks.Include(b => b.Repository).SingleAsync();

        Assert.Equal(repository.Id, reloaded.Repository.Id);
    }

    [Fact]
    public async Task Repository_DuplicateGitHubId_ViolatesUniqueConstraint()
    {
        // F-005's Crawler upserts by GitHubId (F-001 spike finding: re-crawls must not create
        // duplicate records) - this proves the schema itself enforces it, not just application
        // code.
        DbContext.Repositories.Add(NewRepository(gitHubId: 42));
        await DbContext.SaveChangesAsync();

        DbContext.Repositories.Add(NewRepository(gitHubId: 42, name: "hello-world-fork"));

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Bookmark_DuplicateRepositoryId_ViolatesUniqueConstraint()
    {
        var repository = NewRepository(gitHubId: 99);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        DbContext.Bookmarks.Add(new Bookmark { RepositoryId = repository.Id, CreatedAtUtc = DateTimeOffset.UtcNow });
        await DbContext.SaveChangesAsync();

        DbContext.Bookmarks.Add(new Bookmark { RepositoryId = repository.Id, CreatedAtUtc = DateTimeOffset.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Summary_DuplicateRepositoryId_ViolatesUniqueConstraint()
    {
        // F-016/NFR-003: Summary is create-once, never regenerated
        // (GenerateSummariesCommandHandler's own comment) - this proves the schema itself now
        // enforces at most one row per repository, not just application code / the
        // [DisableConcurrentExecution] guard on GenerateSummariesJob. Same shape as
        // Bookmark_DuplicateRepositoryId_ViolatesUniqueConstraint above.
        var repository = NewRepository(gitHubId: 100);
        DbContext.Repositories.Add(repository);
        await DbContext.SaveChangesAsync();

        DbContext.Summaries.Add(new Summary
        {
            RepositoryId = repository.Id,
            ShortContent = "first summary",
            DetailedContent = "first detailed summary",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        DbContext.Summaries.Add(new Summary
        {
            RepositoryId = repository.Id,
            ShortContent = "second summary",
            DetailedContent = "second detailed summary",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task TrendAggregate_DuplicateNaturalKey_ViolatesUniqueConstraint()
    {
        // F-016/NFR-003: AggregateTrendsCommandHandler upserts by (Category, PeriodStart, PeriodEnd)
        // - this proves the schema itself now enforces that natural key, not just the handler's own
        // query-then-upsert logic / the [DisableConcurrentExecution] guard on AggregateTrendsJob.
        var periodStart = new DateOnly(2026, 8, 1);
        var periodEnd = new DateOnly(2026, 8, 1);

        DbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = "C#",
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RepositoryCount = 1,
            AverageScore = 50,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await DbContext.SaveChangesAsync();

        DbContext.TrendAggregates.Add(new TrendAggregate
        {
            Category = "C#",
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RepositoryCount = 2,
            AverageScore = 60,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task DigestSendLog_DuplicateSentForDate_ViolatesUniqueConstraint()
    {
        // F-016/NFR-003: SendDigestCommandHandler's own sequential-retry dedupe guard relies on at
        // most one DigestSendLog row existing per calendar day - this proves the schema itself
        // enforces that, not just the handler's own read-then-write check.
        var sentForDate = new DateOnly(2026, 8, 1);

        DbContext.DigestSendLogs.Add(new DigestSendLog { SentForDate = sentForDate, SentAtUtc = DateTimeOffset.UtcNow });
        await DbContext.SaveChangesAsync();

        DbContext.DigestSendLogs.Add(new DigestSendLog { SentForDate = sentForDate, SentAtUtc = DateTimeOffset.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => DbContext.SaveChangesAsync());
    }

    // F-017: index-presence tests for the dashboard's filter/sort paths. These verify that the
    // indexes added by AddF017DashboardIndexes exist in the EF Core model, using the same
    // model-inspection pattern as the unique-constraint tests above. They assert the model
    // configuration; the physical indexes the migration chain actually creates are asserted
    // separately below against pg_indexes, which only became possible with review finding H-4.

    [Fact]
    public void Model_HasIndex_OnRepository_FirstDiscoveredAtUtc()
    {
        var index = FindIndex<Repository>(r => r.FirstDiscoveredAtUtc);
        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Model_HasIndex_OnRepository_PrimaryLanguage()
    {
        var index = FindIndex<Repository>(r => r.PrimaryLanguage);
        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Model_HasIndex_OnRepository_StarCount()
    {
        var index = FindIndex<Repository>(r => r.StarCount);
        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Model_HasIndex_OnRepository_LicenseIdentifier()
    {
        var index = FindIndex<Repository>(r => r.LicenseIdentifier);
        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Model_HasIndex_OnRepository_Topics()
    {
        // GIN index on Topics for array-overlap filtering. This asserts the index exists on the
        // model; that its method is actually gin in the database is asserted by
        // Migrations_CreateGinIndex_OnRepositoryTopics below.
        var entityType = DbContext.Model.FindEntityType(typeof(Repository));
        var topicsProperty = entityType!.FindProperty(nameof(Repository.Topics));
        var index = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 1 && i.Properties[0] == topicsProperty);
        Assert.NotNull(index);
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void Model_HasCompositeIndex_OnScore_RepositoryIdAndComputedAtUtc()
    {
        // F-017 replaces the old non-unique RepositoryId-only index with a composite
        // (RepositoryId, ComputedAtUtc DESC) covering index for "latest Score per repository"
        // lookups - the most frequent query pattern in the codebase.
        var entityType = DbContext.Model.FindEntityType(typeof(Score));
        var repoIdProperty = entityType!.FindProperty(nameof(Score.RepositoryId));
        var computedAtProperty = entityType!.FindProperty(nameof(Score.ComputedAtUtc));
        var index = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 2
            && i.Properties[0] == repoIdProperty
            && i.Properties[1] == computedAtProperty);
        Assert.NotNull(index);
        Assert.False(index.IsUnique);

        // Verify the old RepositoryId-only index no longer exists (replaced by the composite).
        var oldIndex = entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 1 && i.Properties[0] == repoIdProperty);
        Assert.Null(oldIndex);
    }

    // F-016: verify pre-existing unique constraints are NOT weakened by F-017's migration.
    // These are redundant with the unique-constraint tests above but serve as explicit regression
    // guards for the F-017 context.

    // The tests above assert what the EF model declares. These assert what the migration chain
    // actually built in PostgreSQL, which is a different claim and the one that matters: a model
    // index that no migration ever created would satisfy every test above and still leave the
    // query unindexed in production. Under the suite's previous SQLite provider this was not
    // checkable at all - the PostgreSQL-specific parts were silently dropped (review finding H-4).

    private async Task<List<string>> GetIndexDefinitionsAsync(string table)
    {
        var definitions = new List<string>();

        await using var command = DbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = @table";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "table";
        parameter.Value = table;
        command.Parameters.Add(parameter);

        await DbContext.Database.OpenConnectionAsync();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            definitions.Add(reader.GetString(0));
        }

        return definitions;
    }

    [Fact]
    public async Task Migrations_CreateGinIndex_OnRepositoryTopics()
    {
        // HasMethod("gin") is the one piece of index configuration that is not merely a performance
        // detail: array-overlap (&&) filtering on Topics cannot use a btree index at all, so if the
        // migration emitted the default method the Topic facet would full-scan.
        var definitions = await GetIndexDefinitionsAsync("Repositories");

        var topicsIndex = definitions.Find(d => d.Contains("\"Topics\"", StringComparison.Ordinal));

        Assert.NotNull(topicsIndex);
        Assert.Contains("USING gin", topicsIndex, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migrations_CreateCompositeIndex_OnScoreRepositoryIdAndComputedAtUtc()
    {
        var definitions = await GetIndexDefinitionsAsync("Scores");

        Assert.Contains(definitions, d =>
            d.Contains("\"RepositoryId\"", StringComparison.Ordinal)
            && d.Contains("\"ComputedAtUtc\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Migrations_CreateEveryIndexTheModelDeclares()
    {
        // Catches the general form of the drift the two tests above catch specifically: a HasIndex
        // added to the model without a migration to match it.
        foreach (var entityType in DbContext.Model.GetEntityTypes())
        {
            var table = entityType.GetTableName();
            if (table is null)
            {
                continue;
            }

            var definitions = await GetIndexDefinitionsAsync(table);

            foreach (var index in entityType.GetIndexes())
            {
                var columns = index.Properties.Select(p => p.GetColumnName()).ToList();

                Assert.True(
                    definitions.Exists(d => columns.TrueForAll(c => d.Contains($"\"{c}\"", StringComparison.Ordinal))),
                    $"Model declares an index on {table}({string.Join(", ", columns)}) that no migration created.");
            }
        }
    }

    [Fact]
    public void Model_F017_DoesNotWeaken_F016_UniqueConstraints()
    {
        // Repository.GitHubId must remain unique.
        var repoIndex = FindIndex<Repository>(r => r.GitHubId);
        Assert.NotNull(repoIndex);
        Assert.True(repoIndex.IsUnique);

        // Summary.RepositoryId must remain unique.
        var summaryIndex = FindIndex<Summary>(s => s.RepositoryId);
        Assert.NotNull(summaryIndex);
        Assert.True(summaryIndex.IsUnique);

        // Bookmark.RepositoryId must remain unique.
        var bookmarkIndex = FindIndex<Bookmark>(b => b.RepositoryId);
        Assert.NotNull(bookmarkIndex);
        Assert.True(bookmarkIndex.IsUnique);

        // TrendAggregate (Category, PeriodStart, PeriodEnd) must remain unique.
        var trendEntityType = DbContext.Model.FindEntityType(typeof(TrendAggregate));
        var categoryProp = trendEntityType!.FindProperty(nameof(TrendAggregate.Category));
        var periodStartProp = trendEntityType!.FindProperty(nameof(TrendAggregate.PeriodStart));
        var periodEndProp = trendEntityType!.FindProperty(nameof(TrendAggregate.PeriodEnd));
        var trendIndex = trendEntityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 3
            && i.Properties[0] == categoryProp
            && i.Properties[1] == periodStartProp
            && i.Properties[2] == periodEndProp);
        Assert.NotNull(trendIndex);
        Assert.True(trendIndex.IsUnique);

        // DigestSendLog.SentForDate must remain unique.
        var digestIndex = FindIndex<DigestSendLog>(d => d.SentForDate);
        Assert.NotNull(digestIndex);
        Assert.True(digestIndex.IsUnique);
    }

    private IReadOnlyIndex? FindIndex<TEntity>(
        Expression<Func<TEntity, object?>> propertySelector) where TEntity : class
    {
        var entityType = DbContext.Model.FindEntityType(typeof(TEntity));
        if (entityType is null)
        {
            return null;
        }

        // Extract the property name from the expression.
        var memberExpression = propertySelector.Body switch
        {
            MemberExpression m => m,
            UnaryExpression { Operand: MemberExpression m } => m,
            _ => throw new ArgumentException("Expression must be a simple property access."),
        };
        var property = entityType.FindProperty(memberExpression.Member.Name);
        return property is null ? null : entityType.GetIndexes().FirstOrDefault(i =>
            i.Properties.Count == 1 && i.Properties[0] == property);
    }

    private static Repository NewRepository(long gitHubId, string name = "hello-world") => new()
    {
        GitHubId = gitHubId,
        Owner = "octocat",
        Name = name,
        Url = $"https://github.com/octocat/{name}",
        DefaultBranch = "main",
        CreatedAtUtc = DateTimeOffset.UtcNow,
    };
}
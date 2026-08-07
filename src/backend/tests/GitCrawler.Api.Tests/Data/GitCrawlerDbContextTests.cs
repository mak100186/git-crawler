using System.Linq.Expressions;

using GitCrawler.Api.Data;
using GitCrawler.Api.Data.Entities;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace GitCrawler.Api.Tests.Data;

// Uses a real SQLite in-memory database (not the EF Core InMemory provider) so relational
// constraints - unique indexes, foreign keys - are actually enforced by the engine. The InMemory
// provider's constraint semantics are too weak to catch a broken unique-index config, which is
// exactly what the edge-case tests below need to verify.
public class GitCrawlerDbContextTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly GitCrawlerDbContext _context;

    public GitCrawlerDbContextTests()
    {
        // The in-memory SQLite database is destroyed the moment its last connection closes, so
        // this connection must stay open for the test's lifetime rather than being opened per call.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<GitCrawlerDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new GitCrawlerDbContext(options);

        // Exercises full EF Core model validation and generates the schema - a broken
        // FK/index/relationship configuration would throw here.
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Context_ExposesDbSetForEveryEntity()
    {
        Assert.NotNull(_context.Repositories);
        Assert.NotNull(_context.Scores);
        Assert.NotNull(_context.Summaries);
        Assert.NotNull(_context.TrendAggregates);
        Assert.NotNull(_context.Bookmarks);
        Assert.NotNull(_context.DigestSendLogs);
    }

    [Fact]
    public void Model_DefinesEveryEntityType()
    {
        var model = _context.Model;

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
        _context.Repositories.Add(repository);
        await _context.SaveChangesAsync();

        _context.Scores.Add(new Score
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
        await _context.SaveChangesAsync();

        var reloaded = await _context.Scores.Include(s => s.Repository).SingleAsync();

        Assert.Equal(repository.Id, reloaded.Repository.Id);
        Assert.True(reloaded.HasLicense);
        Assert.Equal("MIT", reloaded.LicenseType);
    }

    [Fact]
    public async Task Summary_PersistsAndLoads_ThroughRepositoryForeignKey()
    {
        var repository = NewRepository(gitHubId: 2);
        _context.Repositories.Add(repository);
        await _context.SaveChangesAsync();

        _context.Summaries.Add(new Summary
        {
            RepositoryId = repository.Id,
            ShortContent = "A concise summary.",
            DetailedContent = "A more detailed summary.",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        var reloaded = await _context.Summaries.Include(s => s.Repository).SingleAsync();

        Assert.Equal(repository.Id, reloaded.Repository.Id);
    }

    [Fact]
    public async Task Bookmark_PersistsAndLoads_ThroughRepositoryForeignKey()
    {
        var repository = NewRepository(gitHubId: 3);
        _context.Repositories.Add(repository);
        await _context.SaveChangesAsync();

        _context.Bookmarks.Add(new Bookmark
        {
            RepositoryId = repository.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        var reloaded = await _context.Bookmarks.Include(b => b.Repository).SingleAsync();

        Assert.Equal(repository.Id, reloaded.Repository.Id);
    }

    [Fact]
    public async Task Repository_DuplicateGitHubId_ViolatesUniqueConstraint()
    {
        // F-005's Crawler upserts by GitHubId (F-001 spike finding: re-crawls must not create
        // duplicate records) - this proves the schema itself enforces it, not just application
        // code.
        _context.Repositories.Add(NewRepository(gitHubId: 42));
        await _context.SaveChangesAsync();

        _context.Repositories.Add(NewRepository(gitHubId: 42, name: "hello-world-fork"));

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task Bookmark_DuplicateRepositoryId_ViolatesUniqueConstraint()
    {
        var repository = NewRepository(gitHubId: 99);
        _context.Repositories.Add(repository);
        await _context.SaveChangesAsync();

        _context.Bookmarks.Add(new Bookmark { RepositoryId = repository.Id, CreatedAtUtc = DateTimeOffset.UtcNow });
        await _context.SaveChangesAsync();

        _context.Bookmarks.Add(new Bookmark { RepositoryId = repository.Id, CreatedAtUtc = DateTimeOffset.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
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
        _context.Repositories.Add(repository);
        await _context.SaveChangesAsync();

        _context.Summaries.Add(new Summary
        {
            RepositoryId = repository.Id,
            ShortContent = "first summary",
            DetailedContent = "first detailed summary",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        _context.Summaries.Add(new Summary
        {
            RepositoryId = repository.Id,
            ShortContent = "second summary",
            DetailedContent = "second detailed summary",
            GeneratedAtUtc = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task TrendAggregate_DuplicateNaturalKey_ViolatesUniqueConstraint()
    {
        // F-016/NFR-003: AggregateTrendsCommandHandler upserts by (Category, PeriodStart, PeriodEnd)
        // - this proves the schema itself now enforces that natural key, not just the handler's own
        // query-then-upsert logic / the [DisableConcurrentExecution] guard on AggregateTrendsJob.
        var periodStart = new DateOnly(2026, 8, 1);
        var periodEnd = new DateOnly(2026, 8, 1);

        _context.TrendAggregates.Add(new TrendAggregate
        {
            Category = "C#",
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RepositoryCount = 1,
            AverageScore = 50,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });
        await _context.SaveChangesAsync();

        _context.TrendAggregates.Add(new TrendAggregate
        {
            Category = "C#",
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            RepositoryCount = 2,
            AverageScore = 60,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task DigestSendLog_DuplicateSentForDate_ViolatesUniqueConstraint()
    {
        // F-016/NFR-003: SendDigestCommandHandler's own sequential-retry dedupe guard relies on at
        // most one DigestSendLog row existing per calendar day - this proves the schema itself
        // enforces that, not just the handler's own read-then-write check.
        var sentForDate = new DateOnly(2026, 8, 1);

        _context.DigestSendLogs.Add(new DigestSendLog { SentForDate = sentForDate, SentAtUtc = DateTimeOffset.UtcNow });
        await _context.SaveChangesAsync();

        _context.DigestSendLogs.Add(new DigestSendLog { SentForDate = sentForDate, SentAtUtc = DateTimeOffset.UtcNow });

        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    // F-017: index-presence tests for the dashboard's filter/sort paths. These verify that the
    // indexes added by AddF017DashboardIndexes exist in the EF Core model, using the same
    // model-inspection pattern as the unique-constraint tests above (which exercise the model
    // via EnsureCreated + actual DB constraint enforcement). The SQLite in-memory provider used
    // here doesn't enforce non-unique indexes the way PostgreSQL does, so these tests verify the
    // model configuration rather than the physical index existence - that's the appropriate level
    // for a unit test (the physical indexes are verified by the seed harness's EXPLAIN ANALYZE).

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
        // GIN index on Topics for array-overlap filtering. The index method is Postgres-specific
        // (silently ignored by SQLite), so this test verifies the index EXISTS on the model, not
        // the method.
        var entityType = _context.Model.FindEntityType(typeof(Repository));
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
        var entityType = _context.Model.FindEntityType(typeof(Score));
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
        var trendEntityType = _context.Model.FindEntityType(typeof(TrendAggregate));
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
        var entityType = _context.Model.FindEntityType(typeof(TEntity));
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
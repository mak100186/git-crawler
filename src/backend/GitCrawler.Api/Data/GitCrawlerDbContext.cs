using GitCrawler.Api.Data.Entities;

using Microsoft.EntityFrameworkCore;

namespace GitCrawler.Api.Data;

// The Data Store is a leaf component with no dependencies (Architecture §3) shared by every other
// Phase 1 feature (Crawler, Scoring Engine, Summarizer, Trend Aggregator, Web API) - that's why it
// lives at the top-level Data/ folder rather than nested under Features/, unlike the
// command/query-slice convention (ADR-015) used everywhere else in this codebase.
//
// Hangfire.PostgreSql (wired by F-006, not here) creates and manages its own job-storage tables in
// this same PostgreSQL database via UsePostgreSqlStorage(...) at runtime, not through EF Core
// migrations - hand-writing those tables here would conflict with what it auto-creates. It
// defaults to its own "hangfire" Postgres schema, which is naturally already separate from EF
// Core's default "public" schema used by the DbSets below, so the two coexist without collision.
// Do not add EF Core entities or migrations for Hangfire's own tables here; F-006 owns calling
// UsePostgreSqlStorage.
public class GitCrawlerDbContext(DbContextOptions<GitCrawlerDbContext> options) : DbContext(options)
{
    public DbSet<Repository> Repositories => Set<Repository>();

    public DbSet<Score> Scores => Set<Score>();

    public DbSet<Summary> Summaries => Set<Summary>();

    public DbSet<TrendAggregate> TrendAggregates => Set<TrendAggregate>();

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    public DbSet<DigestSendLog> DigestSendLogs => Set<DigestSendLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Repository>(entity =>
        {
            // Unique on GitHubId (GitHub's own repository ID), not the local Id - the Crawler
            // (F-005) upserts by this column, and re-crawls must not create duplicates (F-001
            // spike finding).
            entity.HasIndex(r => r.GitHubId).IsUnique();

            // F-017/NFR-004: indexes supporting the dashboard's filter/sort paths, measured
            // against a seeded 100k-repo dataset via EXPLAIN ANALYZE. Each index below targets
            // a specific column the GetHiddenGems query filters or sorts on; none are speculative.
            //
            // FirstDiscoveredAtUtc: the default "Newest" sort key (RepositorySortField.Newest,
            // the codebase's original default before Score overtook it). Without this, every
            // "Newest" page request at scale would full-scan Repositories just to ORDER BY.
            entity.HasIndex(r => r.FirstDiscoveredAtUtc);

            // PrimaryLanguage: both the Language facet filter (WHERE ... IN (...)) and
            // GetCategoriesQueryHandler's DISTINCT PrimaryLanguage query. Without this, the
            // Language filter dropdown's option list and every filtered page request full-scan.
            entity.HasIndex(r => r.PrimaryLanguage);

            // StarCount: the star-range facet (MinStars/MaxStars). Repositories are filtered by
            // range in GetHiddenGems and the star-range facet, and sorted by Stars.
            entity.HasIndex(r => r.StarCount);

            // LicenseIdentifier: the License facet filter (WHERE ... IN (...)).
            entity.HasIndex(r => r.LicenseIdentifier);

            // Topics: the Topic facet filter uses array-overlap (r.Topics.Any(t => topics.Contains
            // (t))), which Npgsql translates to the `&&` operator. A GIN index is the correct
            // index type for array-overlap queries on PostgreSQL; HasMethod("gin") is silently
            // ignored by the SQLite provider (used by the xUnit test suite's EnsureCreated path).
            entity.HasIndex(r => r.Topics).HasMethod("gin");
        });

        modelBuilder.Entity<Score>(entity =>
        {
            entity.HasOne(s => s.Repository)
                .WithMany(r => r.Scores)
                .HasForeignKey(s => s.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // F-017/NFR-004: composite covering "latest Score per repository" - the most frequent
            // lookup pattern in the codebase (GetHiddenGems sort by Score/Commits, TrendGrowth's
            // latest-two-rows, digest top-N selection, summarizer eligibility, trend aggregation).
            // RepositoryId leading + ComputedAtUtc DESC gives an index-only scan for the
            // correlated subquery `r.Scores.OrderByDescending(s => s.ComputedAtUtc)
            // .Select(s => s.TotalScore).FirstOrDefault()` used in ApplySort. Replaces the
            // plain non-unique RepositoryId index that existed since F-004: the composite's
            // leading column already satisfies every query the old index covered (equality on
            // RepositoryId), so the old index would only add write overhead as a redundant copy.
            // The INCLUDE columns (TotalScore, CommitsPerWeek) let the sort's correlated subquery
            // read its value from the index leaf alone without a heap fetch, measurable at the
            // seeded 100k/1M scale.
            entity.HasIndex(s => new { s.RepositoryId, s.ComputedAtUtc })
                .IsDescending(false, true)
                .IncludeProperties(s => new { s.TotalScore, s.CommitsPerWeek });
        });

        modelBuilder.Entity<Summary>(entity =>
        {
            entity.HasOne(s => s.Repository)
                .WithMany(r => r.Summaries)
                .HasForeignKey(s => s.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // F-016/NFR-003: unique, not just indexed - a Summary is create-once, never regenerated
            // (GenerateSummariesCommandHandler's own comment: "a Summary is never regenerated once
            // created"), so at most one row should ever exist per repository. Mirrors
            // Bookmark.RepositoryId's own unique index below exactly. This used to be a plain
            // (non-unique) index, correctness resting entirely on GenerateSummariesJob's Hangfire
            // execution being single-threaded - [DisableConcurrentExecution] (added on that job
            // alongside this migration) now makes a concurrent double-insert practically
            // unreachable in normal operation; this constraint is the defense-in-depth backstop,
            // not the primary fix (see that job's own comment, and this feature's Task Packet
            // Constraints on not over-building exception handling around it).
            entity.HasIndex(s => s.RepositoryId).IsUnique();
        });

        modelBuilder.Entity<Bookmark>(entity =>
        {
            entity.HasOne(b => b.Repository)
                .WithMany(r => r.Bookmarks)
                .HasForeignKey(b => b.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            // One bookmark per repository - there's no user model to scope multiple bookmarks by
            // (single-operator v1, per PRD).
            entity.HasIndex(b => b.RepositoryId).IsUnique();
        });

        modelBuilder.Entity<TrendAggregate>(entity =>
        {
            // F-016/NFR-003: unique on the handler's actual natural key (Category, PeriodStart,
            // PeriodEnd) - AggregateTrendsCommandHandler upserts on exactly this triple (see that
            // class's own comment), but correctness previously rested entirely on this Hangfire
            // job being single-threaded, with no schema-level guarantee behind it.
            // [DisableConcurrentExecution] (added on that job alongside this migration) now makes a
            // concurrent double-insert practically unreachable in normal operation; this constraint
            // is the defense-in-depth backstop, not the primary fix (this feature's Task Packet
            // Constraints explicitly rule out over-building exception handling around it).
            // Replaces the old (Category, PeriodStart) index outright rather than keeping both -
            // that pair is already a left-prefix of this composite key, so Postgres can still use
            // this same index for a query filtered on just those two columns; a separate narrower
            // index would only add write overhead with no query benefit the composite doesn't
            // already cover.
            entity.HasIndex(t => new { t.Category, t.PeriodStart, t.PeriodEnd }).IsUnique();
        });

        modelBuilder.Entity<DigestSendLog>(entity =>
        {
            // One row per calendar day the digest was actually sent (SendDigestCommandHandler's own
            // sequential-retry dedupe guard - see DigestSendLog's header comment for why this is a
            // separate mechanism from TrendAggregate/Summary's own unique indexes above, which only
            // guard against *simultaneous* overlap, not a sequential Hangfire retry).
            entity.HasIndex(d => d.SentForDate).IsUnique();
        });
    }
}
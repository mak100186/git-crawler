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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Repository>(entity =>
        {
            // Unique on GitHubId (GitHub's own repository ID), not the local Id - the Crawler
            // (F-005) upserts by this column, and re-crawls must not create duplicates (F-001
            // spike finding).
            entity.HasIndex(r => r.GitHubId).IsUnique();
        });

        modelBuilder.Entity<Score>(entity =>
        {
            entity.HasOne(s => s.Repository)
                .WithMany(r => r.Scores)
                .HasForeignKey(s => s.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => s.RepositoryId);
        });

        modelBuilder.Entity<Summary>(entity =>
        {
            entity.HasOne(s => s.Repository)
                .WithMany(r => r.Summaries)
                .HasForeignKey(s => s.RepositoryId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(s => s.RepositoryId);
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
            entity.HasIndex(t => new { t.Category, t.PeriodStart });
        });
    }
}
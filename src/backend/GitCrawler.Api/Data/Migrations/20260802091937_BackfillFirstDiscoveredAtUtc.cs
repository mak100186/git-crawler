using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitCrawler.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class BackfillFirstDiscoveredAtUtc : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PM-006: AddRepositoryTopicsAndFirstDiscoveredAt (F-010) left every pre-existing
            // Repository row at FirstDiscoveredAtUtc = DateTimeOffset.MinValue - EF Core's scaffolded
            // default for a non-nullable DateTimeOffset column. Npgsql round-trips that specific
            // .NET value to Postgres's own timestamptz "-infinity" sentinel, not the finite literal
            // "0001-01-01" (confirmed live: `psql` shows the stored value as -infinity; a first
            // attempt at this migration compared against '0001-01-01'::timestamptz and matched zero
            // rows as a result - fixed here to compare against the actual stored sentinel). This is a
            // set-once field, so it never self-heals, and F-011's live Discovery Feed now visibly
            // renders these as "2025 years ago" under its default Newest sort. One-time data-only
            // backfill, no schema change: rows still at the sentinel get LastCrawledAtUtc (the
            // closest thing this system has to "when we actually came to know about this repo" - not
            // the true original discovery date, but a real, non-degenerate timestamp instead of
            // -infinity). Falls back to the migration's own apply time only for the edge case of a
            // row that was inserted but never subsequently (re-)crawled, so LastCrawledAtUtc itself
            // is null.
            //
            // Deliberately does NOT touch rows already past the sentinel (the WHERE clause) - this
            // must never overwrite a genuinely-set FirstDiscoveredAtUtc from a repo the Crawler has
            // discovered since F-010 shipped.
            migrationBuilder.Sql(
                """
                UPDATE "Repositories"
                SET "FirstDiscoveredAtUtc" = COALESCE("LastCrawledAtUtc", now())
                WHERE "FirstDiscoveredAtUtc" = '-infinity'::timestamptz;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: the original placeholder value carried no information (it's the
            // reason this backfill exists), so there is nothing meaningful to restore rows to.
        }
    }
}
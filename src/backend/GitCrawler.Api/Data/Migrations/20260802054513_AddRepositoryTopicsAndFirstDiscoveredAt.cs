using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitCrawler.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryTopicsAndFirstDiscoveredAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows backfill to DateTimeOffset.MinValue (the scaffolded default for a
            // non-nullable DateTimeOffset column) - same accepted precedent as
            // AddScoreStarCountSignal's StarCount defaultValue: 0, a neutral placeholder rather
            // than an attempt to reconstruct true historical discovery dates. Since
            // FirstDiscoveredAtUtc is set-once (never touched again after insert - see
            // DiscoverRepositoriesCommandHandler), pre-existing repos permanently keep this
            // placeholder and will sort as "oldest" under Discovery Feed's Newest sort until
            // manually backfilled - a known, accepted limitation of this additive migration, not a
            // correctness bug in the handler.
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "FirstDiscoveredAtUtc",
                table: "Repositories",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            // defaultValueSql (not the scaffolded default) is required here - without it, adding a
            // NOT NULL text[] column to a table that already has rows (this codebase's real
            // deployment target, since the Crawler has already run in prior phases) fails at apply
            // time. Same "cleanly-applying against existing data" concern AddScoreStarCountSignal's
            // defaultValue: 0 addressed for its own NOT NULL column - existing rows backfill to an
            // empty topic list rather than blocking the migration; the next crawl overwrites it with
            // the real value (Topics is refreshed every crawl, unlike FirstDiscoveredAtUtc below).
            migrationBuilder.AddColumn<List<string>>(
                name: "Topics",
                table: "Repositories",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstDiscoveredAtUtc",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "Topics",
                table: "Repositories");
        }
    }
}
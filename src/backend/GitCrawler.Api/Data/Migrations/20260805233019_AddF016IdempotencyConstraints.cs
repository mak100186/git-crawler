using System;

using Microsoft.EntityFrameworkCore.Migrations;

using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GitCrawler.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddF016IdempotencyConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TrendAggregates_Category_PeriodStart",
                table: "TrendAggregates");

            migrationBuilder.DropIndex(
                name: "IX_Summaries_RepositoryId",
                table: "Summaries");

            migrationBuilder.CreateTable(
                name: "DigestSendLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SentForDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SentAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DigestSendLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TrendAggregates_Category_PeriodStart_PeriodEnd",
                table: "TrendAggregates",
                columns: new[] { "Category", "PeriodStart", "PeriodEnd" },
                unique: true);

            // Installs upgrading from before the Hangfire idempotency guard (F-016) may already have
            // duplicate Summary rows per RepositoryId from concurrent/retried generation runs - the
            // unique index below would fail to create against that pre-existing data otherwise. Keeps
            // the most recently generated row per repo (tie-broken by Id) and drops the rest.
            migrationBuilder.Sql(@"
                DELETE FROM ""Summaries"" s
                USING ""Summaries"" s2
                WHERE s.""RepositoryId"" = s2.""RepositoryId""
                  AND (s.""GeneratedAtUtc"" < s2.""GeneratedAtUtc""
                       OR (s.""GeneratedAtUtc"" = s2.""GeneratedAtUtc"" AND s.""Id"" < s2.""Id""));
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Summaries_RepositoryId",
                table: "Summaries",
                column: "RepositoryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DigestSendLogs_SentForDate",
                table: "DigestSendLogs",
                column: "SentForDate",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DigestSendLogs");

            migrationBuilder.DropIndex(
                name: "IX_TrendAggregates_Category_PeriodStart_PeriodEnd",
                table: "TrendAggregates");

            migrationBuilder.DropIndex(
                name: "IX_Summaries_RepositoryId",
                table: "Summaries");

            migrationBuilder.CreateIndex(
                name: "IX_TrendAggregates_Category_PeriodStart",
                table: "TrendAggregates",
                columns: new[] { "Category", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_Summaries_RepositoryId",
                table: "Summaries",
                column: "RepositoryId");
        }
    }
}
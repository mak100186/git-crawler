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
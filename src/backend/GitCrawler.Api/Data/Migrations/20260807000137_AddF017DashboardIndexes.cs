using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitCrawler.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddF017DashboardIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scores_RepositoryId",
                table: "Scores");

            migrationBuilder.CreateIndex(
                name: "IX_Scores_RepositoryId_ComputedAtUtc",
                table: "Scores",
                columns: new[] { "RepositoryId", "ComputedAtUtc" },
                descending: new[] { false, true })
                .Annotation("Npgsql:IndexInclude", new[] { "TotalScore", "CommitsPerWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_FirstDiscoveredAtUtc",
                table: "Repositories",
                column: "FirstDiscoveredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_LicenseIdentifier",
                table: "Repositories",
                column: "LicenseIdentifier");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_PrimaryLanguage",
                table: "Repositories",
                column: "PrimaryLanguage");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_StarCount",
                table: "Repositories",
                column: "StarCount");

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_Topics",
                table: "Repositories",
                column: "Topics")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Scores_RepositoryId_ComputedAtUtc",
                table: "Scores");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_FirstDiscoveredAtUtc",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_LicenseIdentifier",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_PrimaryLanguage",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_StarCount",
                table: "Repositories");

            migrationBuilder.DropIndex(
                name: "IX_Repositories_Topics",
                table: "Repositories");

            migrationBuilder.CreateIndex(
                name: "IX_Scores_RepositoryId",
                table: "Scores",
                column: "RepositoryId");
        }
    }
}
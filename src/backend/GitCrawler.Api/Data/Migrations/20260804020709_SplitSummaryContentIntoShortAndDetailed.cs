using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitCrawler.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SplitSummaryContentIntoShortAndDetailed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Content",
                table: "Summaries",
                newName: "ShortContent");

            migrationBuilder.AddColumn<string>(
                name: "DetailedContent",
                table: "Summaries",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Every pre-existing Summary row predates DetailedContent and would otherwise sit with
            // it permanently empty (Summary is create-once - GenerateSummariesCommandHandler never
            // regenerates a row that already exists, so there's no automatic backfill path).
            // Operator-confirmed: "i dont mind if the existing summaries are deleted in this
            // implementation" -
            // clearing the table here, rather than leaving partially-populated rows to special-case
            // client-side, means every remaining/future row always has both fields populated
            // together. Affected repos simply get re-picked-up by GenerateSummariesCommandHandler's
            // own "no Summary row yet" selection filter on the next scheduled run (including the
            // standalone hourly trigger added alongside the earlier short/frequent-run change).
            migrationBuilder.Sql("DELETE FROM \"Summaries\";");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DetailedContent",
                table: "Summaries");

            migrationBuilder.RenameColumn(
                name: "ShortContent",
                table: "Summaries",
                newName: "Content");
        }
    }
}
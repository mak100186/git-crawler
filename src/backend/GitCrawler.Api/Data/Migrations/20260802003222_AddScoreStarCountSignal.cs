using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitCrawler.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScoreStarCountSignal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StarCount",
                table: "Scores",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StarCount",
                table: "Scores");
        }
    }
}
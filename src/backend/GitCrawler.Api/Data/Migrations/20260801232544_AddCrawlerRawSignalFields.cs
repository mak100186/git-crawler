using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GitCrawler.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCrawlerRawSignalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CommitCount",
                table: "Repositories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ContributorCount",
                table: "Repositories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ContributorCountFetchedAtUtc",
                table: "Repositories",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommitCount",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "ContributorCount",
                table: "Repositories");

            migrationBuilder.DropColumn(
                name: "ContributorCountFetchedAtUtc",
                table: "Repositories");
        }
    }
}
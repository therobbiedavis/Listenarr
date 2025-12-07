using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchSettingsToApplicationSettings_20251119170500 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableAmazonSearch",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAudibleSearch",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "EnableOpenLibrarySearch",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SearchCandidateCap",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<int>(
                name: "SearchResultCap",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 100);

            migrationBuilder.AddColumn<double>(
                name: "SearchFuzzyThreshold",
                table: "ApplicationSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.20000000000000001);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnableAmazonSearch",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "EnableAudibleSearch",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "EnableOpenLibrarySearch",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "SearchCandidateCap",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "SearchResultCap",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "SearchFuzzyThreshold",
                table: "ApplicationSettings");
        }
    }
}



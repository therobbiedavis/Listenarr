using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchSettingsToApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EnableAmazonSearch",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableAudibleSearch",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "EnableOpenLibrarySearch",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "SearchCandidateCap",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "SearchFuzzyThreshold",
                table: "ApplicationSettings",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "SearchResultCap",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
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
                name: "SearchFuzzyThreshold",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "SearchResultCap",
                table: "ApplicationSettings");
        }
    }
}

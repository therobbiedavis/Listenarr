using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEnhancedDownloadMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Asin",
                table: "Downloads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ExpectedFileSize",
                table: "Downloads",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Isbn",
                table: "Downloads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Downloads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Publisher",
                table: "Downloads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Runtime",
                table: "Downloads",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Series",
                table: "Downloads",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SeriesNumber",
                table: "Downloads",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Asin",
                table: "Downloads");

            migrationBuilder.DropColumn(
                name: "ExpectedFileSize",
                table: "Downloads");

            migrationBuilder.DropColumn(
                name: "Isbn",
                table: "Downloads");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Downloads");

            migrationBuilder.DropColumn(
                name: "Publisher",
                table: "Downloads");

            migrationBuilder.DropColumn(
                name: "Runtime",
                table: "Downloads");

            migrationBuilder.DropColumn(
                name: "Series",
                table: "Downloads");

            migrationBuilder.DropColumn(
                name: "SeriesNumber",
                table: "Downloads");
        }
    }
}

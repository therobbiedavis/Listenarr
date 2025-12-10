using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDownloadFinalizationSettingsToApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DownloadCompletionStabilitySeconds",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<int>(
                name: "MissingSourceRetryInitialDelaySeconds",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "MissingSourceMaxRetries",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 3);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MissingSourceMaxRetries",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "MissingSourceRetryInitialDelaySeconds",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DownloadCompletionStabilitySeconds",
                table: "ApplicationSettings");
        }
    }
}



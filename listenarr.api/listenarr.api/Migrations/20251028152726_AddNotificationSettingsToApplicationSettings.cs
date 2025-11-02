using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.listenarr.api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationSettingsToApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnabledNotificationTriggers",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnabledNotificationTriggers",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "ApplicationSettings");
        }
    }
}

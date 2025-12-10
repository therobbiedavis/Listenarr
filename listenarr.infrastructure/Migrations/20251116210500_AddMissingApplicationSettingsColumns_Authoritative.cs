using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    public partial class AddMissingApplicationSettingsColumns_Authoritative : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnabledNotificationTriggers",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "book-added|book-downloading|book-available|book-completed");

            migrationBuilder.AddColumn<bool>(
                name: "PreferUsDomain",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseUsProxy",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "UsProxyHost",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsProxyPort",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "UsProxyUsername",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UsProxyPassword",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WebhookUrl",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WebhookUrl",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "UsProxyPassword",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "UsProxyUsername",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "UsProxyPort",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "UsProxyHost",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "UseUsProxy",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "PreferUsDomain",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "EnabledNotificationTriggers",
                table: "ApplicationSettings");
        }
    }
}



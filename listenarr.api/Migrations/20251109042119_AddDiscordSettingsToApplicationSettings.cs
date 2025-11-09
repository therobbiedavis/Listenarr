using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordSettingsToApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiscordApplicationId",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordBotAvatar",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DiscordBotEnabled",
                table: "ApplicationSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DiscordBotToken",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordBotUsername",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordChannelId",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordCommandGroupName",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordCommandSubcommandName",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DiscordGuildId",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiscordGuildId",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DiscordCommandSubcommandName",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DiscordCommandGroupName",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DiscordChannelId",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DiscordBotUsername",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DiscordBotToken",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DiscordBotEnabled",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DiscordBotAvatar",
                table: "ApplicationSettings");

            migrationBuilder.DropColumn(
                name: "DiscordApplicationId",
                table: "ApplicationSettings");
        }
    }
}

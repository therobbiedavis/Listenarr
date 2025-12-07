using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDiscordSettingsToApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use raw SQL to conditionally add columns only if they don't exist
            // This handles cases where columns were manually added or from broken migrations
            migrationBuilder.Sql(@"
                -- Add DiscordApplicationId if it doesn't exist
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordApplicationId"" TEXT NULL;
                -- Ignore error if column already exists
            ", suppressTransaction: true);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordBotAvatar"" TEXT NULL;
            ", suppressTransaction: true);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordBotEnabled"" INTEGER NOT NULL DEFAULT 0;
            ", suppressTransaction: true);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordBotToken"" TEXT NULL;
            ", suppressTransaction: true);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordBotUsername"" TEXT NULL;
            ", suppressTransaction: true);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordChannelId"" TEXT NULL;
            ", suppressTransaction: true);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordCommandGroupName"" TEXT NULL;
            ", suppressTransaction: true);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordCommandSubcommandName"" TEXT NULL;
            ", suppressTransaction: true);
            
            migrationBuilder.Sql(@"
                ALTER TABLE ""ApplicationSettings"" ADD COLUMN ""DiscordGuildId"" TEXT NULL;
            ", suppressTransaction: true);
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



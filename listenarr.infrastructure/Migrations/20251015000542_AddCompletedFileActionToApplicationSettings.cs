using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCompletedFileActionToApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompletedFileAction",
                table: "ApplicationSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "Move");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedFileAction",
                table: "ApplicationSettings");
        }
    }
}



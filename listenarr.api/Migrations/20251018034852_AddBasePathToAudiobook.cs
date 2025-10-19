using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBasePathToAudiobook : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BasePath",
                table: "Audiobooks",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BasePath",
                table: "Audiobooks");
        }
    }
}

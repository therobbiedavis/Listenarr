using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddContainerAndCodecToAudiobookFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AudiobookFiles_AudiobookId",
                table: "AudiobookFiles");

            migrationBuilder.AddColumn<string>(
                name: "Codec",
                table: "AudiobookFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Container",
                table: "AudiobookFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudiobookFiles_AudiobookId_Path",
                table: "AudiobookFiles",
                columns: new[] { "AudiobookId", "Path" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AudiobookFiles_AudiobookId_Path",
                table: "AudiobookFiles");

            migrationBuilder.DropColumn(
                name: "Codec",
                table: "AudiobookFiles");

            migrationBuilder.DropColumn(
                name: "Container",
                table: "AudiobookFiles");

            migrationBuilder.CreateIndex(
                name: "IX_AudiobookFiles_AudiobookId",
                table: "AudiobookFiles",
                column: "AudiobookId");
        }
    }
}

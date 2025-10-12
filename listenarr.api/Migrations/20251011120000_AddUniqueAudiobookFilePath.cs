using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    public partial class AddUniqueAudiobookFilePath : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AudiobookFiles_AudiobookId_Path",
                table: "AudiobookFiles",
                columns: new[] { "AudiobookId", "Path" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AudiobookFiles_AudiobookId_Path",
                table: "AudiobookFiles");
        }
    }
}

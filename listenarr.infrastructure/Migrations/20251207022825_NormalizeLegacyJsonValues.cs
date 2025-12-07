using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeLegacyJsonValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AudiobookFiles_AudiobookId_Path",
                table: "AudiobookFiles");

            migrationBuilder.CreateIndex(
                name: "IX_AudiobookFiles_AudiobookId",
                table: "AudiobookFiles",
                column: "AudiobookId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AudiobookFiles_AudiobookId",
                table: "AudiobookFiles");

            migrationBuilder.CreateIndex(
                name: "IX_AudiobookFiles_AudiobookId_Path",
                table: "AudiobookFiles",
                columns: new[] { "AudiobookId", "Path" },
                unique: true);
        }
    }
}

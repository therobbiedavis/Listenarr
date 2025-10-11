using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAudiobookFileMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bitrate",
                table: "AudiobookFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Channels",
                table: "AudiobookFiles",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "AudiobookFiles",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Format",
                table: "AudiobookFiles",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SampleRate",
                table: "AudiobookFiles",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Bitrate",
                table: "AudiobookFiles");

            migrationBuilder.DropColumn(
                name: "Channels",
                table: "AudiobookFiles");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "AudiobookFiles");

            migrationBuilder.DropColumn(
                name: "Format",
                table: "AudiobookFiles");

            migrationBuilder.DropColumn(
                name: "SampleRate",
                table: "AudiobookFiles");
        }
    }
}

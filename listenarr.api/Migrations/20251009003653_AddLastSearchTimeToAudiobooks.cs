using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSearchTimeToAudiobooks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastSearchTime",
                table: "Audiobooks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Audiobooks_QualityProfileId",
                table: "Audiobooks",
                column: "QualityProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_Audiobooks_QualityProfiles_QualityProfileId",
                table: "Audiobooks",
                column: "QualityProfileId",
                principalTable: "QualityProfiles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Audiobooks_QualityProfiles_QualityProfileId",
                table: "Audiobooks");

            migrationBuilder.DropIndex(
                name: "IX_Audiobooks_QualityProfileId",
                table: "Audiobooks");

            migrationBuilder.DropColumn(
                name: "LastSearchTime",
                table: "Audiobooks");
        }
    }
}

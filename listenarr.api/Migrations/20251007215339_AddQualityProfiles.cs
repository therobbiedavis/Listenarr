using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "QualityProfileId",
                table: "Audiobooks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QualityProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: true),
                    Qualities = table.Column<string>(type: "TEXT", nullable: false),
                    CutoffQuality = table.Column<string>(type: "TEXT", nullable: true),
                    MinimumSize = table.Column<int>(type: "INTEGER", nullable: false),
                    MaximumSize = table.Column<int>(type: "INTEGER", nullable: false),
                    PreferredFormats = table.Column<string>(type: "TEXT", nullable: false),
                    PreferredWords = table.Column<string>(type: "TEXT", nullable: false),
                    MustNotContain = table.Column<string>(type: "TEXT", nullable: false),
                    MustContain = table.Column<string>(type: "TEXT", nullable: false),
                    PreferredLanguages = table.Column<string>(type: "TEXT", nullable: false),
                    MinimumSeeders = table.Column<int>(type: "INTEGER", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreferNewerReleases = table.Column<bool>(type: "INTEGER", nullable: false),
                    MaximumAge = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QualityProfiles", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QualityProfiles");

            migrationBuilder.DropColumn(
                name: "QualityProfileId",
                table: "Audiobooks");
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OutputPath = table.Column<string>(type: "TEXT", nullable: false),
                    FileNamingPattern = table.Column<string>(type: "TEXT", nullable: false),
                    EnableMetadataProcessing = table.Column<bool>(type: "INTEGER", nullable: false),
                    EnableCoverArtDownload = table.Column<bool>(type: "INTEGER", nullable: false),
                    AudnexusApiUrl = table.Column<string>(type: "TEXT", nullable: false),
                    MaxConcurrentDownloads = table.Column<int>(type: "INTEGER", nullable: false),
                    PollingIntervalSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    EnableNotifications = table.Column<bool>(type: "INTEGER", nullable: false),
                    AllowedFileExtensions = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");
        }
    }
}

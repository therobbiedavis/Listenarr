using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    public partial class AddProwlarrIndexers : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ConfigContract",
                table: "Indexers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApiPath",
                table: "Indexers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AddedByProwlarr",
                table: "Indexers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ProwlarrIndexerId",
                table: "Indexers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSyncedFromProwlarr",
                table: "Indexers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProwlarrSyncStatus",
                table: "Indexers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Indexers",
                type: "TEXT",
                nullable: true);

            // Create Tags table
            migrationBuilder.CreateTable(
                name: "Tags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tags", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Indexers_ProwlarrIndexerId",
                table: "Indexers",
                column: "ProwlarrIndexerId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Tags");

            migrationBuilder.DropIndex(
                name: "IX_Indexers_ProwlarrIndexerId",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "ConfigContract",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "ApiPath",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "AddedByProwlarr",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "ProwlarrIndexerId",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "LastSyncedFromProwlarr",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "ProwlarrSyncStatus",
                table: "Indexers");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "Indexers");
        }
    }
}

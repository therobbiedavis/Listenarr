using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_History_Timestamp",
                table: "History",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_CompletedAt",
                table: "Downloads",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_DownloadClientId",
                table: "Downloads",
                column: "DownloadClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Downloads_Status",
                table: "Downloads",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DownloadProcessingJobs_DownloadId_Status",
                table: "DownloadProcessingJobs",
                columns: new[] { "DownloadId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DownloadProcessingJobs_Status",
                table: "DownloadProcessingJobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Audiobooks_LastSearchTime",
                table: "Audiobooks",
                column: "LastSearchTime");

            migrationBuilder.CreateIndex(
                name: "IX_Audiobooks_Monitored",
                table: "Audiobooks",
                column: "Monitored");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_History_Timestamp",
                table: "History");

            migrationBuilder.DropIndex(
                name: "IX_Downloads_CompletedAt",
                table: "Downloads");

            migrationBuilder.DropIndex(
                name: "IX_Downloads_DownloadClientId",
                table: "Downloads");

            migrationBuilder.DropIndex(
                name: "IX_Downloads_Status",
                table: "Downloads");

            migrationBuilder.DropIndex(
                name: "IX_DownloadProcessingJobs_DownloadId_Status",
                table: "DownloadProcessingJobs");

            migrationBuilder.DropIndex(
                name: "IX_DownloadProcessingJobs_Status",
                table: "DownloadProcessingJobs");

            migrationBuilder.DropIndex(
                name: "IX_Audiobooks_LastSearchTime",
                table: "Audiobooks");

            migrationBuilder.DropIndex(
                name: "IX_Audiobooks_Monitored",
                table: "Audiobooks");
        }
    }
}



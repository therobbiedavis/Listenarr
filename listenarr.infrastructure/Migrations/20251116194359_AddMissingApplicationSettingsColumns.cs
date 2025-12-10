using Listenarr.Infrastructure.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingApplicationSettingsColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration records that the following columns were manually added to the ApplicationSettings table:
            // - EnabledNotificationTriggers (TEXT, NOT NULL, DEFAULT 'book-added|book-downloading|book-available|book-completed')
            // - PreferUsDomain (INTEGER, NOT NULL, DEFAULT 1)
            // - UseUsProxy (INTEGER, NOT NULL, DEFAULT 0)
            // - UsProxyHost (TEXT, NULL)
            // - UsProxyPort (INTEGER, NOT NULL, DEFAULT 0)
            // - UsProxyUsername (TEXT, NULL)
            // - UsProxyPassword (TEXT, NULL)
            //
            // These columns already exist in the database from manual additions.
            // This migration serves to track these changes in the migration history.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Since these columns were manually added and are now in production use,
            // we don't provide a down migration to avoid data loss.
            // If rollback is needed, the columns should be manually removed if appropriate.
        }
    }
}



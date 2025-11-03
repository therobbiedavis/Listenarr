using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultMetadataSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Only insert if they don't already exist (prevents duplicate key errors on re-run)
            migrationBuilder.Sql(@"
                INSERT INTO ApiConfigurations (Id, Name, BaseUrl, ApiKey, Type, IsEnabled, Priority, HeadersJson, ParametersJson, CreatedAt)
                SELECT 
                    lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
                    'Audimeta',
                    'https://audimeta.de',
                    '',
                    'metadata',
                    1,
                    1,
                    '{}',
                    '{}',
                    datetime('now')
                WHERE NOT EXISTS (SELECT 1 FROM ApiConfigurations WHERE Name = 'Audimeta');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO ApiConfigurations (Id, Name, BaseUrl, ApiKey, Type, IsEnabled, Priority, HeadersJson, ParametersJson, CreatedAt)
                SELECT 
                    lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' || substr(lower(hex(randomblob(2))),2) || '-' || substr('89ab',abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6))),
                    'Audnexus',
                    'https://api.audnex.us',
                    '',
                    'metadata',
                    1,
                    2,
                    '{}',
                    '{}',
                    datetime('now')
                WHERE NOT EXISTS (SELECT 1 FROM ApiConfigurations WHERE Name = 'Audnexus');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the default metadata sources
            migrationBuilder.Sql(@"
                DELETE FROM ApiConfigurations 
                WHERE Name IN ('Audimeta', 'Audnexus') 
                AND Type = 'metadata';
            ");
        }
    }
}

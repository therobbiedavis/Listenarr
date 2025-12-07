using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteNormalizeLegacyJsonValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This migration ensures any remaining legacy non-JSON tokens are
            // normalized to empty arrays where appropriate. It is idempotent.
            migrationBuilder.Sql(@"
-- Ensure empty arrays for any remaining invalid JSON values
UPDATE QualityProfiles SET Qualities = '[]' WHERE Qualities IS NOT NULL AND json_valid(Qualities) = 0;
UPDATE QualityProfiles SET PreferredFormats = '[]' WHERE PreferredFormats IS NOT NULL AND json_valid(PreferredFormats) = 0;
UPDATE QualityProfiles SET PreferredWords = '[]' WHERE PreferredWords IS NOT NULL AND json_valid(PreferredWords) = 0;
UPDATE QualityProfiles SET MustNotContain = '[]' WHERE MustNotContain IS NOT NULL AND json_valid(MustNotContain) = 0;
UPDATE QualityProfiles SET MustContain = '[]' WHERE MustContain IS NOT NULL AND json_valid(MustContain) = 0;
UPDATE QualityProfiles SET PreferredLanguages = '[]' WHERE PreferredLanguages IS NOT NULL AND json_valid(PreferredLanguages) = 0;

UPDATE Audiobooks SET Authors = '[]' WHERE Authors IS NOT NULL AND json_valid(Authors) = 0;
UPDATE Audiobooks SET Genres = '[]' WHERE Genres IS NOT NULL AND json_valid(Genres) = 0;
UPDATE Audiobooks SET Tags = '[]' WHERE Tags IS NOT NULL AND json_valid(Tags) = 0;
UPDATE Audiobooks SET Narrators = '[]' WHERE Narrators IS NOT NULL AND json_valid(Narrators) = 0;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No revert: normalization is one-way.
        }
    }
}

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Listenarr.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertLegacyDelimitedToJsonArrays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert pipe-delimited tokens into JSON arrays and wrap single-token
            // strings into single-element JSON arrays for common TEXT columns.
            // Uses char(34) to represent the double-quote character so the C#
            // string literal stays simple and the SQL is easy to read.
            migrationBuilder.Sql(@"
-- QualityProfiles: convert pipe-separated to JSON arrays
UPDATE QualityProfiles SET PreferredFormats = '[]' WHERE PreferredFormats IS NOT NULL AND json_valid(PreferredFormats)=0 AND trim(PreferredFormats)='';
UPDATE QualityProfiles SET PreferredFormats = json_array(PreferredFormats) WHERE PreferredFormats IS NOT NULL AND json_valid(PreferredFormats)=0 AND PreferredFormats NOT LIKE '%|%';
UPDATE QualityProfiles SET PreferredFormats = '[' || char(34) || replace(PreferredFormats, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE PreferredFormats IS NOT NULL AND json_valid(PreferredFormats)=0 AND PreferredFormats LIKE '%|%';

UPDATE QualityProfiles SET PreferredWords = json_array(PreferredWords) WHERE PreferredWords IS NOT NULL AND json_valid(PreferredWords)=0 AND PreferredWords NOT LIKE '%|%';
UPDATE QualityProfiles SET PreferredWords = '[' || char(34) || replace(PreferredWords, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE PreferredWords IS NOT NULL AND json_valid(PreferredWords)=0 AND PreferredWords LIKE '%|%';

UPDATE QualityProfiles SET MustNotContain = json_array(MustNotContain) WHERE MustNotContain IS NOT NULL AND json_valid(MustNotContain)=0 AND MustNotContain NOT LIKE '%|%';
UPDATE QualityProfiles SET MustNotContain = '[' || char(34) || replace(MustNotContain, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE MustNotContain IS NOT NULL AND json_valid(MustNotContain)=0 AND MustNotContain LIKE '%|%';

UPDATE QualityProfiles SET MustContain = json_array(MustContain) WHERE MustContain IS NOT NULL AND json_valid(MustContain)=0 AND MustContain NOT LIKE '%|%';
UPDATE QualityProfiles SET MustContain = '[' || char(34) || replace(MustContain, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE MustContain IS NOT NULL AND json_valid(MustContain)=0 AND MustContain LIKE '%|%';

UPDATE QualityProfiles SET PreferredLanguages = json_array(PreferredLanguages) WHERE PreferredLanguages IS NOT NULL AND json_valid(PreferredLanguages)=0 AND PreferredLanguages NOT LIKE '%|%';
UPDATE QualityProfiles SET PreferredLanguages = '[' || char(34) || replace(PreferredLanguages, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE PreferredLanguages IS NOT NULL AND json_valid(PreferredLanguages)=0 AND PreferredLanguages LIKE '%|%';

-- Audiobooks: Authors, Genres, Tags, Narrators
UPDATE Audiobooks SET Authors = json_array(Authors) WHERE Authors IS NOT NULL AND json_valid(Authors)=0 AND Authors NOT LIKE '%|%';
UPDATE Audiobooks SET Authors = '[' || char(34) || replace(Authors, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE Authors IS NOT NULL AND json_valid(Authors)=0 AND Authors LIKE '%|%';

UPDATE Audiobooks SET Genres = json_array(Genres) WHERE Genres IS NOT NULL AND json_valid(Genres)=0 AND Genres NOT LIKE '%|%';
UPDATE Audiobooks SET Genres = '[' || char(34) || replace(Genres, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE Genres IS NOT NULL AND json_valid(Genres)=0 AND Genres LIKE '%|%';

UPDATE Audiobooks SET Tags = json_array(Tags) WHERE Tags IS NOT NULL AND json_valid(Tags)=0 AND Tags NOT LIKE '%|%';
UPDATE Audiobooks SET Tags = '[' || char(34) || replace(Tags, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE Tags IS NOT NULL AND json_valid(Tags)=0 AND Tags LIKE '%|%';

UPDATE Audiobooks SET Narrators = json_array(Narrators) WHERE Narrators IS NOT NULL AND json_valid(Narrators)=0 AND Narrators NOT LIKE '%|%';
UPDATE Audiobooks SET Narrators = '[' || char(34) || replace(Narrators, '|', char(34) || ',' || char(34)) || char(34) || ']' WHERE Narrators IS NOT NULL AND json_valid(Narrators)=0 AND Narrators LIKE '%|%';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No revert: normalization is one-way.
        }
    }
}

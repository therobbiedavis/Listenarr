using System.Text.RegularExpressions;
using Listenarr.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Search;

/// <summary>
/// Handles merging and population of metadata from various sources (search results, scraped data, API responses).
/// </summary>
public class MetadataMerger
{
    private readonly ILogger<MetadataMerger> _logger;

    public MetadataMerger(ILogger<MetadataMerger> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Merges data from source metadata into target metadata, filling in missing fields.
    /// </summary>
    public void MergeMetadata(AudibleBookMetadata source, AudibleBookMetadata target)
    {
        _logger.LogInformation("Merging metadata: source.PublishYear={SourceYear}, target.PublishYear={TargetYear}, target.Asin={Asin}", 
            source.PublishYear, target.PublishYear, target.Asin);
            
        // Only merge fields that are missing in target
        if (string.IsNullOrEmpty(target.Title)) target.Title = source.Title;
        if (target.Authors == null || !target.Authors.Any()) target.Authors = source.Authors;
        if (target.Narrators == null || !target.Narrators.Any()) target.Narrators = source.Narrators;
        if (string.IsNullOrEmpty(target.Publisher)) target.Publisher = source.Publisher;
        if (string.IsNullOrEmpty(target.Description)) target.Description = source.Description;
        if (target.Genres == null || !target.Genres.Any()) target.Genres = source.Genres;
        if (string.IsNullOrEmpty(target.Language)) target.Language = source.Language;
        if (string.IsNullOrEmpty(target.ImageUrl)) target.ImageUrl = source.ImageUrl;
        if (!target.Runtime.HasValue && source.Runtime.HasValue) target.Runtime = source.Runtime;
        if (string.IsNullOrEmpty(target.PublishYear)) target.PublishYear = source.PublishYear;
        if (string.IsNullOrEmpty(target.Subtitle)) target.Subtitle = source.Subtitle;
        
        _logger.LogInformation("After merge: target.PublishYear={TargetYear}, target.Asin={Asin}", target.PublishYear, target.Asin);
    }

    /// <summary>
    /// Populate metadata from Audible search result data (has runtime, series, etc.)
    /// </summary>
    public AudibleBookMetadata PopulateMetadataFromSearchResult(AudibleSearchResult? searchResult)
    {
        var metadata = new AudibleBookMetadata
        {
            Source = "Audible"
        };

        if (searchResult == null)
        {
            _logger.LogDebug("No search result provided for metadata population");
            return metadata;
        }

        _logger.LogInformation("Populating metadata from search result: Duration={Duration}, Series={Series}, SeriesNumber={SeriesNumber}, Language={Language}, ReleaseDate={ReleaseDate}",
            searchResult.Duration, searchResult.Series, searchResult.SeriesNumber, searchResult.Language, searchResult.ReleaseDate);

        metadata.Asin = searchResult.Asin;
        metadata.Title = searchResult.Title;
        if (!string.IsNullOrEmpty(searchResult.Author))
        {
            metadata.Authors = new List<string> { searchResult.Author };
        }
        if (!string.IsNullOrEmpty(searchResult.Narrator))
        {
            metadata.Narrators = searchResult.Narrator.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(n => n.Trim())
                .ToList();
        }
        metadata.ImageUrl = searchResult.ImageUrl;
        // Don't use series from search results - HTML structure can cause incorrect series assignment
        // Series should only come from product page scraping or metadata APIs
        metadata.Subtitle = searchResult.Subtitle;
        metadata.Language = searchResult.Language;

        // Parse duration/runtime from search result (e.g., "Length: 21 hrs and 22 mins")
        if (!string.IsNullOrEmpty(searchResult.Duration))
        {
            var match = Regex.Match(searchResult.Duration, @"(\d+)\s*hrs?\s+(?:and\s+)?(\d+)\s*mins?", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                int hours = int.Parse(match.Groups[1].Value);
                int minutes = int.Parse(match.Groups[2].Value);
                metadata.Runtime = hours * 60 + minutes;
                _logger.LogInformation("Parsed runtime from search result: {Runtime} minutes ({Hours}h {Minutes}m)", metadata.Runtime, hours, minutes);
            }
        }

        // Parse release date to extract year
        if (!string.IsNullOrEmpty(searchResult.ReleaseDate))
        {
            var yearMatch = Regex.Match(searchResult.ReleaseDate, @"(\d{2})-(\d{2})-(\d{2})");
            if (yearMatch.Success)
            {
                var year = int.Parse(yearMatch.Groups[3].Value);
                metadata.PublishYear = (2000 + year).ToString();
                _logger.LogInformation("Parsed publish year from search result: {PublishYear} (from ReleaseDate: '{ReleaseDate}')", 
                    metadata.PublishYear, searchResult.ReleaseDate);
            }
            else
            {
                _logger.LogWarning("Could not parse publish year from ReleaseDate: '{ReleaseDate}'", searchResult.ReleaseDate);
            }
        }

        return metadata;
    }
}

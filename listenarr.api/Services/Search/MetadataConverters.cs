using System.Text.RegularExpressions;
using Listenarr.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Search;

/// <summary>
/// Converts external metadata API responses to internal AudibleBookMetadata and SearchResult models.
/// </summary>
public class MetadataConverters
{
    private readonly IImageCacheService _imageCacheService;
    private readonly ILogger<MetadataConverters> _logger;

    public MetadataConverters(IImageCacheService imageCacheService, ILogger<MetadataConverters> logger)
    {
        _imageCacheService = imageCacheService;
        _logger = logger;
    }

    /// <summary>
    /// Converts Amazon search result to SearchResult.
    /// </summary>
    public SearchResult ConvertAmazonSearchToResult(AmazonSearchResult amazonResult)
    {
        return new SearchResult
        {
            Title = amazonResult.Title ?? "Unknown Title",
            Artist = amazonResult.Author ?? "Unknown Author",
            Source = "Amazon",
            Asin = amazonResult.Asin ?? "",
            ImageUrl = amazonResult.ImageUrl ?? ""
        };
    }

    /// <summary>
    /// Converts Audimeta API response to AudibleBookMetadata.
    /// </summary>
    public AudibleBookMetadata ConvertAudimetaToMetadata(AudimetaBookResponse audimetaData, string asin, string source = "Audible")
    {
        var metadata = new AudibleBookMetadata
        {
            Asin = audimetaData.Asin ?? asin,
            Source = source, // Use the original search source (Amazon or Audible)
            Title = audimetaData.Title,
            Subtitle = audimetaData.Subtitle,
            Authors = audimetaData.Authors?.Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList(),
            Narrators = audimetaData.Narrators?.Select(n => n.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList(),
            Publisher = audimetaData.Publisher,
            Description = audimetaData.Description,
            Genres = audimetaData.Genres?.Select(g => g.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList(),
            Language = audimetaData.Language,
            Isbn = audimetaData.Isbn,
            ImageUrl = audimetaData.ImageUrl,
            Abridged = audimetaData.BookFormat?.Contains("abridged", StringComparison.OrdinalIgnoreCase) ?? false,
            Explicit = audimetaData.Explicit ?? false
        };

        // Handle series (audimeta returns array, we just take the first one)
        if (audimetaData.Series != null && audimetaData.Series.Any())
        {
            var firstSeries = audimetaData.Series.First();
            metadata.Series = firstSeries.Name;
            metadata.SeriesNumber = firstSeries.Position;
        }

        // Convert runtime from minutes to minutes (audimeta returns lengthMinutes)
        if (audimetaData.LengthMinutes.HasValue && audimetaData.LengthMinutes > 0)
        {
            metadata.Runtime = audimetaData.LengthMinutes.Value;
        }

        // Extract year from releaseDate (format: "2023-10-24T00:00:00.000+00:00")
        string? dateStr = audimetaData.ReleaseDate ?? audimetaData.PublishDate;
        if (!string.IsNullOrEmpty(dateStr))
        {
            var yearMatch = Regex.Match(dateStr, @"\d{4}");
            if (yearMatch.Success)
            {
                metadata.PublishYear = yearMatch.Value;
            }
        }

        _logger.LogInformation("Converted audimeta data for {Asin}: Title={Title}, Runtime={Runtime}min, Year={Year}, Series={Series}, ImageUrl={ImageUrl}",
            asin, metadata.Title, metadata.Runtime, metadata.PublishYear, metadata.Series, metadata.ImageUrl);

        return metadata;
    }

    /// <summary>
    /// Converts Audnexus API response to AudibleBookMetadata.
    /// </summary>
    public AudibleBookMetadata ConvertAudnexusToMetadata(AudnexusBookResponse audnexusData, string asin, string source = "Audible")
    {
        var metadata = new AudibleBookMetadata
        {
            Asin = audnexusData.Asin ?? asin,
            Source = source, // Use the original search source (Amazon or Audible)
            Title = audnexusData.Title,
            Subtitle = audnexusData.Subtitle,
            Authors = audnexusData.Authors?.Select(a => a.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList(),
            Narrators = audnexusData.Narrators?.Select(n => n.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList(),
            Publisher = audnexusData.PublisherName,
            Description = audnexusData.Description ?? audnexusData.Summary,
            Genres = audnexusData.Genres?.Select(g => g.Name).Where(n => !string.IsNullOrEmpty(n)).Select(n => n!).ToList(),
            Language = audnexusData.Language,
            Isbn = audnexusData.Isbn,
            ImageUrl = audnexusData.Image,
            Abridged = audnexusData.FormatType?.Contains("abridged", StringComparison.OrdinalIgnoreCase) ?? false,
            Explicit = audnexusData.IsAdult ?? false
        };

        // Handle series (primary series first, then secondary) - Audnexus returns single objects, not arrays
        if (audnexusData.SeriesPrimary != null)
        {
            metadata.Series = audnexusData.SeriesPrimary.Name;
            metadata.SeriesNumber = audnexusData.SeriesPrimary.Position;
        }
        else if (audnexusData.SeriesSecondary != null)
        {
            metadata.Series = audnexusData.SeriesSecondary.Name;
            metadata.SeriesNumber = audnexusData.SeriesSecondary.Position;
        }

        // Convert runtime from minutes
        if (audnexusData.RuntimeLengthMin.HasValue && audnexusData.RuntimeLengthMin > 0)
        {
            metadata.Runtime = audnexusData.RuntimeLengthMin.Value;
        }

        // Extract year from releaseDate (format: "2021-05-04T00:00:00.000Z")
        if (!string.IsNullOrEmpty(audnexusData.ReleaseDate))
        {
            var yearMatch = Regex.Match(audnexusData.ReleaseDate, @"\d{4}");
            if (yearMatch.Success)
            {
                metadata.PublishYear = yearMatch.Value;
            }
        }
        // Fallback to copyright year if no release date
        else if (audnexusData.Copyright.HasValue)
        {
            metadata.PublishYear = audnexusData.Copyright.Value.ToString();
        }

        _logger.LogInformation("Converted Audnexus data for {Asin}: Title={Title}, Runtime={Runtime}min, Year={Year}, Series={Series}, ImageUrl={ImageUrl}",
            asin, metadata.Title, metadata.Runtime, metadata.PublishYear, metadata.Series, metadata.ImageUrl);

        return metadata;
    }

    /// <summary>
    /// Converts AudibleBookMetadata to SearchResult with fallback handling for missing fields.
    /// </summary>
    public Task<SearchResult> ConvertMetadataToSearchResultAsync(AudibleBookMetadata metadata, string asin, string? fallbackTitle = null, string? fallbackAuthor = null, string? fallbackImageUrl = null)
    {
        // Use metadata if available, otherwise fallback to raw search result, finally to generic fallback
        var title = metadata.Title;
        if (string.IsNullOrWhiteSpace(title) || title == "Audible" || title.Contains("English - USD"))
        {
            title = fallbackTitle;
        }
        if (string.IsNullOrWhiteSpace(title))
        {
            title = "Unknown Title";
        }

        var author = metadata.Authors?.FirstOrDefault();
        if (SearchValidation.IsAuthorNoise(author))
        {
            author = fallbackAuthor;
        }
        if (SearchValidation.IsAuthorNoise(author))
        {
            author = "Unknown Author";
        }

        var imageUrl = metadata.ImageUrl;
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            imageUrl = fallbackImageUrl;
        }

        // Download and cache the image to temp storage for future use
        // Keep the original external URL for search results to avoid 404s
        if (!string.IsNullOrEmpty(imageUrl) && !string.IsNullOrEmpty(asin))
        {
            try
            {
                // Cache the image in background, but don't wait for it or change the URL
                // This ensures search results always show images immediately from their source
                _ = _imageCacheService.DownloadAndCacheImageAsync(imageUrl, asin);
                _logger.LogDebug("Started background image cache for ASIN {Asin}", asin);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initiate image caching for ASIN {Asin}", asin);
            }
        }

        // Generate product URL based on source and ASIN
        string? productUrl = null;
        if (!string.IsNullOrEmpty(asin))
        {
            productUrl = metadata.Source == "Amazon"
                ? $"https://www.amazon.com/dp/{asin}"
                : $"https://www.audible.com/pd/{asin}";
        }

        return Task.FromResult(new SearchResult
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Artist = author ?? "Unknown Author",
            Album = metadata.Series ?? metadata.Title ?? "Unknown Album",
            Category = string.Join(", ", metadata.Genres ?? new List<string> { "Audiobook" }),
            Size = 0, // We don't have file size from metadata
            Seeders = 0, // Not applicable for direct Amazon results
            Leechers = 0, // Not applicable for direct Amazon results
            MagnetLink = $"amazon://asin/{asin}", // Use a custom scheme to identify Amazon sources
            Source = metadata.Source ?? "Amazon/Audible", // Use the metadata source (Audible or Amazon) if available
            MetadataSource = metadata.Source, // Set the metadata source for display
            SourceLink = productUrl, // Link to the product page
            PublishedDate = !string.IsNullOrEmpty(metadata.PublishYear) && int.TryParse(metadata.PublishYear, out var year) ? new DateTime(year, 1, 1) : DateTime.MinValue,
            Quality = metadata.Version ?? "Unknown",
            Format = "Audiobook",
            Description = metadata.Description,
            Publisher = metadata.Publisher,
            Language = metadata.Language,
            Runtime = metadata.Runtime,
            Narrator = string.Join(", ", metadata.Narrators ?? new List<string>()),
            Series = metadata.Series,
            SeriesNumber = metadata.SeriesNumber,
            ImageUrl = imageUrl,
            Asin = asin,
            ProductUrl = productUrl
        });
    }
}

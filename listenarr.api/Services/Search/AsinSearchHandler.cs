using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Search;

/// <summary>
/// Handles direct ASIN queries with metadata-first approach.
/// </summary>
public class AsinSearchHandler
{
    private readonly ILogger<AsinSearchHandler> _logger;
    private readonly IConfigurationService _configurationService;
    private readonly AudimetaService _audimetaService;
    private readonly AudnexusService _audnexusService;
    private readonly IAudibleMetadataService _audibleMetadataService;
    private readonly IAmazonMetadataService _amazonMetadataService;
    private readonly MetadataConverters _metadataConverters;
    private readonly SearchProgressReporter _searchProgressReporter;

    public AsinSearchHandler(
        ILogger<AsinSearchHandler> logger,
        IConfigurationService configurationService,
        AudimetaService audimetaService,
        AudnexusService audnexusService,
        IAudibleMetadataService audibleMetadataService,
        IAmazonMetadataService amazonMetadataService,
        MetadataConverters metadataConverters,
        SearchProgressReporter searchProgressReporter)
    {
        _logger = logger;
        _configurationService = configurationService;
        _audimetaService = audimetaService;
        _audnexusService = audnexusService;
        _audibleMetadataService = audibleMetadataService;
        _amazonMetadataService = amazonMetadataService;
        _metadataConverters = metadataConverters;
        _searchProgressReporter = searchProgressReporter;
    }

    /// <summary>
    /// Searches for a specific ASIN using metadata APIs first, then fallback to scraping.
    /// </summary>
    public async Task<List<SearchResult>> SearchByAsinAsync(
        string asin,
        List<ApiConfiguration> metadataSources)
    {
        _logger.LogInformation("Processing direct ASIN query: {Asin}", asin);
        await _searchProgressReporter.BroadcastAsync($"Extracting ASIN: {asin}", null);

        // Load application settings for provider flags
        bool skipAmazon = false;
        bool skipAudible = false;

        try
        {
            var appSettings = await _configurationService.GetApplicationSettingsAsync();
            if (appSettings != null)
            {
                skipAmazon = !appSettings.EnableAmazonSearch;
                skipAudible = !appSettings.EnableAudibleSearch;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to load application settings for ASIN search");
        }

        // Step 1: Try to get metadata from configured sources (Audimeta, Audnexus)
        AudibleBookMetadata? metadata = null;
        string? metadataSourceName = null;

        if (metadataSources != null && metadataSources.Any())
        {
            _logger.LogInformation("Trying {Count} metadata source(s) for ASIN {Asin}", metadataSources.Count, asin);
            await _searchProgressReporter.BroadcastAsync($"Checking metadata sources for {asin}", null);

            foreach (var source in metadataSources.OrderBy(s => s.Priority))
            {
                try
                {
                    if (source.Name.Contains("Audimeta", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Attempting Audimeta for ASIN {Asin}", asin);
                        await _searchProgressReporter.BroadcastAsync($"Searching Audimeta for {asin}", null);
                        var audimetaData = await _audimetaService.GetBookMetadataAsync(asin, "us", true);
                        if (audimetaData != null)
                        {
                            metadata = _metadataConverters.ConvertAudimetaToMetadata(audimetaData, asin, "Audible");
                            metadataSourceName = source.Name;
                            _logger.LogInformation("Successfully got metadata from {Source} for ASIN {Asin}", source.Name, asin);
                            break;
                        }
                    }
                    else if (source.Name.Contains("Audnexus", StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Attempting Audnexus for ASIN {Asin}", asin);
                        await _searchProgressReporter.BroadcastAsync($"Searching Audnexus for {asin}", null);
                        var audnexusData = await _audnexusService.GetBookMetadataAsync(asin, "us", true, false);
                        if (audnexusData != null)
                        {
                            metadata = _metadataConverters.ConvertAudnexusToMetadata(audnexusData, asin, "Audible");
                            metadataSourceName = source.Name;
                            _logger.LogInformation("Successfully got metadata from {Source} for ASIN {Asin}", source.Name, asin);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to get metadata from {Source} for ASIN {Asin}", source.Name, asin);
                }
            }
        }

        // Step 2: If metadata sources failed, scrape product pages as fallback
        if (metadata == null)
        {
            _logger.LogInformation("No metadata found from APIs for ASIN {Asin}, falling back to product page scraping", asin);
            await _searchProgressReporter.BroadcastAsync($"Metadata sources unavailable, scraping product pages", null);

            // Try Audible scraping first (more reliable for audiobooks)
            if (!skipAudible)
            {
                try
                {
                    await _searchProgressReporter.BroadcastAsync($"Scraping Audible for {asin}", null);
                    var audibleMeta = await _audibleMetadataService.ScrapeAudibleMetadataAsync(asin);
                    if (audibleMeta != null)
                    {
                        metadata = audibleMeta;
                        metadataSourceName = audibleMeta.Source ?? "Audible";
                        _logger.LogInformation("Successfully scraped metadata for ASIN {Asin} from {Source}", asin, metadataSourceName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to scrape Audible for ASIN {Asin}", asin);
                }
            }

            // If Audible scraping failed, try Amazon
            if (metadata == null && !skipAmazon)
            {
                try
                {
                    await _searchProgressReporter.BroadcastAsync($"Scraping Amazon for {asin}", null);
                    var amazonMeta = await _amazonMetadataService.ScrapeAmazonMetadataAsync(asin);
                    if (amazonMeta != null)
                    {
                        metadata = amazonMeta;
                        metadataSourceName = amazonMeta.Source ?? "Amazon";
                        _logger.LogInformation("Successfully scraped metadata for ASIN {Asin} from {Source}", asin, metadataSourceName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to scrape Amazon for ASIN {Asin}", asin);
                }
            }
        }

        // Step 3: Convert metadata to SearchResult
        if (metadata != null)
        {
            await _searchProgressReporter.BroadcastAsync($"Found audiobook: {metadata.Title}", null);
            var result = await _metadataConverters.ConvertMetadataToSearchResultAsync(metadata, asin, null, null, null);
            result.IsEnriched = true;
            result.MetadataSource = metadataSourceName;

            // Set source and source link based on where metadata came from
            if (metadataSourceName == "Amazon")
            {
                result.Source = "Amazon";
                result.SourceLink = $"https://www.amazon.com/dp/{asin}";
            }
            else if (metadataSourceName == "Audible")
            {
                result.Source = "Audible";
                result.SourceLink = $"https://www.audible.com/pd/{asin}";
            }
            else
            {
                // Metadata API source - default to Audible for product link
                result.Source = "Audible";
                result.SourceLink = $"https://www.audible.com/pd/{asin}";
            }

            // Validate result before returning
            if (!string.IsNullOrWhiteSpace(result.Title) &&
                !result.Title.Equals("Amazon.com", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(result.Artist))
            {
                _logger.LogInformation("ASIN query succeeded for {Asin}, returning enriched result from {Source}", asin, metadataSourceName);
                return new List<SearchResult> { result };
            }
            else
            {
                _logger.LogWarning("ASIN query got invalid data for {Asin} (Title={Title}, Artist={Artist})", asin, result.Title, result.Artist);
            }
        }
        else
        {
            _logger.LogWarning("ASIN query failed for {Asin} - no metadata from APIs or scraping", asin);
        }

        // If we reach here, ASIN query failed - return empty list
        return new List<SearchResult>();
    }
}

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
    private readonly IAudnexusService _audnexusService;
    private readonly IAudibleMetadataService _audibleMetadataService;
    private readonly IAmazonMetadataService _amazonMetadataService;
    private readonly MetadataConverters _metadataConverters;
    private readonly SearchProgressReporter _searchProgressReporter;

    public AsinSearchHandler(
        ILogger<AsinSearchHandler> logger,
        IConfigurationService configurationService,
        AudimetaService audimetaService,
        IAudnexusService audnexusService,
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
    /// Searches for a specific ASIN using the following workflow:
    /// 1. Audimeta.de /book/{asin} endpoint (primary)
    /// 2. Audnexus fallback (if configured)
    /// 3. Amazon product page scraping (final fallback)
    /// Note: Audible scraping has been removed per new workflow requirements.
    /// </summary>
    public async Task<List<SearchResult>> SearchByAsinAsync(
        string asin,
        List<ApiConfiguration> metadataSources,
        CancellationToken ct = default)
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

        // Initialize metadata variables
        AudibleBookMetadata? metadata = null;
        string? metadataSourceName = null;

        // Step 1: Try to get metadata from Audimeta.de first (primary source)
        _logger.LogInformation("Attempting Audimeta.de for ASIN {Asin}", asin);
        await _searchProgressReporter.BroadcastAsync($"Searching Audimeta.de for {asin}", null);
        
        try
        {
            var audimetaData = await _audimetaService.GetBookMetadataAsync(asin, "us", true);
            if (audimetaData != null)
            {
                metadata = _metadataConverters.ConvertAudimetaToMetadata(audimetaData, asin, "Audible");
                metadataSourceName = "Audimeta";
                _logger.LogInformation("Successfully got metadata from Audimeta.de for ASIN {Asin}", asin);
            }
            else
            {
                _logger.LogInformation("Audimeta.de returned no data for ASIN {Asin}", asin);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to get metadata from Audimeta.de for ASIN {Asin}", asin);
        }

        // Step 2: If Audimeta failed, try other configured metadata sources (Audnexus)
        if (metadata == null && metadataSources != null && metadataSources.Any())
        {
            _logger.LogInformation("Trying {Count} fallback metadata source(s) for ASIN {Asin}", metadataSources.Count, asin);
            await _searchProgressReporter.BroadcastAsync($"Checking fallback metadata sources for {asin}", null);

            foreach (var source in metadataSources.OrderBy(s => s.Priority))
            {
                try
                {
                    if (source.Name.Contains("Audnexus", StringComparison.OrdinalIgnoreCase))
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

        // Step 3: If metadata sources failed, scrape Amazon product page as fallback (Audible scraping removed)
        if (metadata == null)
        {
            _logger.LogInformation("No metadata found from APIs for ASIN {Asin}, falling back to Amazon scraping", asin);
            await _searchProgressReporter.BroadcastAsync($"Metadata sources unavailable, scraping Amazon", null);

            // Try Amazon scraping only (Audible no longer used as fallback)
            if (!skipAmazon)
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

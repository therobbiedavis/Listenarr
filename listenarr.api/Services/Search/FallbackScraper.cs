using System.Collections.Concurrent;
using Listenarr.Api.Services.Search.Filters;
using Listenarr.Domain.Models;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services.Search;

/// <summary>
/// Handles last-resort product page scraping for ASINs that failed all metadata sources.
/// </summary>
public class FallbackScraper
{
    private readonly ILogger<FallbackScraper> _logger;
    private readonly IAmazonMetadataService _amazonMetadataService;
    private readonly MetadataConverters _metadataConverters;
    private readonly SearchResultFilterPipeline _filterPipeline;
    private readonly SearchProgressReporter _searchProgressReporter;

    public FallbackScraper(
        ILogger<FallbackScraper> logger,
        IAmazonMetadataService amazonMetadataService,
        MetadataConverters metadataConverters,
        SearchResultFilterPipeline filterPipeline,
        SearchProgressReporter searchProgressReporter)
    {
        _logger = logger;
        _amazonMetadataService = amazonMetadataService;
        _metadataConverters = metadataConverters;
        _filterPipeline = filterPipeline;
        _searchProgressReporter = searchProgressReporter;
    }

    /// <summary>
    /// Scrapes Amazon product pages for ASINs that failed all other metadata sources.
    /// </summary>
    public async Task<FallbackScrapeResult> ScrapeAsinsAsync(
        List<string> asinsNeedingFallback,
        List<SearchResult> alreadyEnrichedResults,
        ConcurrentDictionary<string, string> candidateDropReasons)
    {
        var scrapedResults = new ConcurrentBag<SearchResult>();

        var fallbackAsins = asinsNeedingFallback.Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Except(alreadyEnrichedResults.Select(e => e.Asin), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!fallbackAsins.Any())
        {
            return new FallbackScrapeResult(scrapedResults.ToList(), candidateDropReasons);
        }

        _logger.LogInformation("Attempting product-page scraping fallback for {Count} ASIN(s)", fallbackAsins.Count);

        var scrapeSemaphore = new SemaphoreSlim(5); // Increased from 3 to 5 for better throughput
        var scrapeTasks = fallbackAsins.Select(asin => Task.Run(async () =>
        {
            await scrapeSemaphore.WaitAsync();
            try
            {
                _logger.LogDebug("Scraping product page for ASIN {Asin}", asin);
                await _searchProgressReporter.BroadcastAsync($"Processing ASIN {asin}", asin);
                
                var metadata = await _amazonMetadataService.ScrapeAmazonMetadataAsync(asin!);
                
                if (metadata != null)
                {
                    // Create temporary result to test filters
                    var tempResult = new SearchResult 
                    { 
                        Title = metadata.Title!, 
                        Artist = metadata.Authors?.FirstOrDefault() ?? "" 
                    };
                    
                    // Apply filters to scraped metadata
                    if (_filterPipeline.WouldFilter(tempResult, out string? filterReason))
                    {
                        _logger.LogDebug("Filtering out scraped result: {Title} (ASIN: {Asin}) - Reason: {Reason}", 
                            metadata.Title, asin, filterReason);
                        
                        if (!string.IsNullOrWhiteSpace(asin))
                        {
                            try { candidateDropReasons[asin] = filterReason ?? "scrape_filtered"; } catch { }
                        }
                        return;
                    }

                    var enrichedResult = await _metadataConverters.ConvertMetadataToSearchResultAsync(
                        metadata, asin!, null, null, metadata.ImageUrl);
                    
                    enrichedResult.IsEnriched = true;
                    enrichedResult.MetadataSource = metadata.Source; // Set Amazon as metadata source
                    
                    scrapedResults.Add(enrichedResult);
                    
                    if (!string.IsNullOrWhiteSpace(asin))
                    {
                        try { candidateDropReasons[asin] = "scrape_enriched"; } catch { }
                    }
                    
                    _logger.LogInformation("Product-page scraping enriched ASIN {Asin} with title={Title}", 
                        asin, metadata.Title);
                    await _searchProgressReporter.BroadcastAsync($"Found: {metadata.Title}", asin);
                }
                else
                {
                    _logger.LogDebug("Product-page scraping returned no useful data for ASIN {Asin}", asin);
                    
                    if (!string.IsNullOrWhiteSpace(asin))
                    {
                        try { candidateDropReasons[asin] = "scrape_no_data"; } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Product-page scraping failed for ASIN {Asin}", asin);
                
                if (!string.IsNullOrWhiteSpace(asin))
                {
                    try { candidateDropReasons[asin] = "scrape_exception"; } catch { }
                }
            }
            finally
            {
                scrapeSemaphore.Release();
            }
        })).ToList();

        await Task.WhenAll(scrapeTasks);
        
        _logger.LogInformation("Scraping fallback complete. Scraped {Count} additional results from {Total} ASINs", 
            scrapedResults.Count, fallbackAsins.Count);

        return new FallbackScrapeResult(scrapedResults.ToList(), candidateDropReasons);
    }
}

/// <summary>
/// Result of fallback scraping operation.
/// </summary>
public record FallbackScrapeResult(
    List<SearchResult> ScrapedResults,
    ConcurrentDictionary<string, string> CandidateDropReasons);

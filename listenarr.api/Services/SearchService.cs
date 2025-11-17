/*
 * Listenarr - Audiobook Management System
 * Copyright (C) 2024-2025 Robbie Davis
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as published
 * by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Affero General Public License for more details.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>.
 */

using Listenarr.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace Listenarr.Api.Services
{
    public class SearchService : ISearchService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<SearchService> _logger;
    private readonly IAudibleMetadataService _audibleMetadataService;
        private readonly IOpenLibraryService _openLibraryService;
    private readonly IHubContext<DownloadHub> _hubContext;
        private readonly IAmazonSearchService _amazonSearchService;
        private readonly IAudibleSearchService _audibleSearchService;
        private readonly IImageCacheService _imageCacheService;
        private readonly ListenArrDbContext _dbContext;
        private readonly AudimetaService _audimetaService;
        private readonly AudnexusService _audnexusService;

        public SearchService(
            HttpClient httpClient, 
            IConfigurationService configurationService, 
            ILogger<SearchService> logger, 
            IAudibleMetadataService audibleMetadataService,
            IOpenLibraryService openLibraryService,
            IAmazonSearchService amazonSearchService,
            IAudibleSearchService audibleSearchService,
            IImageCacheService imageCacheService,
            ListenArrDbContext dbContext,
            IHubContext<DownloadHub> hubContext,
            AudimetaService audimetaService,
            AudnexusService audnexusService)
        {
            _httpClient = httpClient;
            _configurationService = configurationService;
            _logger = logger;
            _audibleMetadataService = audibleMetadataService;
            _openLibraryService = openLibraryService;
            _amazonSearchService = amazonSearchService;
            _audibleSearchService = audibleSearchService;
            _imageCacheService = imageCacheService;
            _dbContext = dbContext;
            _hubContext = hubContext;
            _audimetaService = audimetaService;
            _audnexusService = audnexusService;
        }

        private async Task BroadcastSearchProgressAsync(string message, string? asin = null)
        {
            try
            {
                if (_hubContext != null)
                {
                    await _hubContext.Clients.All.SendAsync("SearchProgress", new { message, asin });
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to broadcast SearchProgress: {Message}", ex.Message);
            }
        }

        public async Task<List<SearchResult>> SearchAsync(string query, string? category = null, List<string>? apiIds = null, SearchSortBy sortBy = SearchSortBy.Seeders, SearchSortDirection sortDirection = SearchSortDirection.Descending, bool isAutomaticSearch = false)
        {
            var results = new List<SearchResult>();

            // Diagnostic log to help trace which callers invoke automatic vs interactive searches
            _logger.LogInformation("SearchAsync called. Query='{Query}', isAutomaticSearch={IsAutomaticSearch}", query, isAutomaticSearch);

            // For automatic search, only search indexers - skip Amazon/Audible entirely
            if (isAutomaticSearch)
            {
                var automaticIndexerResults = await SearchIndexersAsync(query, category, sortBy, sortDirection, isAutomaticSearch);
                if (automaticIndexerResults.Any())
                {
                    results.AddRange(automaticIndexerResults);
                    _logger.LogInformation("Found {Count} indexer results for automatic search query: {Query}", automaticIndexerResults.Count, query);
                }
                else
                {
                    _logger.LogInformation("No indexer results found for automatic search query: {Query}", query);
                }
                return ApplySorting(results, sortBy, sortDirection);
            }

            // For manual/interactive search, use intelligent search (Amazon/Audible) + indexers
            var intelligentResults = await IntelligentSearchAsync(query);
            if (intelligentResults.Any())
            {
                results.AddRange(intelligentResults);
                _logger.LogInformation("Found {Count} valid Amazon/Audible results using intelligent search for query: {Query}", intelligentResults.Count, query);
            }
            else
            {
                _logger.LogInformation("No valid Amazon/Audible results found for query: {Query}; falling back to raw search conversions", query);
                // Return raw Amazon/Audible converted search results (IsEnriched = false)
                // This is preferable to mock placeholder data.
                var amazonResults = await _amazonSearchService.SearchAudiobooksAsync(query);
                var audibleResults = await _audibleSearchService.SearchAudiobooksAsync(query);
                var fallback = new List<SearchResult>();
                foreach (var a in amazonResults.Take(12))
                {
                    var r = ConvertAmazonSearchToResult(a);
                    r.IsEnriched = false;
                    fallback.Add(r);
                }
                foreach (var a in audibleResults.Take(12))
                {
                    var r = ConvertAudibleSearchToResult(a);
                    r.IsEnriched = false;
                    fallback.Add(r);
                }
                _logger.LogInformation("Returning {Count} raw-conversion fallback results for query: {Query}", fallback.Count, query);
                return ApplySorting(fallback, sortBy, sortDirection);
            }

            // Also search configured indexers for additional results (including DDL downloads)
            var indexerResults = await SearchIndexersAsync(query, category, sortBy, sortDirection, isAutomaticSearch);
            if (indexerResults.Any())
            {
                results.AddRange(indexerResults);
                _logger.LogInformation("Added {Count} indexer results (including DDL downloads) for query: {Query}", indexerResults.Count, query);
            }

            return ApplySorting(results, sortBy, sortDirection);
        }

        private List<SearchResult> ApplySorting(List<SearchResult> results, SearchSortBy sortBy, SearchSortDirection sortDirection)
        {
            if (!results.Any())
                return results;

            IOrderedEnumerable<SearchResult> orderedResults;

            // Primary sort
            switch (sortBy)
            {
                case SearchSortBy.Seeders:
                    orderedResults = sortDirection == SearchSortDirection.Descending
                        ? results.OrderByDescending(r => r.Seeders)
                        : results.OrderBy(r => r.Seeders);
                    break;

                case SearchSortBy.Size:
                    orderedResults = sortDirection == SearchSortDirection.Descending
                        ? results.OrderByDescending(r => r.Size)
                        : results.OrderBy(r => r.Size);
                    break;

                case SearchSortBy.PublishedDate:
                    orderedResults = sortDirection == SearchSortDirection.Descending
                        ? results.OrderByDescending(r => r.PublishedDate)
                        : results.OrderBy(r => r.PublishedDate);
                    break;

                case SearchSortBy.Title:
                    orderedResults = sortDirection == SearchSortDirection.Descending
                        ? results.OrderByDescending(r => r.Title, StringComparer.OrdinalIgnoreCase)
                        : results.OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase);
                    break;

                case SearchSortBy.Source:
                    orderedResults = sortDirection == SearchSortDirection.Descending
                        ? results.OrderByDescending(r => r.Source, StringComparer.OrdinalIgnoreCase)
                        : results.OrderBy(r => r.Source, StringComparer.OrdinalIgnoreCase);
                    break;

                case SearchSortBy.Quality:
                    orderedResults = sortDirection == SearchSortDirection.Descending
                        ? results.OrderByDescending(r => GetQualityScore(r.Quality))
                        : results.OrderBy(r => GetQualityScore(r.Quality));
                    break;

                default:
                    // Default to seeders descending
                    orderedResults = results.OrderByDescending(r => r.Seeders);
                    break;
            }

            return orderedResults.ToList();
        }

        private int GetQualityScore(string? quality)
        {
            if (string.IsNullOrEmpty(quality))
                return 0;

            var lowerQuality = quality.ToLower();

            // Highest quality
            if (lowerQuality.Contains("flac"))
                return 100;

            // Audible format (AAX) - high quality
            if (lowerQuality.Contains("aax"))
                return 95;

            // Container formats
            if (lowerQuality.Contains("m4b"))
                return 90;

            // Modern efficient codecs
            if (lowerQuality.Contains("opus"))
                return 85;

            // VBR quality presets (LAME VBR presets like V0/V1/V2)
            if (lowerQuality.Contains("v0") || lowerQuality.Contains("-v0") || lowerQuality.Contains(" v0"))
                return 82;
            if (lowerQuality.Contains("v1") || lowerQuality.Contains("-v1") || lowerQuality.Contains(" v1"))
                return 76;
            if (lowerQuality.Contains("v2") || lowerQuality.Contains("-v2") || lowerQuality.Contains(" v2"))
                return 70;


            // AAC / M4A (check before numeric bitrates to prefer codec score for e.g. "AAC 256")
            if (lowerQuality.Contains("aac") || lowerQuality.Contains("m4a"))
                return 78;

            // Explicit numeric bitrates
            if (lowerQuality.Contains("320"))
                return 80;
            if (lowerQuality.Contains("256"))
                return 74;
            if (lowerQuality.Contains("192"))
                return 60;

            // VBR / CBR generic tokens (treat as mid-range if no numeric bitrate provided)
            if (lowerQuality.Contains("vbr") || lowerQuality.Contains("cbr"))
            {
                // If there's an explicit numeric bitrate elsewhere, that will have matched above.
                return 65;
            }

            // Generic MP3 mention without explicit bitrate -> mid-range
            if (lowerQuality.Contains("mp3") && !lowerQuality.Contains("64") && !lowerQuality.Contains("128") && !lowerQuality.Contains("192") && !lowerQuality.Contains("256") && !lowerQuality.Contains("320"))
                return 65;

            if (lowerQuality.Contains("128"))
                return 50;
            if (lowerQuality.Contains("64"))
                return 40;

            return 0;
        }

        public async Task<List<SearchResult>> SearchIndexersAsync(string query, string? category = null, SearchSortBy sortBy = SearchSortBy.Seeders, SearchSortDirection sortDirection = SearchSortDirection.Descending, bool isAutomaticSearch = false)
        {
            var results = new List<SearchResult>();
            var indexers = await _dbContext.Indexers
                .Where(i => i.IsEnabled && (isAutomaticSearch ? i.EnableAutomaticSearch : i.EnableInteractiveSearch))
                .OrderBy(i => i.Priority)
                .ToListAsync();

            _logger.LogInformation("Searching {Count} enabled indexers for query: {Query}", indexers.Count, query);

            // If no indexers are configured, return mock data for development
            if (!indexers.Any())
            {
                _logger.LogWarning("No indexers configured, returning mock results for query: {Query}", query);
                return GenerateMockIndexerResults(query);
            }

            // Search all enabled indexers in parallel
            var searchTasks = indexers.Select(async indexer =>
            {
                try
                {
                    _logger.LogInformation("Searching indexer {Name} ({Type}) for query: {Query}", indexer.Name, indexer.Type, query);
                    var indexerResults = await SearchIndexerAsync(indexer, query, category);
                    _logger.LogInformation("Found {Count} results from indexer {Name}", indexerResults.Count, indexer.Name);
                    return indexerResults;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error searching indexer {Name} for query: {Query}", indexer.Name, query);
                    return new List<SearchResult>();
                }
            }).ToList();

            var indexerResults = await Task.WhenAll(searchTasks);
            
            // Flatten all results
            foreach (var indexerResult in indexerResults)
            {
                results.AddRange(indexerResult);
            }

            _logger.LogInformation("Total {Count} results from all indexers for query: {Query}", results.Count, query);
            
            // Sort by seeders (descending) then by date
            return results.OrderByDescending(r => r.Seeders).ThenByDescending(r => r.PublishedDate).ToList();
        }

    public async Task<List<SearchResult>> IntelligentSearchAsync(string query)
        {
            var results = new List<SearchResult>();

            try
            {
                _logger.LogInformation("Starting intelligent search for: {Query}", query);

                // Step 1: Parallel search Amazon & Audible
                _logger.LogInformation("Searching for: {Query}", query);
                await BroadcastSearchProgressAsync($"Searching for {query}", null);

                // Detect if the query is an ISBN (digits only after cleaning). If so, skip Audible
                var digitsOnly = new string((query ?? string.Empty).Where(char.IsDigit).ToArray());
                var isIsbnQuery = digitsOnly.Length == 10 || digitsOnly.Length == 13;

                List<AmazonSearchResult> amazonResults = new();
                List<AudibleSearchResult> audibleResults = new();

                if (isIsbnQuery)
                {
                    _logger.LogInformation("IntelligentSearch detected ISBN-only query; skipping Audible search for: {Query}", query);
                    if (!string.IsNullOrEmpty(query))
                    {
                        var amazonTask = _amazonSearchService.SearchAudiobooksAsync(query);
                        amazonResults = await amazonTask;
                    }
                    audibleResults = new List<AudibleSearchResult>();
                    _logger.LogInformation("Collected {AmazonCount} Amazon raw results and skipped Audible for ISBN query", amazonResults.Count);
                }
                else
                {
                    if (!string.IsNullOrEmpty(query))
                    {
                        var amazonTask = _amazonSearchService.SearchAudiobooksAsync(query);
                        var audibleTask = _audibleSearchService.SearchAudiobooksAsync(query);
                        amazonResults = await amazonTask;
                        audibleResults = await audibleTask;
                    }
                    _logger.LogInformation("Collected {AmazonCount} Amazon raw results and {AudibleCount} Audible raw results", amazonResults.Count, audibleResults.Count);
                }

                // Step 2: Build a unified ASIN candidate set (Amazon priority, then Audible)
                // Also create a lookup map for fallback titles and full search result objects
                var asinCandidates = new List<string>();
                var asinToRawResult = new Dictionary<string, (string? Title, string? Author, string? ImageUrl)>(StringComparer.OrdinalIgnoreCase);
                var asinToAudibleResult = new Dictionary<string, AudibleSearchResult>(StringComparer.OrdinalIgnoreCase);
                var asinToSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var a in amazonResults.Where(a => !string.IsNullOrEmpty(a.Asin) && IsValidAsin(a.Asin!)))
                {
                    asinCandidates.Add(a.Asin!);
                    asinToRawResult[a.Asin!] = (a.Title, a.Author, a.ImageUrl);
                    asinToSource[a.Asin!] = "Amazon";
                }
                
                foreach (var a in audibleResults.Where(a => !string.IsNullOrEmpty(a.Asin) && IsValidAsin(a.Asin!)))
                {
                    if (asinToRawResult.TryAdd(a.Asin!, (a.Title, a.Author, a.ImageUrl)))
                    {
                        asinCandidates.Add(a.Asin!);
                        asinToAudibleResult[a.Asin!] = a;  // Store full Audible search result
                        asinToSource[a.Asin!] = "Audible";
                    }
                }
                
                asinCandidates = asinCandidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
                _logger.LogInformation("Unified ASIN candidate list size: {Count}", asinCandidates.Count);
                await BroadcastSearchProgressAsync($"Found {asinCandidates.Count} ASIN candidates", null);

                // If we don't have any ASIN candidates, fall back to returning raw search results
                // (converted) so the API still returns usable items instead of an empty list.
                if (!asinCandidates.Any())
                {
                    _logger.LogInformation("No ASIN candidates found; returning raw Amazon/Audible search results as fallback");
                    var fallback = new List<SearchResult>();
                    foreach (var a in amazonResults.Take(12))
                    {
                        var r = ConvertAmazonSearchToResult(a);
                        r.IsEnriched = false;
                        fallback.Add(r);
                    }
                    foreach (var a in audibleResults.Take(12))
                    {
                        var r = ConvertAudibleSearchToResult(a);
                        r.IsEnriched = false;
                        fallback.Add(r);
                    }
                    return fallback.OrderByDescending(r => r.Seeders).ThenBy(r => r.Size).ToList();
                }

                // Step 3: Get enabled metadata sources ONCE before concurrent enrichment to avoid DbContext threading issues
                _logger.LogInformation("Fetching enabled metadata sources before concurrent enrichment...");
                var metadataSources = await GetEnabledMetadataSourcesAsync();
                _logger.LogInformation("Will use {Count} metadata source(s) for all ASINs", metadataSources.Count);

                // Step 4: Enrich each ASIN with detailed metadata concurrently (limit concurrency)
                var semaphore = new SemaphoreSlim(3); // throttle external fetches
                var enrichmentTasks = new List<Task>();
                var enriched = new ConcurrentBag<SearchResult>();

                foreach (var asin in asinCandidates)
                {
                    enrichmentTasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            _logger.LogDebug("Enriching ASIN {Asin}", asin);
                            await BroadcastSearchProgressAsync($"Enriching ASIN: {asin}", asin);
                            
                            // Get the original search results
                            asinToRawResult.TryGetValue(asin, out var rawResult);
                            asinToAudibleResult.TryGetValue(asin, out var audibleResult);
                            asinToSource.TryGetValue(asin, out var originalSource);
                            
                            AudibleBookMetadata? metadata = null;
                            string? metadataSourceName = null;
                            
                            // Use the pre-fetched metadata sources (avoid DbContext concurrency issues)
                            if (metadataSources.Count > 0)
                            {
                                _logger.LogInformation("Attempting to fetch metadata for ASIN {Asin} from {Count} configured source(s): {Sources}", 
                                    asin, metadataSources.Count, string.Join(", ", metadataSources.Select(s => s.Name)));
                            }
                            
                            // Try each metadata source in priority order until one succeeds
                            foreach (var source in metadataSources)
                            {
                                try
                                {
                                    _logger.LogInformation("Attempting to fetch metadata from {SourceName} ({BaseUrl}) for ASIN {Asin}", source.Name, source.BaseUrl, asin);
                                    await BroadcastSearchProgressAsync($"Fetching metadata from {source.Name} for ASIN: {asin}", asin);
                                    
                                    if (source.BaseUrl.Contains("audimeta.de", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.LogDebug("Calling Audimeta service for ASIN {Asin}", asin);
                                        var audimetaData = await _audimetaService.GetBookMetadataAsync(asin, "us", true);
                                        
                                        if (audimetaData != null)
                                        {
                                            _logger.LogInformation("✓ Audimeta returned data for ASIN {Asin}. Title: {Title}", asin, audimetaData.Title ?? "null");
                                            metadata = ConvertAudimetaToMetadata(audimetaData, asin, originalSource ?? "Audible");
                                            metadataSourceName = source.Name; // Store which metadata source succeeded
                                            _logger.LogInformation("Successfully enriched ASIN {Asin} with metadata from {SourceName}", asin, source.Name);
                                            break; // Success! Stop trying other sources
                                        }
                                        else
                                        {
                                            _logger.LogWarning("✗ Audimeta returned null for ASIN {Asin}", asin);
                                        }
                                    }
                                    else if (source.BaseUrl.Contains("audnex.us", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.LogDebug("Calling Audnexus service for ASIN {Asin}", asin);
                                        var audnexusData = await _audnexusService.GetBookMetadataAsync(asin, "us", true, false);
                                        
                                        if (audnexusData != null)
                                        {
                                            _logger.LogInformation("✓ Audnexus returned data for ASIN {Asin}. Title: {Title}", asin, audnexusData.Title ?? "null");
                                            metadata = ConvertAudnexusToMetadata(audnexusData, asin, originalSource ?? "Audible");
                                            metadataSourceName = source.Name; // Store which metadata source succeeded
                                            _logger.LogInformation("Successfully enriched ASIN {Asin} with metadata from {SourceName}", asin, source.Name);
                                            break; // Success! Stop trying other sources
                                        }
                                        else
                                        {
                                            _logger.LogWarning("✗ Audnexus returned null for ASIN {Asin}", asin);
                                        }
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Unknown metadata source type: {BaseUrl}, skipping", source.BaseUrl);
                                    }
                                }
                                catch (Exception sourceEx)
                                {
                                    _logger.LogWarning(sourceEx, "Failed to fetch metadata from {SourceName} for ASIN {Asin}, trying next source", source.Name, asin);
                                    continue; // Try next metadata source
                                }
                            }
                            
                            // If all metadata sources failed, fall back to scraping
                            if (metadata == null)
                            {
                                if (metadataSources.Count == 0)
                                {
                                    _logger.LogInformation("⚠ No metadata sources configured for ASIN {Asin}, falling back to scraping", asin);
                                    await BroadcastSearchProgressAsync($"No metadata sources configured, scraping for ASIN: {asin}", asin);
                                }
                                else
                                {
                                    _logger.LogWarning("⚠ All {Count} metadata sources failed for ASIN {Asin} (tried: {Sources}), falling back to scraping", 
                                        metadataSources.Count, asin, string.Join(", ", metadataSources.Select(s => s.Name)));
                                    await BroadcastSearchProgressAsync($"Metadata sources failed, scraping for ASIN: {asin}", asin);
                                }
                                
                                _logger.LogInformation("Attempting to scrape metadata for ASIN {Asin}", asin);
                                metadata = await _audibleMetadataService.ScrapeAudibleMetadataAsync(asin);
                                
                                if (metadata != null)
                                {
                                    _logger.LogInformation("✓ Successfully scraped metadata for ASIN {Asin}. Title: {Title}, Source: {Source}", 
                                        asin, metadata.Title ?? "null", metadata.Source ?? "null");
                                    
                                    // Keep the original source for the product link (Amazon/Audible where ASIN was found)
                                    var scrapedFrom = metadata.Source; // This is set by the scraper based on which site worked
                                    metadata.Source = originalSource ?? "Audible";
                                    
                                    // Set metadata source to the site that was scraped (without "(Scraped)" suffix)
                                    metadataSourceName = scrapedFrom;
                                    _logger.LogInformation("Scraped metadata from {ScrapedFrom}, setting MetadataSource badge to: {MetadataSource}", scrapedFrom, metadataSourceName);
                                }
                                else
                                {
                                    _logger.LogWarning("✗ Failed to scrape metadata for ASIN {Asin}", asin);
                                }
                            }
                            
                            // If we have an Audible search result, populate/merge that data first
                            if (audibleResult != null && metadata != null)
                            {
                                var searchMetadata = PopulateMetadataFromSearchResult(audibleResult);
                                MergeMetadata(searchMetadata, metadata);
                                metadata = searchMetadata;
                            }
                            
                            if (metadata != null)
                            {
                                // Accept metadata even if Title is missing. ConvertMetadataToSearchResult
                                // will use raw result title as fallback if metadata title is empty.
                                var enrichedResult = await ConvertMetadataToSearchResultAsync(metadata, asin, rawResult.Title, rawResult.Author, rawResult.ImageUrl);
                                enrichedResult.IsEnriched = true;
                                
                                // Store the metadata source name for the badge
                                if (!string.IsNullOrEmpty(metadataSourceName))
                                {
                                    enrichedResult.MetadataSource = metadataSourceName;
                                    _logger.LogInformation("✓ Enriched result for ASIN {Asin} - Title: {Title}, MetadataSource: {MetadataSource}", 
                                        asin, enrichedResult.Title ?? "null", metadataSourceName);
                                }
                                else
                                {
                                    _logger.LogWarning("⚠ Metadata obtained for ASIN {Asin} but metadataSourceName is null/empty", asin);
                                }
                                
                                enriched.Add(enrichedResult);
                            }
                            else
                            {
                                _logger.LogWarning("✗ No metadata obtained for ASIN {Asin} after trying all sources and scraping", asin);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Metadata enrichment failed for ASIN {Asin}", asin);
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }
                await Task.WhenAll(enrichmentTasks);

                // Only return enriched items (metadata success); skip basic fallbacks entirely
                results.AddRange(enriched);
                await BroadcastSearchProgressAsync($"Enrichment complete. Returning {results.Count} results", null);

                // If still no enriched results, try OpenLibrary derived titles to attempt enrichment again
                if (!results.Any())
                {
                    _logger.LogInformation("No Amazon or Audible results found, trying OpenLibrary for title variations");
                    if (!string.IsNullOrEmpty(query))
                    {
                        var books = await _openLibraryService.SearchBooksAsync(query, null, 5);
                        
                        foreach (var book in books.Docs.Take(3))
                        {
                            if (!string.IsNullOrEmpty(book.Title) && book.Title != query)
                            {
                                _logger.LogInformation("Trying Amazon search with OpenLibrary title: {Title}", book.Title);
                                var altResults = await _amazonSearchService.SearchAudiobooksAsync(book.Title);
                                
                                foreach (var altResult in altResults.Take(2))
                                {
                                    if (!string.IsNullOrEmpty(altResult.Asin))
                                    {
                                        try
                                        {
                                            await BroadcastSearchProgressAsync($"Attempting metadata fetch for alternate ASIN: {altResult.Asin}", altResult.Asin);
                                            
                                            // Try audimeta first
                                            var audimetaData = await _audimetaService.GetBookMetadataAsync(altResult.Asin, "us", true);
                                            AudibleBookMetadata? metadata = null;
                                            
                                            if (audimetaData != null)
                                            {
                                                metadata = ConvertAudimetaToMetadata(audimetaData, altResult.Asin, "Amazon");
                                            }
                                            else
                                            {
                                                // Fallback to scraping
                                                metadata = await _audibleMetadataService.ScrapeAudibleMetadataAsync(altResult.Asin);
                                                if (metadata != null)
                                                {
                                                    metadata.Source = "Amazon";
                                                }
                                            }
                                            
                                            if (metadata != null && !string.IsNullOrEmpty(metadata.Title))
                                            {
                                                var searchResult = await ConvertMetadataToSearchResultAsync(metadata, altResult.Asin);
                                                searchResult.IsEnriched = true;
                                                results.Add(searchResult);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogWarning(ex, "Failed to get metadata for alternative ASIN: {Asin}", altResult.Asin);
                                        }
                                    }
                                }
                            }
                            
                            if (results.Any()) break; // Stop if we found results
                        }
                    }
                }

                // Final filter: Remove any results with problematic titles
                results = results.Where(r => 
                    !string.IsNullOrWhiteSpace(r.Title) && 
                    !IsTitleNoise(r.Title) &&
                    r.Title.Length >= 3
                ).ToList();

                // Sort results: Prioritize proper metadata sources over scraped sources
                // Proper metadata sources (Audimeta, Audnexus, etc.) should be at the top
                // Scraped sources (Amazon, Audible) should be at the bottom
                results = results.OrderByDescending(r =>
                {
                    // Check if this result has a proper metadata source (not scraped)
                    if (string.IsNullOrEmpty(r.MetadataSource))
                        return 0; // No metadata source = lowest priority
                    
                    var metadataSource = r.MetadataSource.ToLowerInvariant();
                    
                    // Scraped sources get lower priority
                    if (metadataSource == "amazon" || metadataSource == "audible")
                        return 1; // Scraped = medium priority
                    
                    // Proper metadata sources get highest priority
                    return 2; // Audimeta, Audnexus, etc. = highest priority
                }).ToList();

                _logger.LogInformation("Intelligent search complete. Returning {Count} filtered and sorted results for query: {Query}", results.Count, query);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during intelligent search for query: {Query}", query);
                return results;
            }
        }

        private static bool IsValidAsin(string asin)
        {
            if (string.IsNullOrEmpty(asin))
                return false;
                
            // Amazon ASIN format: 10 characters, typically starting with B0 or digits
            return asin.Length == 10 && 
                   (asin.StartsWith("B0") || char.IsDigit(asin[0])) &&
                   asin.All(c => char.IsLetterOrDigit(c));
        }

        private static bool IsTitleNoise(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return true;
            
            var t = title.Trim();
            
            // Common noise phrases that appear in search results
            string[] noisePhrases = new[]
            {
                "No results", "Suggested Searches", "No results found", "Try again",
                "Browse categories", "Customer Service", "Help", "Search", "Menu",
                "Sign in", "Account", "Audible.com", "Language", "Currency"
            };
            
            // Check if title contains any noise phrases
            if (noisePhrases.Any(p => t.IndexOf(p, StringComparison.OrdinalIgnoreCase) >= 0))
                return true;
            
            // Check if title is mostly whitespace/newlines
            if (t.All(c => char.IsWhiteSpace(c) || c == '\n' || c == '\r'))
                return true;
            
            // Check for excessive newlines (typical of scraped navigation elements)
            if (t.Count(c => c == '\n') > 2)
                return true;
            
            return false;
        }

        private static bool IsAuthorNoise(string? author)
        {
            if (string.IsNullOrWhiteSpace(author)) return true;
            var a = author.Trim();
            // Filter out common header/navigation noise in author fields
            if (a.Length < 2) return true;
            if (a.Equals("Authors", StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Equals("By:", StringComparison.OrdinalIgnoreCase)) return true;
            if (a.StartsWith("Sort by", StringComparison.OrdinalIgnoreCase)) return true;
            if (a.Contains("English - USD", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Populate metadata from Audible search result data (has runtime, series, etc.)
        /// </summary>
        private AudibleBookMetadata PopulateMetadataFromSearchResult(AudibleSearchResult? searchResult)
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
            metadata.Series = searchResult.Series;
            metadata.SeriesNumber = searchResult.SeriesNumber;
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
                    _logger.LogInformation("Parsed publish year from search result: {PublishYear}", metadata.PublishYear);
                }
            }

            return metadata;
        }

        private AudibleBookMetadata ConvertAudimetaToMetadata(AudimetaBookResponse audimetaData, string asin, string source = "Audible")
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

        private AudibleBookMetadata ConvertAudnexusToMetadata(AudnexusBookResponse audnexusData, string asin, string source = "Audible")
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
        /// Merge scraped metadata into search result metadata (scraped data takes priority except for fields search results do better)
        /// </summary>
        private void MergeMetadata(AudibleBookMetadata target, AudibleBookMetadata scraped)
        {
            // Scraped data takes priority for these fields
            if (!string.IsNullOrEmpty(scraped.Source))
                target.Source = scraped.Source; // Source is based on actual URL, so scraped wins
            if (!string.IsNullOrEmpty(scraped.Title))
                target.Title = scraped.Title;
            if (scraped.Authors != null && scraped.Authors.Any())
                target.Authors = scraped.Authors;
            if (scraped.Narrators != null && scraped.Narrators.Any())
                target.Narrators = scraped.Narrators;
            if (!string.IsNullOrEmpty(scraped.ImageUrl))
            {
                _logger.LogDebug("Using image from metadata source ({Source}): {ImageUrl}", scraped.Source ?? "Unknown", scraped.ImageUrl);
                target.ImageUrl = scraped.ImageUrl;
            }
            if (!string.IsNullOrEmpty(scraped.Description))
                target.Description = scraped.Description;
            if (!string.IsNullOrEmpty(scraped.Publisher))
                target.Publisher = scraped.Publisher;
            if (!string.IsNullOrEmpty(scraped.Language))
                target.Language = scraped.Language;
            if (!string.IsNullOrEmpty(scraped.Version))
                target.Version = scraped.Version;
            if (scraped.Genres != null && scraped.Genres.Any())
                target.Genres = scraped.Genres;

            // Keep search result data if not found in scraped data
            // (Search results have runtime, series, release date that product pages often don't)
            if (target.Runtime == null && scraped.Runtime != null)
                target.Runtime = scraped.Runtime;
            if (string.IsNullOrEmpty(target.Series) && !string.IsNullOrEmpty(scraped.Series))
                target.Series = scraped.Series;
            if (string.IsNullOrEmpty(target.SeriesNumber) && !string.IsNullOrEmpty(scraped.SeriesNumber))
                target.SeriesNumber = scraped.SeriesNumber;
            if (string.IsNullOrEmpty(target.PublishYear) && !string.IsNullOrEmpty(scraped.PublishYear))
                target.PublishYear = scraped.PublishYear;
        }

        private Task<SearchResult> ConvertMetadataToSearchResultAsync(AudibleBookMetadata metadata, string asin, string? fallbackTitle = null, string? fallbackAuthor = null, string? fallbackImageUrl = null)
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
            if (IsAuthorNoise(author))
            {
                author = fallbackAuthor;
            }
            if (IsAuthorNoise(author))
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

        private SearchResult ConvertAmazonSearchToResult(AmazonSearchResult amazonResult)
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

        private SearchResult ConvertAudibleSearchToResult(AudibleSearchResult audibleResult)
        {
            return new SearchResult
            {
                Id = Guid.NewGuid().ToString(),
                Title = audibleResult.Title ?? "Unknown Title",
                Artist = audibleResult.Author ?? "Unknown Author",
                Album = audibleResult.Title ?? "Unknown Album",
                Category = "Audiobook",
                Size = 0,
                Seeders = 0,
                Leechers = 0,
                MagnetLink = $"audible://asin/{audibleResult.Asin}",
                Source = "Audible",
                PublishedDate = DateTime.MinValue,
                Quality = "Unknown",
                Format = "Audiobook",
                Description = null,
                Publisher = null,
                Language = null,
                Runtime = ParseDuration(audibleResult.Duration),
                Narrator = audibleResult.Narrator,
                ImageUrl = audibleResult.ImageUrl,
                Asin = audibleResult.Asin
            };
        }

        private int? ParseDuration(string? duration)
        {
            if (string.IsNullOrEmpty(duration)) return null;
            
            try
            {
                // Try to extract hours and minutes from duration string
                var hoursMatch = System.Text.RegularExpressions.Regex.Match(duration, @"(\d+)\s*hrs?");
                var minutesMatch = System.Text.RegularExpressions.Regex.Match(duration, @"(\d+)\s*mins?");
                
                int totalMinutes = 0;
                
                if (hoursMatch.Success)
                {
                    totalMinutes += int.Parse(hoursMatch.Groups[1].Value) * 60;
                }
                
                if (minutesMatch.Success)
                {
                    totalMinutes += int.Parse(minutesMatch.Groups[1].Value);
                }
                
                return totalMinutes > 0 ? totalMinutes : null;
            }
            catch
            {
                return null;
            }
        }

        private async Task<List<SearchResult>> TraditionalSearchAsync(string query, string? category = null, List<string>? apiIds = null)
        {
            var results = new List<SearchResult>();
            var apis = await _configurationService.GetApiConfigurationsAsync();
            
            if (apiIds != null && apiIds.Any())
            {
                apis = apis.Where(a => apiIds.Contains(a.Id)).ToList();
            }

            var enabledApis = apis.Where(a => a.IsEnabled).OrderBy(a => a.Priority).ToList();

            var searchTasks = enabledApis.Select(api => SearchByApiAsync(api.Id, query, category));
            var apiResults = await Task.WhenAll(searchTasks);

            foreach (var apiResult in apiResults)
            {
                foreach (var result in apiResult)
                {
                    // Try to enrich with Audible metadata if ASIN is present
                    if (!string.IsNullOrEmpty(result.MagnetLink)) // Replace with ASIN property if available
                    {
                        try
                        {
                            // Example: extract ASIN from MagnetLink or other property
                            var asin = ExtractAsin(result.MagnetLink);
                            if (!string.IsNullOrEmpty(asin))
                            {
                                var metadata = await _audibleMetadataService.ScrapeAudibleMetadataAsync(asin);
                                result.Title = metadata.Title ?? result.Title;
                                result.Artist = metadata.Authors?.FirstOrDefault() ?? result.Artist;
                                result.Album = metadata.Series ?? result.Album;
                                result.Category = string.Join(", ", metadata.Genres ?? new List<string>());
                                // Add more fields as needed
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning($"Failed to enrich result with Audible metadata: {ex.Message}");
                        }
                    }
                    results.Add(result);
                }
            }

            return results;
        }

        private string ExtractAsin(string magnetLink)
        {
            // TODO: Implement ASIN extraction logic from magnet/torrent/nzb or other property
            // For now, return empty string
            return string.Empty;
        }

        public async Task<List<SearchResult>> SearchByApiAsync(string apiId, string query, string? category = null)
        {
            try
            {
                Indexer? indexer = null;

                // Try parsing apiId as numeric indexer ID first
                if (int.TryParse(apiId, out var indexerId))
                {
                    indexer = await _dbContext.Indexers.FindAsync(indexerId);
                }
                else
                {
                    // If not numeric, try to find an indexer by name (case-insensitive)
                    indexer = await _dbContext.Indexers
                        .Where(i => i.Name.Equals(apiId, StringComparison.OrdinalIgnoreCase))
                        .FirstOrDefaultAsync();
                }

                if (indexer == null)
                {
                    _logger.LogWarning("Indexer not found for apiId: {ApiId}", apiId);
                    return new List<SearchResult>();
                }

                if (!indexer.IsEnabled)
                {
                    _logger.LogWarning("Indexer {IndexerName} (apiId: {ApiId}) is not enabled", indexer.Name, apiId);
                    return new List<SearchResult>();
                }

                // Search using the indexer
                return await SearchIndexerAsync(indexer, query, category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching indexer {apiId} for query: {query}");
                return new List<SearchResult>();
            }
        }

        public async Task<bool> TestApiConnectionAsync(string apiId)
        {
            try
            {
                var apiConfig = await _configurationService.GetApiConfigurationAsync(apiId);
                if (apiConfig == null) return false;

                // Test connection to the API
                var response = await _httpClient.GetAsync(apiConfig.BaseUrl);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error testing API connection for {apiId}");
                return false;
            }
        }

        private async Task<List<SearchResult>> SearchIndexerAsync(Indexer indexer, string query, string? category = null)
        {
            try
            {
                _logger.LogInformation("Searching indexer {Name} ({Implementation}) for: {Query}", indexer.Name, indexer.Implementation, query);

                // Route to appropriate search method based on implementation
                if (indexer.Implementation.Equals("InternetArchive", StringComparison.OrdinalIgnoreCase))
                {
                    return await SearchInternetArchiveAsync(indexer, query, category);
                }
                else if (indexer.Implementation.Equals("MyAnonamouse", StringComparison.OrdinalIgnoreCase))
                {
                    return await SearchMyAnonamouseAsync(indexer, query, category);
                }
                else
                {
                    return await SearchTorznabNewznabAsync(indexer, query, category);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching indexer {Name}", indexer.Name);
                return new List<SearchResult>();
            }
        }

        private async Task<List<SearchResult>> SearchTorznabNewznabAsync(Indexer indexer, string query, string? category)
        {
            try
            {
                // Build Torznab/Newznab API URL
                var url = BuildTorznabUrl(indexer, query, category);
                _logger.LogDebug("Indexer API URL: {Url}", url);

                // Make HTTP request
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Indexer {Name} returned status {Status}", indexer.Name, response.StatusCode);
                    return new List<SearchResult>();
                }

                var xmlContent = await response.Content.ReadAsStringAsync();
                
                // Parse Torznab/Newznab XML response
                var results = ParseTorznabResponse(xmlContent, indexer);
                
                _logger.LogInformation("Indexer {Name} returned {Count} results", indexer.Name, results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Torznab/Newznab indexer {Name}", indexer.Name);
                return new List<SearchResult>();
            }
        }

        private async Task<List<SearchResult>> SearchMyAnonamouseAsync(Indexer indexer, string query, string? category)
        {
            try
            {
                _logger.LogInformation("Searching MyAnonamouse for: {Query}", query);

                // Parse username and password from AdditionalSettings
                var username = "";
                var password = "";
                
                if (!string.IsNullOrEmpty(indexer.AdditionalSettings))
                {
                    try
                    {
                        var settings = JsonDocument.Parse(indexer.AdditionalSettings);
                        if (settings.RootElement.TryGetProperty("username", out var userElem))
                            username = userElem.GetString() ?? "";
                        if (settings.RootElement.TryGetProperty("password", out var passElem))
                            password = passElem.GetString() ?? "";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse MyAnonamouse settings");
                        return new List<SearchResult>();
                    }
                }

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    _logger.LogWarning("MyAnonamouse indexer {Name} missing username or password", indexer.Name);
                    return new List<SearchResult>();
                }

                // Build MyAnonamouse API request
                var url = $"{indexer.Url.TrimEnd('/')}/tor/js/loadSearchJSONbasic.php";
                
                var requestBody = new
                {
                    tor = new
                    {
                        text = query,
                        srchIn = new[] { "title", "author", "narrator", "series" },
                        searchType = "all",
                        searchIn = "torrents",
                        cat = new[] { "0" }, // 0 = all categories
                        main_cat = new[] { "13" }, // 13 = AudioBooks
                        browseFlagsHideVsShow = "0",
                        startDate = "",
                        endDate = "",
                        hash = "",
                        sortType = "default",
                        startNumber = "0",
                        perpage = 100
                    }
                };

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    System.Text.Encoding.UTF8,
                    "application/json"
                );

                // Create request with basic auth
                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = jsonContent
                };

                var authBytes = System.Text.Encoding.UTF8.GetBytes($"{username}:{password}");
                var authHeader = Convert.ToBase64String(authBytes);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

                _logger.LogDebug("MyAnonamouse API URL: {Url}", url);

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("MyAnonamouse returned status {Status}", response.StatusCode);
                    return new List<SearchResult>();
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                var results = ParseMyAnonamouseResponse(jsonResponse, indexer);
                
                _logger.LogInformation("MyAnonamouse returned {Count} results", results.Count);
                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching MyAnonamouse indexer {Name}", indexer.Name);
                return new List<SearchResult>();
            }
        }

        private List<SearchResult> ParseMyAnonamouseResponse(string jsonResponse, Indexer indexer)
        {
            var results = new List<SearchResult>();

            try
            {
                var doc = JsonDocument.Parse(jsonResponse);
                
                if (!doc.RootElement.TryGetProperty("data", out var dataArray))
                {
                    return results;
                }

                foreach (var item in dataArray.EnumerateArray())
                {
                    try
                    {
                        var id = item.TryGetProperty("id", out var idElem) ? idElem.GetString() : "";
                        var title = item.TryGetProperty("title", out var titleElem) ? titleElem.GetString() : "";
                        var sizeStr = "";
                        if (item.TryGetProperty("size", out var sizeElem))
                        {
                            if (sizeElem.ValueKind == System.Text.Json.JsonValueKind.String)
                            {
                                sizeStr = sizeElem.GetString() ?? "0";
                            }
                            else if (sizeElem.ValueKind == System.Text.Json.JsonValueKind.Number)
                            {
                                sizeStr = sizeElem.GetInt64().ToString();
                            }
                            else
                            {
                                sizeStr = "0";
                            }
                        }
                        var seeders = item.TryGetProperty("seeders", out var seedElem) ? seedElem.GetInt32() : 0;
                        var leechers = item.TryGetProperty("leechers", out var leechElem) ? leechElem.GetInt32() : 0;
                        var dlHash = item.TryGetProperty("dl", out var dlElem) ? dlElem.GetString() : "";
                        var category = item.TryGetProperty("catname", out var catElem) ? catElem.GetString() : "";
                        var tags = item.TryGetProperty("tags", out var tagsElem) ? tagsElem.GetString() : "";
                        var description = item.TryGetProperty("description", out var descElem) ? descElem.GetString() : "";

                        if (string.IsNullOrEmpty(title))
                            continue;

                        // Parse size - handle various formats
                        long size = 0;
                        if (!string.IsNullOrEmpty(sizeStr) && sizeStr != "0")
                        {
                            size = ParseSizeString(sizeStr);
                            _logger.LogDebug("Parsed size for MyAnonamouse result '{Title}': {Size} bytes from size field '{SizeStr}'", title, size, sizeStr);
                        }
                        else
                        {
                            // Try to extract size from description when size field is 0
                            size = ExtractSizeFromMyAnonamouseDescription(description);
                            if (size > 0)
                            {
                                _logger.LogDebug("Parsed size for MyAnonamouse result '{Title}': {Size} bytes from description", title, size);
                            }
                            else
                            {
                                _logger.LogWarning("MyAnonamouse result '{Title}' has no size information in size field or description", title);
                            }
                        }

                        // Extract author from author_info JSON
                        string? author = null;
                        if (item.TryGetProperty("author_info", out var authorInfo))
                        {
                            var authorJson = authorInfo.GetString();
                            if (!string.IsNullOrEmpty(authorJson))
                            {
                                try
                                {
                                    var authorDoc = JsonDocument.Parse(authorJson);
                                    var authors = new List<string>();
                                    foreach (var prop in authorDoc.RootElement.EnumerateObject())
                                    {
                                        authors.Add(prop.Value.GetString() ?? "");
                                    }
                                    author = string.Join(", ", authors.Where(a => !string.IsNullOrEmpty(a)));
                                }
                                catch (Exception ex) 
                                { 
                                    _logger.LogWarning(ex, "Failed to parse author JSON for search result");
                                }
                            }
                        }

                        // Extract narrator from narrator_info JSON
                        string? narrator = null;
                        if (item.TryGetProperty("narrator_info", out var narratorInfo))
                        {
                            var narratorJson = narratorInfo.GetString();
                            if (!string.IsNullOrEmpty(narratorJson))
                            {
                                try
                                {
                                    var narratorDoc = JsonDocument.Parse(narratorJson);
                                    var narrators = new List<string>();
                                    foreach (var prop in narratorDoc.RootElement.EnumerateObject())
                                    {
                                        narrators.Add(prop.Value.GetString() ?? "");
                                    }
                                    narrator = string.Join(", ", narrators.Where(n => !string.IsNullOrEmpty(n)));
                                }
                                catch (Exception ex) 
                                { 
                                    _logger.LogWarning(ex, "Failed to parse narrator JSON for search result");
                                }
                            }
                        }

                        // Detect quality and format from tags
                        var quality = DetectQualityFromTags(tags ?? "");
                        var format = DetectFormatFromTags(tags ?? "");

                        // Build download URL
                        var downloadUrl = "";
                        if (!string.IsNullOrEmpty(dlHash))
                        {
                            downloadUrl = $"https://www.myanonamouse.net/tor/download.php/{dlHash}";
                        }

                        var result = new SearchResult
                        {
                            Id = id ?? Guid.NewGuid().ToString(),
                            Title = title ?? "Unknown",
                            Artist = author ?? "Unknown Author",
                            Album = narrator != null ? $"Narrated by {narrator}" : "Unknown",
                            Category = category ?? "Audiobook",
                            Size = size,
                            Seeders = seeders,
                            Leechers = leechers,
                            Source = indexer.Name,
                            PublishedDate = DateTime.UtcNow,
                            Quality = quality,
                            Format = format,
                            TorrentUrl = downloadUrl,
                            MagnetLink = "",
                            NzbUrl = ""
                        };

                        // Attempt to parse language codes from title or tags (e.g. [ENG / M4B])
                        var detectedLang = ParseLanguageFromText(title + " " + (tags ?? ""));
                        if (!string.IsNullOrEmpty(detectedLang))
                        {
                            result.Language = detectedLang;
                        }

                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse MyAnonamouse result item");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse MyAnonamouse response");
            }

            return results;
        }

        private string BuildTorznabUrl(Indexer indexer, string query, string? category)
        {
            var url = indexer.Url.TrimEnd('/');
            var apiPath = indexer.Implementation.ToLower() switch
            {
                "torznab" => "/api",
                "newznab" => "/api",
                _ => "/api"
            };

            var queryParams = new List<string>
            {
                $"t=search",
                $"q={Uri.EscapeDataString(query)}"
            };

            // Add API key if provided
            if (!string.IsNullOrEmpty(indexer.ApiKey))
            {
                queryParams.Add($"apikey={Uri.EscapeDataString(indexer.ApiKey)}");
            }

            // Add categories if specified
            if (!string.IsNullOrEmpty(category))
            {
                queryParams.Add($"cat={Uri.EscapeDataString(category)}");
            }
            else if (!string.IsNullOrEmpty(indexer.Categories))
            {
                queryParams.Add($"cat={Uri.EscapeDataString(indexer.Categories)}");
            }

            // Add limit
            queryParams.Add("limit=100");

            return $"{url}{apiPath}?{string.Join("&", queryParams)}";
        }

        private async Task<List<SearchResult>> SearchInternetArchiveAsync(Indexer indexer, string query, string? category)
        {
            try
            {
                _logger.LogInformation("Searching Internet Archive for: {Query}", query);

                // Parse collection from AdditionalSettings (default: librivoxaudio)
                var collection = "librivoxaudio";
                
                if (!string.IsNullOrEmpty(indexer.AdditionalSettings))
                {
                    try
                    {
                        var settings = JsonDocument.Parse(indexer.AdditionalSettings);
                        if (settings.RootElement.TryGetProperty("collection", out var collectionElem))
                        {
                            var parsedCollection = collectionElem.GetString();
                            if (!string.IsNullOrEmpty(parsedCollection))
                                collection = parsedCollection;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse Internet Archive settings, using default collection");
                    }
                }

                _logger.LogDebug("Using Internet Archive collection: {Collection}", collection);

                // Build search query - search in title and creator (author) fields
                var searchQuery = $"collection:{collection} AND (title:({query}) OR creator:({query}))";
                var searchUrl = $"https://archive.org/advancedsearch.php?q={Uri.EscapeDataString(searchQuery)}&fl=identifier,title,creator,date,downloads,item_size,description&rows=100&output=json";

                _logger.LogInformation("Internet Archive search URL: {Url}", searchUrl);

                var response = await _httpClient.GetAsync(searchUrl);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Internet Archive returned status {Status}", response.StatusCode);
                    return new List<SearchResult>();
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("Internet Archive response length: {Length}", jsonResponse.Length);
                
                var searchResults = await ParseInternetArchiveSearchResponse(jsonResponse, indexer);
                
                _logger.LogInformation("Internet Archive returned {Count} results", searchResults.Count);
                return searchResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Internet Archive indexer {Name}", indexer.Name);
                return new List<SearchResult>();
            }
        }

        private async Task<List<SearchResult>> ParseInternetArchiveSearchResponse(string jsonResponse, Indexer indexer)
        {
            var results = new List<SearchResult>();

            try
            {
                _logger.LogInformation("Parsing Internet Archive response, length: {Length}", jsonResponse.Length);
                
                var doc = JsonDocument.Parse(jsonResponse);
                
                if (!doc.RootElement.TryGetProperty("response", out var responseObj))
                {
                    _logger.LogWarning("Internet Archive response missing 'response' object");
                    return results;
                }

                if (!responseObj.TryGetProperty("docs", out var docsArray))
                {
                    _logger.LogWarning("Internet Archive response missing 'docs' array");
                    return results;
                }

                _logger.LogInformation("Found {Count} Internet Archive items in response", docsArray.GetArrayLength());

                // Limit to first 20 results to avoid timeout
                var itemsToProcess = Math.Min(20, docsArray.GetArrayLength());
                _logger.LogInformation("Processing first {Count} of {Total} Internet Archive items", itemsToProcess, docsArray.GetArrayLength());

                var processedCount = 0;
                foreach (var item in docsArray.EnumerateArray())
                {
                    if (processedCount >= itemsToProcess)
                    {
                        break;
                    }
                    processedCount++;

                    try
                    {
                        var identifier = item.TryGetProperty("identifier", out var idElem) ? idElem.GetString() : "";
                        var title = item.TryGetProperty("title", out var titleElem) ? titleElem.GetString() : "";
                        var creator = item.TryGetProperty("creator", out var creatorElem) ? creatorElem.GetString() : "";
                        var itemSize = item.TryGetProperty("item_size", out var sizeElem) ? sizeElem.GetInt64() : 0;

                        if (string.IsNullOrEmpty(identifier) || string.IsNullOrEmpty(title))
                        {
                            _logger.LogDebug("Skipping item with missing identifier or title");
                            continue;
                        }

                        _logger.LogDebug("Fetching metadata for {Identifier}", identifier);

                        // Fetch detailed metadata to get file information
                        var metadataUrl = $"https://archive.org/metadata/{identifier}";
                        var metadataResponse = await _httpClient.GetAsync(metadataUrl);
                        
                        if (!metadataResponse.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Failed to fetch metadata for {Identifier}", identifier);
                            continue;
                        }

                        var metadataJson = await metadataResponse.Content.ReadAsStringAsync();
                        var audioFile = GetBestAudioFile(metadataJson, identifier);

                        if (audioFile == null)
                        {
                            _logger.LogDebug("No suitable audio file found for {Identifier}", identifier);
                            continue;
                        }

                        // Build download URL
                        var downloadUrl = $"https://archive.org/download/{identifier}/{audioFile.FileName}";

                        _logger.LogDebug("Found audio file for {Title}: {FileName} ({Format}, {Size} bytes)", 
                            title, audioFile.FileName, audioFile.Format, audioFile.Size);

                        var iaResult = new SearchResult
                        {
                            Id = Guid.NewGuid().ToString(),
                            Title = title,
                            Artist = creator ?? "Unknown",
                            Album = title,
                            Category = "Audiobook",
                            Size = audioFile.Size,
                            Seeders = 0, // N/A for direct downloads
                            Leechers = 0, // N/A for direct downloads
                            TorrentUrl = downloadUrl, // Using TorrentUrl field for direct download URL
                            DownloadType = "DDL", // Direct Download Link
                            Format = audioFile.Format,
                            Quality = DetectQualityFromFormat(audioFile.Format),
                            Source = $"{indexer.Name} (Internet Archive)"
                        };

                        try
                        {
                            var detectedLang = ParseLanguageFromText(title ?? string.Empty);
                            if (!string.IsNullOrEmpty(detectedLang)) iaResult.Language = detectedLang;
                        }
                        catch (Exception ex) 
                        { 
                            _logger.LogDebug(ex, "Failed to parse language from title: {Title}", title);
                        }

                        results.Add(iaResult);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing Internet Archive item");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Internet Archive response");
            }

            return results;
        }

        private class AudioFileInfo
        {
            public string FileName { get; set; } = "";
            public string Format { get; set; } = "";
            public long Size { get; set; }
            public int Priority { get; set; } // Lower = better
        }

        private AudioFileInfo? GetBestAudioFile(string metadataJson, string identifier)
        {
            try
            {
                var doc = JsonDocument.Parse(metadataJson);
                
                if (!doc.RootElement.TryGetProperty("files", out var filesArray))
                {
                    return null;
                }

                var audioFiles = new List<AudioFileInfo>();

                foreach (var file in filesArray.EnumerateArray())
                {
                    var fileName = file.TryGetProperty("name", out var nameElem) ? nameElem.GetString() : "";
                    var format = file.TryGetProperty("format", out var formatElem) ? formatElem.GetString() : "";
                    
                    // Size can be either a string or a number in Internet Archive API
                    long size = 0;
                    if (file.TryGetProperty("size", out var sizeElem))
                    {
                        if (sizeElem.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            long.TryParse(sizeElem.GetString(), out size);
                        }
                        else if (sizeElem.ValueKind == System.Text.Json.JsonValueKind.Number)
                        {
                            size = sizeElem.GetInt64();
                        }
                    }

                    if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(format))
                        continue;

                    // Assign priority based on format (lower = better)
                    int priority = format switch
                    {
                        "LibriVox Apple Audiobook" => 1,  // M4B - best quality, multi-chapter
                        "M4B" => 1,
                        "128Kbps MP3" => 2,                // Good quality MP3
                        "VBR MP3" => 3,                    // Variable bitrate MP3
                        "Ogg Vorbis" => 4,                 // OGG format
                        "64Kbps MP3" => 5,                 // Lower quality MP3
                        _ => int.MaxValue                  // Unknown format - lowest priority
                    };

                    // Only include known audio formats
                    if (priority < int.MaxValue)
                    {
                        audioFiles.Add(new AudioFileInfo
                        {
                            FileName = fileName,
                            Format = format,
                            Size = size,
                            Priority = priority
                        });
                    }
                }

                // Return the highest priority (lowest priority number) audio file
                return audioFiles.OrderBy(f => f.Priority).ThenByDescending(f => f.Size).FirstOrDefault();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Internet Archive metadata for {Identifier}", identifier);
                return null;
            }
        }

        private List<SearchResult> ParseTorznabResponse(string xmlContent, Indexer indexer)
        {
            var results = new List<SearchResult>();

            try
            {
                // Log first 500 chars of XML for debugging
                var preview = xmlContent.Length > 500 ? xmlContent.Substring(0, 500) + "..." : xmlContent;
                _logger.LogDebug("Parsing XML from {IndexerName}: {Preview}", indexer.Name, preview);

                // Parse XML with settings that are more lenient
                var settings = new System.Xml.XmlReaderSettings
                {
                    DtdProcessing = System.Xml.DtdProcessing.Ignore,
                    XmlResolver = null,
                    IgnoreWhitespace = true,
                    IgnoreComments = true
                };

                System.Xml.Linq.XDocument doc;
                using (var reader = System.Xml.XmlReader.Create(new System.IO.StringReader(xmlContent), settings))
                {
                    doc = System.Xml.Linq.XDocument.Load(reader);
                }

                var channel = doc.Root?.Element("channel");
                if (channel == null)
                {
                    _logger.LogWarning("Invalid Torznab response: no channel element");
                    return results;
                }

                var items = channel.Elements("item");
                var isUsenet = indexer.Type.Equals("Usenet", StringComparison.OrdinalIgnoreCase);

                foreach (var item in items)
                {
                    try
                    {
                        var result = new SearchResult
                        {
                            Id = item.Element("guid")?.Value ?? Guid.NewGuid().ToString(),
                            Title = item.Element("title")?.Value ?? "Unknown",
                            Source = indexer.Name,
                            Category = item.Element("category")?.Value ?? "Audiobook"
                        };

                        // Parse published date
                        var pubDateStr = item.Element("pubDate")?.Value;
                        if (DateTime.TryParse(pubDateStr, out var pubDate))
                        {
                            result.PublishedDate = pubDate;
                        }
                        else
                        {
                            result.PublishedDate = DateTime.UtcNow;
                        }

                        // Parse Torznab/Newznab attributes
                        var torznabNs = System.Xml.Linq.XNamespace.Get("http://torznab.com/schemas/2015/feed");
                        var attributes = item.Elements(torznabNs + "attr").ToList();

                        foreach (var attr in attributes)
                        {
                            var name = attr.Attribute("name")?.Value;
                            var value = attr.Attribute("value")?.Value;

                            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(value))
                                continue;

                            switch (name.ToLower())
                            {
                                case "size":
                                    var parsedSize = ParseSizeString(value);
                                    if (parsedSize > 0)
                                    {
                                        result.Size = parsedSize;
                                        _logger.LogDebug("Parsed size for {Title}: {Size} bytes from indexer {Indexer}", result.Title, parsedSize, indexer.Name);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Failed to parse size value '{Value}' for result '{Title}' from indexer {Indexer}", value, result.Title, indexer.Name);
                                    }
                                    break;
                                case "seeders":
                                    if (int.TryParse(value, out var seeders))
                                        result.Seeders = seeders;
                                    break;
                                case "peers":
                                    if (int.TryParse(value, out var peers))
                                        result.Leechers = peers;
                                    break;
                                case "magneturl":
                                    result.MagnetLink = value;
                                    break;
                                case "grabs":
                                    // Usenet grabs can be used as a quality indicator
                                    break;
                            }
                        }

                        // Get enclosure/link for download URL
                        var enclosure = item.Element("enclosure");
                        if (enclosure != null)
                        {
                            var enclosureUrl = enclosure.Attribute("url")?.Value;
                            if (!string.IsNullOrEmpty(enclosureUrl))
                            {
                                if (isUsenet)
                                {
                                    result.NzbUrl = enclosureUrl;
                                }
                                else
                                {
                                    result.TorrentUrl = enclosureUrl;
                                }
                            }
                        }

                        // If no magnet link found in attributes, check link element
                        if (string.IsNullOrEmpty(result.MagnetLink) && !isUsenet)
                        {
                            var link = item.Element("link")?.Value;
                            if (!string.IsNullOrEmpty(link) && link.StartsWith("magnet:"))
                            {
                                result.MagnetLink = link;
                            }
                            else if (!string.IsNullOrEmpty(link) && string.IsNullOrEmpty(result.TorrentUrl))
                            {
                                result.TorrentUrl = link;
                            }
                        }

                        // Parse description for additional metadata
                        var description = item.Element("description")?.Value;
                        if (!string.IsNullOrEmpty(description))
                        {
                            result.Description = description;
                            
                            // Try to extract quality/format from description or title
                            var titleAndDesc = $"{result.Title} {description}".ToLower();
                            
                            if (titleAndDesc.Contains("flac"))
                                result.Quality = "FLAC";
                            else if (titleAndDesc.Contains("320") || titleAndDesc.Contains("320kbps"))
                                result.Quality = "MP3 320kbps";
                            else if (titleAndDesc.Contains("256") || titleAndDesc.Contains("256kbps"))
                                result.Quality = "MP3 256kbps";
                            else if (titleAndDesc.Contains("192") || titleAndDesc.Contains("192kbps"))
                                result.Quality = "MP3 192kbps";
                            else if (titleAndDesc.Contains("128") || titleAndDesc.Contains("128kbps"))
                                result.Quality = "MP3 128kbps";
                            else if (titleAndDesc.Contains("64") || titleAndDesc.Contains("64kbps"))
                                result.Quality = "MP3 64kbps";
                            else if (titleAndDesc.Contains("m4b"))
                                result.Quality = "M4B";
                            else
                                result.Quality = "Unknown";

                            // Detect format
                            if (titleAndDesc.Contains("m4b"))
                                result.Format = "M4B";
                            else if (titleAndDesc.Contains("flac"))
                                result.Format = "FLAC";
                            else if (titleAndDesc.Contains("mp3"))
                                result.Format = "MP3";
                            else if (titleAndDesc.Contains("opus"))
                                result.Format = "OPUS";
                            else if (titleAndDesc.Contains("aac"))
                                result.Format = "AAC";

                            // Detect language codes present in title or description (e.g. [ENG / M4B])
                            try
                            {
                                var lang = ParseLanguageFromText(result.Title + " " + (description ?? string.Empty));
                                if (!string.IsNullOrEmpty(lang)) result.Language = lang;
                            }
                            catch { /* Non-critical */ }
                        }

                        // Extract author from title if possible (common format: "Author - Title")
                        var titleParts = result.Title.Split(new[] { " - ", " – " }, StringSplitOptions.RemoveEmptyEntries);
                        if (titleParts.Length >= 2)
                        {
                            result.Artist = titleParts[0].Trim();
                            result.Album = string.Join(" - ", titleParts.Skip(1)).Trim();
                        }
                        else
                        {
                            result.Artist = "Unknown Author";
                            result.Album = result.Title;
                        }

                        // Only add results that have a valid download link
                        if (!string.IsNullOrEmpty(result.MagnetLink) || 
                            !string.IsNullOrEmpty(result.TorrentUrl) || 
                            !string.IsNullOrEmpty(result.NzbUrl))
                        {
                            // Set download type based on what's available
                            if (!string.IsNullOrEmpty(result.NzbUrl))
                            {
                                result.DownloadType = "Usenet";
                            }
                            else if (!string.IsNullOrEmpty(result.MagnetLink) || !string.IsNullOrEmpty(result.TorrentUrl))
                            {
                                result.DownloadType = "Torrent";
                            }
                            
                            results.Add(result);
                        }
                        else
                        {
                            _logger.LogWarning("Skipping result '{Title}' - no download link found", result.Title);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing indexer result item");
                    }
                }
            }
            catch (System.Xml.XmlException xmlEx)
            {
                _logger.LogError(xmlEx, "XML parsing error from {IndexerName} at Line {Line}, Position {Position}: {Message}", 
                    indexer.Name, xmlEx.LineNumber, xmlEx.LinePosition, xmlEx.Message);
                
                // Log the problematic XML content around the error
                if (!string.IsNullOrEmpty(xmlContent))
                {
                    var lines = xmlContent.Split('\n');
                    if (xmlEx.LineNumber > 0 && xmlEx.LineNumber <= lines.Length)
                    {
                        var startLine = Math.Max(0, xmlEx.LineNumber - 3);
                        var endLine = Math.Min(lines.Length - 1, xmlEx.LineNumber + 2);
                        var context = string.Join("\n", lines[startLine..(endLine + 1)]);
                        _logger.LogError("XML context around error:\n{Context}", context);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing Torznab XML response from {IndexerName}", indexer.Name);
            }

            return results;
        }

        private List<SearchResult> GenerateMockIndexerResults(string query)
        {
            // Generate multiple mock results to simulate real indexer responses
            // Default to torrent for backwards compatibility
            return GenerateMockIndexerResults(query, "Mock Indexer", "Torrent");
        }

        private List<SearchResult> GenerateMockIndexerResults(string query, string indexerName)
        {
            // Default to torrent for backwards compatibility
            return GenerateMockIndexerResults(query, indexerName, "Torrent");
        }

        private List<SearchResult> GenerateMockIndexerResults(string query, string indexerName, string indexerType)
        {
            // Generate multiple mock results to simulate real indexer responses
            var random = new Random();
            var results = new List<SearchResult>();
            var isUsenet = indexerType.Equals("Usenet", StringComparison.OrdinalIgnoreCase);
            
            _logger.LogInformation("Generating {Count} mock {Type} results for indexer {IndexerName}", 5, indexerType, indexerName);
            
            for (int i = 0; i < 5; i++)
            {
                var result = new SearchResult
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"{query} - Quality {i + 1}",
                    Artist = "Various Authors",
                    Album = $"{query} Series",
                    Category = "Audiobook",
                    Size = random.Next(200_000_000, 1_500_000_000), // 200 MB to 1.5 GB
                    Seeders = isUsenet ? 0 : random.Next(5, 100), // Usenet doesn't have seeders
                    Leechers = isUsenet ? 0 : random.Next(0, 20), // Usenet doesn't have leechers
                    Source = indexerName,
                    PublishedDate = DateTime.UtcNow.AddDays(-random.Next(1, 365)),
                    Quality = i switch
                    {
                        0 => "MP3 64kbps",
                        1 => "MP3 128kbps",
                        2 => "MP3 192kbps",
                        3 => "M4B 128kbps",
                        _ => "FLAC"
                    },
                    Format = i >= 3 ? "M4B" : "MP3",
                    Language = "English"
                };

                // Set appropriate download link based on indexer type
                if (isUsenet)
                {
                    result.NzbUrl = $"https://{indexerName.ToLower()}.example.com/api/nzb/{Guid.NewGuid():N}";
                    result.MagnetLink = string.Empty;
                    result.TorrentUrl = string.Empty;
                }
                else
                {
                    result.MagnetLink = $"magnet:?xt=urn:btih:{Guid.NewGuid():N}";
                    result.NzbUrl = string.Empty;
                }

                results.Add(result);
            }
            
            return results;
        }

        private List<SearchResult> GenerateMockResults(string query, string source)
        {
            // This is mock data for development purposes
            return new List<SearchResult>
            {
                new SearchResult
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"Sample Audiobook - {query}",
                    Artist = "Sample Author",
                    Album = "Sample Series Book 1",
                    Category = "Audiobook",
                    Size = 512_000_000, // 512 MB
                    Seeders = 25,
                    Leechers = 3,
                    MagnetLink = "magnet:?xt=urn:btih:sample",
                    Source = source,
                    PublishedDate = DateTime.UtcNow.AddDays(-Random.Shared.Next(1, 365)),
                    Quality = "MP3 128kbps",
                    Format = "MP3"
                }
            };
        }

        private string DetectQualityFromTags(string tags)
        {
            var lowerTags = tags.ToLower();
            
            if (lowerTags.Contains("flac"))
                return "FLAC";
            else if (lowerTags.Contains("320") || lowerTags.Contains("320kbps"))
                return "MP3 320kbps";
            else if (lowerTags.Contains("256") || lowerTags.Contains("256kbps"))
                return "MP3 256kbps";
            else if (lowerTags.Contains("192") || lowerTags.Contains("192kbps"))
                return "MP3 192kbps";
            else if (lowerTags.Contains("128") || lowerTags.Contains("128kbps"))
                return "MP3 128kbps";
            else if (lowerTags.Contains("64") || lowerTags.Contains("64kbps"))
                return "MP3 64kbps";
            else if (lowerTags.Contains("m4b"))
                return "M4B";
            else
                return "Unknown";
        }

        private string DetectQualityFromFormat(string format)
        {
            if (string.IsNullOrEmpty(format))
                return "Unknown";

            var lowerFormat = format.ToLower();

            if (lowerFormat.Contains("flac"))
                return "FLAC";
            else if (lowerFormat.Contains("m4b") || lowerFormat.Contains("apple audiobook"))
                return "M4B";
            else if (lowerFormat.Contains("320kbps") || lowerFormat.Contains("320 kbps"))
                return "MP3 320kbps";
            else if (lowerFormat.Contains("256kbps") || lowerFormat.Contains("256 kbps"))
                return "MP3 256kbps";
            else if (lowerFormat.Contains("192kbps") || lowerFormat.Contains("192 kbps"))
                return "MP3 192kbps";
            else if (lowerFormat.Contains("128kbps") || lowerFormat.Contains("128 kbps"))
                return "MP3 128kbps";
            else if (lowerFormat.Contains("64kbps") || lowerFormat.Contains("64 kbps"))
                return "MP3 64kbps";
            else if (lowerFormat.Contains("vbr mp3") || lowerFormat.Contains("variable bitrate"))
                return "MP3 VBR";
            else if (lowerFormat.Contains("ogg vorbis") || lowerFormat.Contains("ogg"))
                return "OGG Vorbis";
            else if (lowerFormat.Contains("opus"))
                return "OPUS";
            else if (lowerFormat.Contains("aac"))
                return "AAC";
            else if (lowerFormat.Contains("mp3"))
                return "MP3";
            else
                return "Unknown";
        }

        private string DetectFormatFromTags(string tags)
        {
            var lowerTags = tags.ToLower();
            
            if (lowerTags.Contains("m4b"))
                return "M4B";
            else if (lowerTags.Contains("flac"))
                return "FLAC";
            else if (lowerTags.Contains("mp3"))
                return "MP3";
            else if (lowerTags.Contains("opus"))
                return "OPUS";
            else if (lowerTags.Contains("aac"))
                return "AAC";
            else
                return "MP3"; // Default to MP3
        }

        /// <summary>
        /// Parse common language codes from a text block and return a full language name.
        /// Matches bracketed tokens like "[ENG / M4B]", parenthesized "(ENG)", or standalone tokens with word boundaries.
        /// Supports both three-letter codes and common two-letter aliases (ENG|EN -> English, DUT|NL -> Dutch, GER|DE -> German, FRE|FR -> French).
        /// Matching is case-insensitive and conservative to avoid false positives.
        /// </summary>
        private string? ParseLanguageFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;

            // Normalize whitespace
            var normalized = Regex.Replace(text, "\\s+", " ", RegexOptions.Compiled | RegexOptions.IgnoreCase).Trim();

            // Combined pattern: look for bracketed or parenthesized tokens OR standalone word-boundary tokens
            // Examples matched: [ENG / M4B], (EN), ENG, EN
            var codes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "ENG", "English" }, { "EN", "English" },
                { "DUT", "Dutch" },    { "NL", "Dutch" },
                { "GER", "German" },   { "DE", "German" },
                { "FRE", "French" },   { "FR", "French" }
            };

            // Build a joined alternation like ENG|EN|DUT|NL|...
            var alternation = string.Join("|", codes.Keys.Select(Regex.Escape));

            // Bracketed or parenthesis forms: [ ENG / ... ] or (EN)
            // Use verbatim interpolated string and escape [ and (
            var bracketedPattern = $@"[\[\(]\s*(?:{alternation})\b"; // starts with [ or ( then code

            // Standalone word boundary pattern: \b(ENG|EN|DUT|NL|...)\b
            var wordBoundaryPattern = $"\\b(?:{alternation})\\b";

            // Try bracketed/parenthesized first (higher confidence)
            var bracketMatch = Regex.Match(normalized, bracketedPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            if (bracketMatch.Success)
            {
                var code = bracketMatch.Value.TrimStart('[', '(').Trim().Split(' ', '/', ',')[0];
                if (codes.TryGetValue(code.ToUpperInvariant(), out var lang)) return lang;
            }

            // Fall back to standalone word match
            var wordMatch = Regex.Match(normalized, wordBoundaryPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            if (wordMatch.Success)
            {
                var code = wordMatch.Value.Trim();
                if (codes.TryGetValue(code.ToUpperInvariant(), out var lang)) return lang;
            }

            return null;
        }

        private long ExtractSizeFromMyAnonamouseDescription(string? description)
        {
            if (string.IsNullOrEmpty(description))
                return 0;

            // Look for patterns like "Total Size : 259MB (272 033 986 bytes)"
            var match = Regex.Match(description, @"Total Size\s*:\s*([\d\.,]+)\s*(MB|GB|KB|B)\s*\(([\d\s,]+)\s*bytes?\)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Try to parse the bytes value first (most accurate)
                var bytesStr = match.Groups[3].Value.Replace(",", "").Replace(" ", "");
                if (long.TryParse(bytesStr, out var bytes))
                {
                    _logger.LogDebug("Extracted size from MyAnonamouse description bytes: {Bytes}", bytes);
                    return bytes;
                }

                // Fallback to parsing the formatted size
                var sizeValue = match.Groups[1].Value.Replace(",", "");
                var unit = match.Groups[2].Value.ToUpper();
                if (double.TryParse(sizeValue, out var value))
                {
                    var result = unit switch
                    {
                        "B" => (long)value,
                        "KB" => (long)(value * 1024),
                        "MB" => (long)(value * 1024 * 1024),
                        "GB" => (long)(value * 1024 * 1024 * 1024),
                        _ => (long)value
                    };
                    _logger.LogDebug("Extracted size from MyAnonamouse description formatted: {Value} {Unit} = {Result} bytes", value, unit, result);
                    return result;
                }
            }

            // Alternative pattern: just "Total Size : 259MB" without bytes
            match = Regex.Match(description, @"Total Size\s*:\s*([\d\.,]+)\s*(MB|GB|KB|B)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var sizeValue = match.Groups[1].Value.Replace(",", "");
                var unit = match.Groups[2].Value.ToUpper();
                if (double.TryParse(sizeValue, out var value))
                {
                    var result = unit switch
                    {
                        "B" => (long)value,
                        "KB" => (long)(value * 1024),
                        "MB" => (long)(value * 1024 * 1024),
                        "GB" => (long)(value * 1024 * 1024 * 1024),
                        _ => (long)value
                    };
                    _logger.LogDebug("Extracted size from MyAnonamouse description (no bytes): {Value} {Unit} = {Result} bytes", value, unit, result);
                    return result;
                }
            }

            _logger.LogDebug("No size found in MyAnonamouse description");
            return 0;
        }

        private long ParseSizeString(string sizeStr)
        {
            if (string.IsNullOrEmpty(sizeStr))
                return 0;

            // Remove any commas and extra spaces
            sizeStr = sizeStr.Replace(",", "").Trim();

            // Try to parse as direct bytes first
            if (long.TryParse(sizeStr, out var bytes))
                return bytes;

            // Handle formats like "500 MB", "1.2 GB", "1024 KB", etc.
            var match = System.Text.RegularExpressions.Regex.Match(sizeStr, @"^([\d\.]+)\s*(KB|MB|GB|TB|B)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, out var value))
                {
                    var unit = match.Groups[2].Value.ToUpper();
                    return unit switch
                    {
                        "B" => (long)value,
                        "KB" => (long)(value * 1024),
                        "MB" => (long)(value * 1024 * 1024),
                        "GB" => (long)(value * 1024 * 1024 * 1024),
                        "TB" => (long)(value * 1024 * 1024 * 1024 * 1024),
                        _ => (long)value
                    };
                }
            }

            _logger.LogWarning("Unable to parse size string: '{SizeStr}'", sizeStr);
            return 0;
        }

        public async Task<List<ApiConfiguration>> GetEnabledMetadataSourcesAsync()
        {
            try
            {
                _logger.LogDebug("Querying database for enabled metadata sources...");
                
                var metadataSources = await _dbContext.ApiConfigurations
                    .Where(api => api.IsEnabled && api.Type == "metadata")
                    .OrderBy(api => api.Priority)
                    .ToListAsync();
                
                if (metadataSources.Count > 0)
                {
                    _logger.LogInformation("Retrieved {Count} enabled metadata sources: {Sources}", 
                        metadataSources.Count, 
                        string.Join(", ", metadataSources.Select(s => $"{s.Name} (Priority: {s.Priority}, BaseUrl: {s.BaseUrl})")));
                }
                else
                {
                    _logger.LogWarning("No enabled metadata sources found in database");
                }
                
                return metadataSources;
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                _logger.LogError(ex, "Database update error retrieving enabled metadata sources");
                return new List<ApiConfiguration>();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(ex, "Invalid operation error retrieving enabled metadata sources");
                return new List<ApiConfiguration>();
            }
        }
    }
}

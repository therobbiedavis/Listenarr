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

using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;
using Microsoft.AspNetCore.SignalR;
using Listenarr.Api.Hubs;
using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
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
                    // Structured payload: include a type so clients can distinguish interactive vs automatic
                    await _hubContext.Clients.All.SendAsync("SearchProgress", new { message, asin, type = "interactive" });
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

                // Consult application settings to avoid calling providers that are disabled
                try
                {
                    var appSettings = await _configurationService.GetApplicationSettingsAsync();

                    var fallback = new List<SearchResult>();

                    if (appSettings == null || appSettings.EnableAmazonSearch)
                    {
                        var amazonResults = await _amazonSearchService.SearchAudiobooksAsync(query);
                        foreach (var a in amazonResults.Take(12))
                        {
                            var r = ConvertAmazonSearchToResult(a);
                            r.IsEnriched = false;
                            fallback.Add(r);
                        }
                    }

                    if (appSettings == null || appSettings.EnableAudibleSearch)
                    {
                        var audibleResults = await _audibleSearchService.SearchAudiobooksAsync(query);
                        foreach (var a in audibleResults.Take(12))
                        {
                            var r = ConvertAudibleSearchToResult(a);
                            r.IsEnriched = false;
                            fallback.Add(r);
                        }
                    }

                    _logger.LogInformation("Returning {Count} raw-conversion fallback results for query: {Query}", fallback.Count, query);
                    return ApplySorting(fallback, sortBy, sortDirection);
                }
                catch (Exception exFallback)
                {
                    _logger.LogWarning(exFallback, "Failed to consult application settings during fallback; performing provider calls conservatively");

                    // Conservative fallback: call both providers if settings couldn't be loaded
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
            }

            // Also search configured indexers for additional results (including DDL downloads)
            var indexerResults = await SearchIndexersAsync(query, category, sortBy, sortDirection, isAutomaticSearch);
            if (indexerResults.Any())
            {
                results.AddRange(indexerResults);
                _logger.LogInformation("Added {Count} indexer results (including DDL downloads) for query: {Query}", indexerResults.Count, query);
            }

            // Apply sorting first so trimming (if configured) keeps the most relevant/desired items
            var sorted = ApplySorting(results, sortBy, sortDirection);

            try
            {
                var appSettings = await _configurationService.GetApplicationSettingsAsync();
                if (appSettings != null && appSettings.SearchResultCap > 0 && sorted.Count > appSettings.SearchResultCap)
                {
                    _logger.LogInformation("Trimming total combined search results from {Before} to SearchResultCap={Cap}", sorted.Count, appSettings.SearchResultCap);
                    sorted = sorted.Take(appSettings.SearchResultCap).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to load application settings when enforcing total SearchResultCap; returning full result set");
            }

            return sorted;
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

        public async Task<List<SearchResult>> IntelligentSearchAsync(string query, int candidateLimit = 200, int returnLimit = 100, string containmentMode = "Relaxed", bool requireAuthorAndPublisher = false, double fuzzyThreshold = 0.2)
        {
            var results = new List<SearchResult>();

            try
            {
                _logger.LogInformation("Starting intelligent search for: {Query}", query);

                // Parse search prefixes to force specific search types
                string? searchType = null;
                string actualQuery = query;
                
                if (query.StartsWith("ASIN:", StringComparison.OrdinalIgnoreCase))
                {
                    searchType = "ASIN";
                    actualQuery = query.Substring(5).Trim();
                    _logger.LogInformation("Detected ASIN search: {ASIN}", actualQuery);
                }
                else if (query.StartsWith("ISBN:", StringComparison.OrdinalIgnoreCase))
                {
                    searchType = "ISBN";
                    actualQuery = query.Substring(5).Trim();
                    _logger.LogInformation("Detected ISBN search: {ISBN}", actualQuery);
                }
                else if (query.StartsWith("AUTHOR:", StringComparison.OrdinalIgnoreCase))
                {
                    searchType = "AUTHOR";
                    actualQuery = query.Substring(7).Trim();
                    _logger.LogInformation("Detected AUTHOR search: {Author}", actualQuery);
                }
                else if (query.StartsWith("TITLE:", StringComparison.OrdinalIgnoreCase))
                {
                    searchType = "TITLE";
                    actualQuery = query.Substring(6).Trim();
                    _logger.LogInformation("Detected TITLE search: {Title}", actualQuery);
                }

                // Step 1: Parallel search Amazon & Audible
                _logger.LogInformation("Searching for: {Query}", actualQuery);
                await BroadcastSearchProgressAsync($"Searching for {actualQuery}", null);

                // Detect if the query is an ISBN (digits only after cleaning). If so, skip Audible
                var digitsOnly = new string((actualQuery ?? string.Empty).Where(char.IsDigit).ToArray());
                var isIsbnQuery = digitsOnly.Length == 10 || digitsOnly.Length == 13 || searchType == "ISBN";

                List<AmazonSearchResult> amazonResults = new();
                List<AudibleSearchResult> audibleResults = new();

                // Flags controlling provider calls (enabled by default)
                var skipAmazon = false; // set true to disable Amazon calls
                var skipAudible = false; // set true to disable Audible calls
                var skipOpenLibrary = false; // set true to disable OpenLibrary augmentation

                // Apply application-level search settings (if configured)
                try
                {
                    var appSettings = await _configurationService.GetApplicationSettingsAsync();
                    if (appSettings != null)
                    {
                        // Provider toggles (invert to skip flags)
                        skipAmazon = !appSettings.EnableAmazonSearch;
                        skipAudible = !appSettings.EnableAudibleSearch;
                        skipOpenLibrary = !appSettings.EnableOpenLibrarySearch;

                        // Apply candidate/result caps and fuzzy threshold when provided
                        if (appSettings.SearchCandidateCap > 0)
                        {
                            candidateLimit = appSettings.SearchCandidateCap;
                        }
                        if (appSettings.SearchResultCap > 0)
                        {
                            returnLimit = appSettings.SearchResultCap;
                        }
                        if (appSettings.SearchFuzzyThreshold >= 0.0 && appSettings.SearchFuzzyThreshold <= 1.0)
                        {
                            fuzzyThreshold = appSettings.SearchFuzzyThreshold;
                        }
                    }
                }
                catch (Exception exAppSettings)
                {
                    _logger.LogDebug(exAppSettings, "Failed to load application search settings, falling back to defaults");
                }

                // Always attempt Audible; Amazon is conditional on skipAmazon
                if (!string.IsNullOrEmpty(actualQuery))
                {
                    Task<List<AmazonSearchResult>>? amazonTask = null;
                    Task<List<AudibleSearchResult>>? audibleTask = null;
                    
                    // Handle specific search types
                    if (searchType == "ASIN")
                    {
                        // For ASIN search, skip Amazon/Audible search and go directly to metadata fetch
                        if (!skipAudible)
                        {
                            _logger.LogInformation("ASIN search: directly fetching metadata for {ASIN}", actualQuery);
                            asinCandidates.Add(actualQuery);
                        }
                    }
                    else if (searchType == "ISBN")
                    {
                        // For ISBN, only search Amazon (Audible doesn't support ISBN well)
                        if (!skipAmazon)
                        {
                            amazonTask = _amazonSearchService.SearchAudiobooksAsync(actualQuery!);
                        }
                    }
                    else if (searchType == "AUTHOR" || searchType == "TITLE")
                    {
                        // For AUTHOR or TITLE, search both but the query is already parsed
                        if (!skipAmazon)
                        {
                            amazonTask = _amazonSearchService.SearchAudiobooksAsync(actualQuery!);
                        }
                        if (!skipAudible)
                        {
                            audibleTask = _audibleSearchService.SearchAudiobooksAsync(actualQuery!);
                        }
                    }
                    else
                    {
                        // Normal search (no prefix)
                        if (!skipAmazon)
                        {
                            amazonTask = _amazonSearchService.SearchAudiobooksAsync(actualQuery!);
                        }
                        if (!skipAudible)
                        {
                            audibleTask = _audibleSearchService.SearchAudiobooksAsync(actualQuery!);
                        }
                    }

                    if (amazonTask != null)
                    {
                        amazonResults = await amazonTask;
                    }

                    if (audibleTask != null)
                    {
                        audibleResults = await audibleTask;
                    }
                }
                _logger.LogInformation("Collected {AmazonCount} Amazon raw results and {AudibleCount} Audible raw results", amazonResults.Count, audibleResults.Count);

                // Step 2: Build a unified ASIN candidate set (Amazon priority, then Audible)
                // Also create a lookup map for fallback titles and full search result objects
                var asinCandidates = new List<string>();
                var asinToRawResult = new Dictionary<string, (string? Title, string? Author, string? ImageUrl)>(StringComparer.OrdinalIgnoreCase);
                var asinToAudibleResult = new Dictionary<string, AudibleSearchResult>(StringComparer.OrdinalIgnoreCase);
                var asinToSource = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var asinToOpenLibrary = new Dictionary<string, OpenLibraryBook>(StringComparer.OrdinalIgnoreCase);
                // OpenLibrary-derived SearchResult placeholders (converted directly from OL docs)
                var openLibraryDerivedResults = new List<SearchResult>();

                // Per-provider candidate caps (limit how many candidates we take from each source)
                // These are per-provider caps; we intentionally do NOT cap the unified candidate set.
                int amazonProviderCap = 50; // max ASINs to take from Amazon results
                int audibleProviderCap = 50; // max ASINs to take from Audible results

                // Populate ASIN candidates from Amazon results with detailed logging
                foreach (var a in amazonResults.Take(amazonProviderCap))
                {
                    if (string.IsNullOrEmpty(a.Asin))
                    {
                        _logger.LogInformation("Amazon search result missing ASIN. Title='{Title}', Author='{Author}'", a.Title, a.Author);
                        continue;
                    }

                    if (!IsValidAsin(a.Asin!))
                    {
                        _logger.LogInformation("Amazon search result had invalid ASIN '{Asin}'. Title='{Title}', Author='{Author}'", a.Asin, a.Title, a.Author);
                        continue;
                    }

                    // Filter obvious non-audiobook product results early
                    if (IsProductLikeTitle(a.Title) || IsSellerArtist(a.Author))
                    {
                        _logger.LogInformation("Skipping Amazon ASIN {Asin} because title/author looks like a product or seller: Title='{Title}', Author='{Author}'", a.Asin, a.Title, a.Author);
                        continue;
                    }

                    asinCandidates.Add(a.Asin!);
                    asinToRawResult[a.Asin!] = (a.Title, a.Author, a.ImageUrl);
                    asinToSource[a.Asin!] = "Amazon";
                    _logger.LogInformation("Added Amazon ASIN candidate {Asin} Title='{Title}' Author='{Author}' ImageUrl='{ImageUrl}'", a.Asin, a.Title, a.Author, a.ImageUrl);
                }

                foreach (var a in audibleResults.Where(a => !string.IsNullOrEmpty(a.Asin) && IsValidAsin(a.Asin!)).Take(audibleProviderCap))
                {
                    // Filter obvious non-audiobook results even from Audible (defensive)
                    if (IsProductLikeTitle(a.Title) || IsSellerArtist(a.Author))
                    {
                        _logger.LogInformation("Skipping Audible ASIN {Asin} because title/author looks like a product or seller: Title='{Title}', Author='{Author}'", a.Asin, a.Title, a.Author);
                        continue;
                    }

                    if (asinToRawResult.TryAdd(a.Asin!, (a.Title, a.Author, a.ImageUrl)))
                    {
                        asinCandidates.Add(a.Asin!);
                        asinToAudibleResult[a.Asin!] = a;  // Store full Audible search result
                        asinToSource[a.Asin!] = "Audible";
                    }
                }

                // Augment ASIN candidates with OpenLibrary suggestions (run after Amazon/Audible but before trimming)
                if (!skipOpenLibrary)
                {
                    try
                    {
                        if (!string.IsNullOrEmpty(query))
                        {
                            var books = await _openLibraryService.SearchBooksAsync(query, null, 5);
                            foreach (var book in books.Docs.Take(3))
                            {
                                if (!string.IsNullOrEmpty(book.Title) && !string.Equals(book.Title, query, StringComparison.OrdinalIgnoreCase))
                                {
                                    _logger.LogInformation("OpenLibrary suggested title: {Title}", book.Title);

                                    // Convert OpenLibrary work/edition into minimal AudibleBookMetadata and SearchResult
                                    try
                                    {
                                        string? coverUrl = null;
                                        if (book.CoverId.HasValue && book.CoverId.Value > 0)
                                        {
                                            coverUrl = $"https://covers.openlibrary.org/b/id/{book.CoverId}-L.jpg";
                                        }

                                        var metadata = new AudibleBookMetadata
                                        {
                                            Asin = null,
                                            Source = "OpenLibrary",
                                            Title = book.Title,
                                            Authors = book.AuthorName != null ? book.AuthorName.Where(a => !string.IsNullOrWhiteSpace(a)).ToList() : null,
                                            Publisher = (book.Publisher != null && book.Publisher.Count > 1) ? "Multiple" : (book.Publisher != null && book.Publisher.Any() ? book.Publisher.FirstOrDefault() : null),
                                            PublishYear = book.FirstPublishYear?.ToString(),
                                            Description = null,
                                            ImageUrl = coverUrl
                                        };

                                        var searchResult = await ConvertMetadataToSearchResultAsync(metadata, string.Empty);
                                        searchResult.IsEnriched = true;
                                        searchResult.MetadataSource = "OpenLibrary";

                                        // If OpenLibrary provides a canonical key (work or edition), expose it as product/result URLs
                                        try
                                        {
                                            if (!string.IsNullOrWhiteSpace(book.Key))
                                            {
                                                // Prefer work page/JSON when key is a /works/... value
                                                if (book.Key.StartsWith("/works", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    searchResult.ProductUrl = $"https://openlibrary.org{book.Key}"; // human-facing page
                                                    // also expose a .json metadata link if desired via ResultUrl
                                                    searchResult.ResultUrl = $"https://openlibrary.org{book.Key}.json";
                                                }
                                                else if (book.Key.StartsWith("/books", StringComparison.OrdinalIgnoreCase))
                                                {
                                                    searchResult.ProductUrl = $"https://openlibrary.org{book.Key}";
                                                    searchResult.ResultUrl = $"https://openlibrary.org{book.Key}.json";
                                                }
                                                else
                                                {
                                                    // If key is a plain OLID like 'OL82548W', build book/work URLs conservatively
                                                    if (Regex.IsMatch(book.Key, "^OL\\w+W$", RegexOptions.IgnoreCase))
                                                    {
                                                        searchResult.ProductUrl = $"https://openlibrary.org/works/{book.Key}";
                                                        searchResult.ResultUrl = $"https://openlibrary.org/works/{book.Key}.json";
                                                    }
                                                    else if (Regex.IsMatch(book.Key, "^OL\\w+M$", RegexOptions.IgnoreCase))
                                                    {
                                                        searchResult.ProductUrl = $"https://openlibrary.org/books/{book.Key}";
                                                        searchResult.ResultUrl = $"https://openlibrary.org/books/{book.Key}.json";
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception exUrl)
                                        {
                                            _logger.LogDebug(exUrl, "Failed to build OpenLibrary URL for book key {Key}", book.Key);
                                        }

                                        // If OpenLibrary provided a canonical key (e.g. '/works/OL82548W' or 'OL82548W'),
                                        // populate SearchResult.Id with the OLID so callers can treat it like an identifier
                                        try
                                        {
                                            if (!string.IsNullOrWhiteSpace(book.Key))
                                            {
                                                var raw = book.Key.Trim();
                                                // If the key is a path like '/works/OL82548W', take the last segment
                                                if (raw.StartsWith("/"))
                                                {
                                                    var parts = raw.Trim('/').Split('/');
                                                    raw = parts.Length > 0 ? parts.Last() : raw;
                                                }

                                                // Ensure we have a sensible token and assign it
                                                if (!string.IsNullOrWhiteSpace(raw))
                                                {
                                                    searchResult.Id = raw;
                                                }
                                            }
                                        }
                                        catch (Exception exId)
                                        {
                                            _logger.LogDebug(exId, "Failed to extract OpenLibrary ID for book key {Key}", book.Key);
                                        }

                                        openLibraryDerivedResults.Add(searchResult);
                                        _logger.LogInformation("Added OpenLibrary-derived candidate: {Title}", book.Title);
                                    }
                                    catch (Exception exBook)
                                    {
                                        _logger.LogWarning(exBook, "Failed to convert OpenLibrary book {Title} to search result", book.Title);
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "OpenLibrary augmentation failed, continuing without alternate titles");
                    }
                }

                // Server-side: enrich OpenLibrary-derived candidates by fetching their canonical .json
                // Populate Description and ImageUrl where available to avoid client-side on-click fetches
                if (openLibraryDerivedResults != null && openLibraryDerivedResults.Any())
                {
                    _logger.LogInformation("Enriching {Count} OpenLibrary-derived candidate(s) with canonical JSON", openLibraryDerivedResults.Count);
                    foreach (var olr in openLibraryDerivedResults)
                    {
                        try
                        {
                            // Prefer ResultUrl if provided (expected to be the .json endpoint)
                            var jsonUrl = olr.ResultUrl;
                            if (string.IsNullOrWhiteSpace(jsonUrl) && !string.IsNullOrWhiteSpace(olr.ProductUrl))
                            {
                                // If only a human-facing page exists, try to derive the .json endpoint conservatively
                                if (olr.ProductUrl.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                                    jsonUrl = olr.ProductUrl;
                                else
                                    jsonUrl = olr.ProductUrl.TrimEnd('/') + ".json";
                            }

                            if (string.IsNullOrWhiteSpace(jsonUrl))
                                continue;

                            var resp = await _httpClient.GetAsync(jsonUrl);
                            if (!resp.IsSuccessStatusCode)
                            {
                                _logger.LogDebug("OpenLibrary JSON fetch returned {Status} for {Url}", resp.StatusCode, jsonUrl);
                                continue;
                            }

                            var json = await resp.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(json);
                            var root = doc.RootElement;

                            // Description can be a string or an object with a 'value' property
                            try
                            {
                                if (root.TryGetProperty("description", out var descProp))
                                {
                                    if (descProp.ValueKind == JsonValueKind.String)
                                    {
                                        olr.Description = descProp.GetString();
                                    }
                                    else if (descProp.ValueKind == JsonValueKind.Object && descProp.TryGetProperty("value", out var v))
                                    {
                                        olr.Description = v.GetString();
                                    }
                                }
                            }
                            catch (Exception exDesc)
                            {
                                _logger.LogDebug(exDesc, "Failed to parse OpenLibrary description for {Url}", jsonUrl);
                            }

                            // Covers: works/edition JSON commonly expose an array 'covers' of ints
                            try
                            {
                                // Collect possible cover ids/urls
                                List<int> coverIds = new List<int>();
                                string? coverLargeUrl = null;
                                string? coverMediumUrl = null;

                                if (root.TryGetProperty("covers", out var coversProp) && coversProp.ValueKind == JsonValueKind.Array && coversProp.GetArrayLength() > 0)
                                {
                                    foreach (var cp in coversProp.EnumerateArray())
                                    {
                                        if (cp.ValueKind == JsonValueKind.Number && cp.TryGetInt32(out var cid))
                                        {
                                            coverIds.Add(cid);
                                        }
                                    }
                                }
                                else if (root.TryGetProperty("cover", out var coverObj) && coverObj.ValueKind == JsonValueKind.Object)
                                {
                                    if (coverObj.TryGetProperty("large", out var largeProp) && largeProp.ValueKind == JsonValueKind.String)
                                    {
                                        coverLargeUrl = largeProp.GetString();
                                    }
                                    if (coverObj.TryGetProperty("medium", out var medProp) && medProp.ValueKind == JsonValueKind.String)
                                    {
                                        coverMediumUrl = medProp.GetString();
                                    }
                                }

                                // If we have explicit cover ids, attempt to pick the best by measuring aspect ratio
                                if (coverIds.Any())
                                {
                                    try
                                    {
                                        var best = await PickBestCoverUrlAsync(coverIds);
                                        if (!string.IsNullOrWhiteSpace(best))
                                        {
                                            olr.ImageUrl = best;
                                        }
                                        else
                                        {
                                            // fallback to first cover id
                                            olr.ImageUrl = $"https://covers.openlibrary.org/b/id/{coverIds.First()}-L.jpg";
                                        }
                                    }
                                    catch (Exception exPick)
                                    {
                                        _logger.LogDebug(exPick, "Failed to pick best OpenLibrary cover by measurement for {Url}", jsonUrl);
                                        olr.ImageUrl = $"https://covers.openlibrary.org/b/id/{coverIds.First()}-L.jpg";
                                    }
                                }
                                else if (!string.IsNullOrWhiteSpace(coverLargeUrl))
                                {
                                    olr.ImageUrl = coverLargeUrl;
                                }
                                else if (!string.IsNullOrWhiteSpace(coverMediumUrl))
                                {
                                    olr.ImageUrl = coverMediumUrl;
                                }
                            }
                            catch (Exception exCover)
                            {
                                _logger.LogDebug(exCover, "Failed to parse OpenLibrary covers for {Url}", jsonUrl);
                            }

                            // If we found a description or image, ensure metadata source is set
                            if (!string.IsNullOrWhiteSpace(olr.Description) || !string.IsNullOrWhiteSpace(olr.ImageUrl))
                            {
                                olr.MetadataSource = string.IsNullOrWhiteSpace(olr.MetadataSource) ? "OpenLibrary" : olr.MetadataSource;
                                olr.IsEnriched = true;
                                _logger.LogInformation("OpenLibrary enriched candidate {Title} with description/image from {Url}", olr.Title, jsonUrl);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "OpenLibrary enrichment failed for candidate {Title}", olr.Title);
                            continue;
                        }
                    }
                }

                // Deduplicate unified candidate list
                asinCandidates = asinCandidates.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                // Enforce unified candidate cap (if specified) so we don't attempt enrichment on too many ASINs
                if (candidateLimit > 0 && asinCandidates.Count > candidateLimit)
                {
                    _logger.LogInformation("Trimming unified ASIN candidate list from {Before} to candidateLimit={Limit}", asinCandidates.Count, candidateLimit);
                    asinCandidates = asinCandidates.Take(candidateLimit).ToList();
                }
                _logger.LogInformation("Unified ASIN candidate list size: {Count} (amazonCap={AmazonCap}, audibleCap={AudibleCap})", asinCandidates.Count, amazonProviderCap, audibleProviderCap);
                await BroadcastSearchProgressAsync($"Found {asinCandidates.Count} ASIN candidates", null);

                // If we don't have any ASIN candidates, do not return early. Instead allow
                // OpenLibrary-derived candidates (if any) to be merged and processed later
                // so they are combined with ASIN-derived enriched results at the end.
                if (!asinCandidates.Any())
                {
                    _logger.LogInformation("No ASIN candidates found; will rely on OpenLibrary augmentation and later fallback processing if available");
                }

                // Step 3: Get enabled metadata sources ONCE before concurrent enrichment to avoid DbContext threading issues
                _logger.LogInformation("Fetching enabled metadata sources before concurrent enrichment...");
                var metadataSources = await GetEnabledMetadataSourcesAsync();
                _logger.LogInformation("Will use {Count} metadata source(s) for all ASINs", metadataSources.Count);

                // Step 4: Enrich each ASIN with detailed metadata concurrently (limit concurrency)
                var semaphore = new SemaphoreSlim(3); // throttle external fetches
                var enrichmentTasks = new List<Task>();
                var enriched = new ConcurrentBag<SearchResult>();
                var asinsNeedingFallback = new ConcurrentBag<string>();
                // Diagnostic: track per-ASIN disposition / drop reason for easier debugging
                var candidateDropReasons = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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

                            // If this ASIN was added from OpenLibrary augmentation, prefer OpenLibrary metadata
                            if (asinToOpenLibrary.TryGetValue(asin, out var olBook))
                            {
                                try
                                {
                                    _logger.LogInformation("ASIN {Asin} has OpenLibrary augmentation. Considering OL metadata.", asin);

                                    // Build a simple haystack from OL fields and check if the original query
                                    // appears anywhere in those fields. Only show OL-derived results when
                                    // the search value appears in OL metadata (title/author/publisher/subject).
                                    var hayOl = string.Join(" ", new[] {
                                        olBook.Title ?? string.Empty,
                                        olBook.AuthorName != null ? string.Join(" ", olBook.AuthorName) : string.Empty,
                                        olBook.Publisher != null ? string.Join(" ", olBook.Publisher) : string.Empty,
                                        olBook.Subject != null ? string.Join(" ", olBook.Subject) : string.Empty
                                    }.Where(s => !string.IsNullOrWhiteSpace(s)));

                                    var queryMatchesOl = false;
                                    if (!string.IsNullOrWhiteSpace(hayOl) && !string.IsNullOrWhiteSpace(query))
                                    {
                                        queryMatchesOl = hayOl.IndexOf(query ?? string.Empty, StringComparison.OrdinalIgnoreCase) >= 0;
                                    }

                                    if (queryMatchesOl)
                                    {
                                        // Convert OpenLibrary book to AudibleBookMetadata-like object
                                        metadata = new AudibleBookMetadata
                                        {
                                            Asin = asin,
                                            Source = "OpenLibrary",
                                            Title = olBook.Title,
                                            Authors = olBook.AuthorName?.Where(a => !string.IsNullOrWhiteSpace(a)).ToList(),
                                            Publisher = olBook.Publisher != null && olBook.Publisher.Any() ? olBook.Publisher.FirstOrDefault() : null,
                                            PublishYear = olBook.FirstPublishYear?.ToString(),
                                            Description = null,
                                            ImageUrl = olBook.CoverId.HasValue ? $"https://covers.openlibrary.org/b/id/{olBook.CoverId}-L.jpg" : null
                                        };
                                        metadataSourceName = "OpenLibrary";
                                        _logger.LogInformation("Using OpenLibrary metadata for ASIN {Asin} (title: {Title})", asin, olBook.Title);
                                    }
                                    else
                                    {
                                        // OL exists for this ASIN but does not contain the query -> queue for fallback
                                        _logger.LogInformation("OpenLibrary has entry for ASIN {Asin} but OL metadata did not contain the query; queuing for fallback", asin);
                                        asinsNeedingFallback.Add(asin);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to convert OpenLibrary metadata for ASIN {Asin}", asin);
                                }

                                // Skip calling Audimeta/Audnexus for OpenLibrary-augmented ASINs
                                if (metadata != null)
                                {
                                    // proceed to convert metadata below without trying other metadata sources
                                }
                                else
                                {
                                    // move on to next ASIN; no remote metadata calls for OL-derived ASINs
                                    return;
                                }
                            }

                            // Use the pre-fetched metadata sources (avoid DbContext concurrency issues)
                            if (metadata == null && metadataSources.Count > 0)
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
                                            _logger.LogInformation("âœ“ Audimeta returned data for ASIN {Asin}. Title: {Title}", asin, audimetaData.Title ?? "null");
                                            metadata = ConvertAudimetaToMetadata(audimetaData, asin, originalSource ?? "Audible");
                                            metadataSourceName = source.Name; // Store which metadata source succeeded
                                            _logger.LogInformation("Successfully enriched ASIN {Asin} with metadata from {SourceName}", asin, source.Name);
                                            break; // Success! Stop trying other sources
                                        }
                                        else
                                        {
                                            _logger.LogWarning("âœ— Audimeta returned null for ASIN {Asin}", asin);

                                            // Retry once without cache (force audimeta to refresh) when cache lookup fails
                                            try
                                            {
                                                _logger.LogInformation("Audimeta returned null for ASIN {Asin} (cache=true); retrying without cache", asin);
                                                var audimetaRetry = await _audimetaService.GetBookMetadataAsync(asin, "us", false);
                                                if (audimetaRetry != null)
                                                {
                                                    _logger.LogInformation("âœ“ Audimeta returned data for ASIN {Asin} on retry (no-cache). Title: {Title}", asin, audimetaRetry.Title ?? "null");
                                                    metadata = ConvertAudimetaToMetadata(audimetaRetry, asin, originalSource ?? "Audible");
                                                    metadataSourceName = source.Name;
                                                    _logger.LogInformation("Successfully enriched ASIN {Asin} with metadata from {SourceName} (no-cache)", asin, source.Name);
                                                    break;
                                                }
                                                else
                                                {
                                                    _logger.LogWarning("âœ— Audimeta returned null on retry for ASIN {Asin}", asin);
                                                }
                                            }
                                            catch (Exception exRetry)
                                            {
                                                _logger.LogWarning(exRetry, "Audimeta retry without cache failed for ASIN {Asin}", asin);
                                            }
                                        }
                                    }
                                    else if (source.BaseUrl.Contains("audnex.us", StringComparison.OrdinalIgnoreCase))
                                    {
                                        _logger.LogDebug("Calling Audnexus service for ASIN {Asin}", asin);
                                        var audnexusData = await _audnexusService.GetBookMetadataAsync(asin, "us", true, false);

                                        if (audnexusData != null)
                                        {
                                            _logger.LogInformation("âœ“ Audnexus returned data for ASIN {Asin}. Title: {Title}", asin, audnexusData.Title ?? "null");
                                            metadata = ConvertAudnexusToMetadata(audnexusData, asin, originalSource ?? "Audible");
                                            metadataSourceName = source.Name; // Store which metadata source succeeded
                                            _logger.LogInformation("Successfully enriched ASIN {Asin} with metadata from {SourceName}", asin, source.Name);
                                            break; // Success! Stop trying other sources
                                        }
                                        else
                                        {
                                            _logger.LogWarning("âœ— Audnexus returned null for ASIN {Asin}", asin);
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

                            // If all metadata sources failed, try the configured audible metadata scraper as a last
                            // attempt before queuing the ASIN for fallback scraping. Tests commonly replace
                            // IAudibleMetadataService with a deterministic test implementation and expect
                            // it to be used when upstream metadata sources are not configured.
                            if (metadata == null)
                            {
                                try
                                {
                                    if (_audibleMetadataService != null)
                                    {
                                        _logger.LogInformation("No external metadata sources succeeded for ASIN {Asin} - trying audible metadata scraper", asin);
                                        var scrapedMd = await _audibleMetadataService.ScrapeAudibleMetadataAsync(asin);
                                        if (scrapedMd != null)
                                        {
                                            metadata = scrapedMd;
                                            metadataSourceName = "AudibleScrape";
                                            _logger.LogInformation("Audible metadata scraper returned data for ASIN {Asin} (title: {Title})", asin, metadata.Title);
                                        }
                                    }
                                }
                                catch (Exception exScrape)
                                {
                                    _logger.LogWarning(exScrape, "Audible metadata scraper failed for ASIN {Asin}", asin);
                                }

                                if (metadata == null)
                                {
                                    asinsNeedingFallback.Add(asin);
                                    // Note that this ASIN has no metadata yet
                                    try { candidateDropReasons[asin] = "queued_for_fallback_no_metadata"; } catch { }
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
                                    _logger.LogInformation("âœ“ Enriched result for ASIN {Asin} - Title: {Title}, MetadataSource: {MetadataSource}",
                                        asin, enrichedResult.Title ?? "null", metadataSourceName);
                                }
                                else
                                {
                                    _logger.LogWarning("âš  Metadata obtained for ASIN {Asin} but metadataSourceName is null/empty", asin);
                                }

                                enriched.Add(enrichedResult);
                                try { candidateDropReasons[asin] = "enriched_from_metadata"; } catch { }
                            }
                            else
                            {
                                _logger.LogWarning("âœ— No metadata obtained for ASIN {Asin} after trying all sources and scraping", asin);
                                try { candidateDropReasons[asin] = "no_metadata_after_sources"; } catch { }
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

                await Task.WhenAll(enrichmentTasks);

                // Only return enriched items (metadata success); skip basic fallbacks entirely
                var enrichedList = enriched.ToList();
                await BroadcastSearchProgressAsync($"Enrichment complete. Found {enrichedList.Count} enriched results", null);

                // Merge OpenLibrary-derived results (created earlier) into the enriched list so
                // OpenLibrary-only augmentation produces visible, scoreable items without calling Amazon.
                try
                {
                    // Only merge OpenLibrary-derived candidates when we did not obtain any enriched
                    // metadata from external sources. If we already have enriched metadata results
                    // (e.g. from Audible/Amazon/Audimeta/Audnexus or the audible scraper), prefer
                    // those authoritative results and avoid adding OpenLibrary fallbacks that could
                    // dilute the final ranked list.
                    if ((openLibraryDerivedResults != null && openLibraryDerivedResults.Any()) && !enrichedList.Any())
                    {
                        _logger.LogInformation("Merging {Count} OpenLibrary-derived candidate(s) into enriched results", openLibraryDerivedResults.Count);

                        foreach (var ol in openLibraryDerivedResults)
                        {
                            // Basic dedupe: avoid adding items with same Title+Artist
                            var duplicate = enrichedList.Any(e =>
                                // Prefer exact identifier matches (OpenLibrary ID or ASIN)
                                (!string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(ol.Id) && string.Equals(e.Id, ol.Id, StringComparison.OrdinalIgnoreCase))
                                || (!string.IsNullOrWhiteSpace(e.Asin) && !string.IsNullOrWhiteSpace(ol.Asin) && string.Equals(e.Asin, ol.Asin, StringComparison.OrdinalIgnoreCase))
                                // Fallback: Title+Artist equality (defensive)
                                || (string.Equals(e.Title ?? string.Empty, ol.Title ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                                    && string.Equals(e.Artist ?? string.Empty, ol.Artist ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                            );

                            if (!duplicate)
                            {
                                enrichedList.Add(ol);
                                try { candidateDropReasons[(!string.IsNullOrWhiteSpace(ol.Asin) ? ol.Asin : ol.Id)] = "enriched_from_openlibrary"; } catch { }
                                _logger.LogInformation("Added OpenLibrary-derived enriched result: Title='{Title}', Artist='{Artist}'", ol.Title, ol.Artist);
                            }
                            else
                            {
                                _logger.LogDebug("Skipping duplicate OpenLibrary candidate: Title='{Title}', Artist='{Artist}'", ol.Title, ol.Artist);
                            }
                        }

                        await BroadcastSearchProgressAsync($"OpenLibrary augmentation added {openLibraryDerivedResults.Count} candidate(s)", null);

                        // Diagnostic: dump enrichedList immediately after merging OpenLibrary-derived candidates
                        try
                        {
                            var enrichedDumpList = enrichedList.Select(e => string.Format("{0} :: {1} :: {2}", e.Title ?? "<no-title>", e.MetadataSource ?? "<no-md>", string.IsNullOrWhiteSpace(e.Id) ? (e.Asin ?? "<no-id>") : e.Id));
                            var enrichedDump = string.Join(" | ", enrichedDumpList);
                            _logger.LogInformation("Enriched list after OpenLibrary merge ({Count}): {Dump}", enrichedList.Count, enrichedDump);
                        }
                        catch (Exception exDump2)
                        {
                            _logger.LogDebug(exDump2, "Failed to create enrichedList dump after OpenLibrary merge");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to merge OpenLibrary-derived results into enriched list");
                }

                // Last-ditch fallback: scrape product detail pages for ASINs that failed all metadata sources
                try
                {
                    var fallbackAsins = asinsNeedingFallback.Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(a => !string.IsNullOrWhiteSpace(a))
                        .Except(enrichedList.Select(e => e.Asin), StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    if (fallbackAsins.Any())
                    {
                        _logger.LogInformation("Attempting product-page scraping fallback for {Count} ASIN(s)", fallbackAsins.Count);
                        await BroadcastSearchProgressAsync($"Scraping product pages for {fallbackAsins.Count} ASINs", null);

                        var scrapeSemaphore = new SemaphoreSlim(3);
                        var scrapeTasks = new List<Task>();

                        foreach (var fa in fallbackAsins)
                        {
                            scrapeTasks.Add(Task.Run(async () =>
                            {
                                await scrapeSemaphore.WaitAsync();
                                try
                                {
                                    _logger.LogInformation("Scraping product page for ASIN {Asin}", fa);
                                    var scraped = await _amazonSearchService.ScrapeProductPageAsync(fa!);
                                    if (scraped != null)
                                    {
                                        // Skip scraped titles that are clearly promotional or noise (e.g., "Unlock 15% savings")
                                        if (IsTitleNoise(scraped.Title) || IsPromotionalTitle(scraped.Title))
                                        {
                                            _logger.LogInformation("Skipping scraped ASIN {Asin} because title appears promotional/noise: {Title}", fa, scraped.Title);
                                            if (!string.IsNullOrWhiteSpace(fa))
                                            {
                                                try { candidateDropReasons[fa] = "scrape_filtered_promotional_or_noise"; } catch { }
                                            }
                                            return;
                                        }
                                        // Convert scraped data into AudibleBookMetadata-like object for conversion
                                        var metadata = new AudibleBookMetadata
                                        {
                                            Asin = scraped.Asin,
                                            Source = "Amazon",
                                            Title = scraped.Title,
                                            Subtitle = null,
                                            Authors = !string.IsNullOrWhiteSpace(scraped.Author) ? new List<string> { scraped.Author } : null,
                                            Author = scraped.Author,
                                            ImageUrl = scraped.ImageUrl,
                                            PublishYear = scraped.PublishYear,
                                            Publisher = scraped.Publisher,
                                            Version = scraped.Version,
                                            Language = scraped.Language,
                                            Series = scraped.Series,
                                            SeriesNumber = scraped.SeriesNumber,
                                            Runtime = scraped.RuntimeMinutes,
                                            Narrators = !string.IsNullOrWhiteSpace(scraped.Narrator) ? scraped.Narrator.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim()).ToList() : null
                                        };

                                        var enrichedResult = await ConvertMetadataToSearchResultAsync(metadata, fa!, null, null, scraped.ImageUrl);
                                        enrichedResult.IsEnriched = true;
                                        enriched.Add(enrichedResult);
                                        if (!string.IsNullOrWhiteSpace(fa))
                                        {
                                            try { candidateDropReasons[fa] = "scrape_enriched"; } catch { }
                                        }
                                        _logger.LogInformation("Product-page scraping enriched ASIN {Asin} with title={Title}", fa, scraped.Title);
                                    }
                                    else
                                    {
                                        _logger.LogInformation("Product-page scraping returned no useful data for ASIN {Asin}", fa);
                                        if (!string.IsNullOrWhiteSpace(fa))
                                        {
                                            try { candidateDropReasons[fa] = "scrape_no_data"; } catch { }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Product-page scraping failed for ASIN {Asin}", fa);
                                    if (!string.IsNullOrWhiteSpace(fa))
                                    {
                                        try { candidateDropReasons[fa] = "scrape_exception"; } catch { }
                                    }
                                }
                                finally
                                {
                                    scrapeSemaphore.Release();
                                }
                            }));
                        }

                        await Task.WhenAll(scrapeTasks);

                        // Refresh enrichedList after scraping attempts
                        enrichedList = enriched.ToList();
                        await BroadcastSearchProgressAsync($"Scraping fallback complete. Total enriched results now: {enrichedList.Count}", null);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Product-page scraping fallback encountered an error");
                }

                // Compute scores and apply deferred filtering (containment, author/publisher, fuzzy)
                var scored = new List<(SearchResult Result, double Score, double ContainmentScore, double FuzzyScore, string DropReason)>();

                foreach (var r in enrichedList)
                {
                    double containmentScore = 0.0;
                    double fuzzyScore = 0.0;

                    // Always preserve OpenLibrary-sourced items
                    var isOpenLibrary = string.Equals(r.MetadataSource, "OpenLibrary", StringComparison.OrdinalIgnoreCase);

                    // Compute containment and fuzzy similarity based on title/author/description
                    try
                    {
                        containmentScore = ComputeContainmentScore(r, query ?? string.Empty);
                        fuzzyScore = ComputeFuzzySimilarity((r.Title ?? string.Empty) + " " + (r.Artist ?? string.Empty), query ?? string.Empty);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to compute containment/fuzzy scores for ASIN {Asin}", r.Asin);
                    }

                    // Compute completeness: fraction of important fields present
                    int fieldsPresent = 0;
                    if (!string.IsNullOrWhiteSpace(r.Title)) fieldsPresent++;
                    if (!string.IsNullOrWhiteSpace(r.Artist)) fieldsPresent++;
                    if (!string.IsNullOrWhiteSpace(r.Publisher)) fieldsPresent++;
                    if (!string.IsNullOrWhiteSpace(r.ImageUrl)) fieldsPresent++;
                    double completenessScore = fieldsPresent / 4.0;

                    // Source priority: proper metadata sources get higher base multiplier
                    int sourcePriority = 0;
                    if (!string.IsNullOrEmpty(r.MetadataSource))
                    {
                        var md = r.MetadataSource.ToLowerInvariant();
                        if (md.Contains("audimeta") || md.Contains("audnex") || md.Contains("audnexus") || md.Contains("openlibrary")) sourcePriority = 2;
                        else if (md == "amazon" || md == "audible") sourcePriority = 1;
                        else sourcePriority = 1;
                    }

                    // More granular relevance scoring
                    // Features considered:
                    // - Title containment (token containment of query in result fields)
                    // - Title fuzzy similarity (levenshtein-based)
                    // - Exact author match (query tokens appear in artist)
                    // - ASIN exact match (query equals asin)
                    // - Series match (query tokens match series)
                    // - Metadata completeness
                    // - Source priority (proper metadata sources preferred)
                    // - Promotional/noise penalty

                    // Title containment remains the primary signal
                    double titleContainment = containmentScore; // 0.0 - 1.0

                    // Title fuzzy similarity gives a smaller boost for near-miss spellings
                    double titleFuzzy = fuzzyScore; // 0.0 - 1.0

                    // Author match: proportion of query tokens that appear in artist
                    double authorMatch = 0.0;
                    try
                    {
                        var artist = (r.Artist ?? string.Empty);
                        var queryTokens = TokenizeAndNormalize(query ?? string.Empty);
                        if (!string.IsNullOrWhiteSpace(artist) && queryTokens.Any())
                        {
                            var artistTokens = new HashSet<string>(TokenizeAndNormalize(artist), StringComparer.OrdinalIgnoreCase);
                            int matchedAuthor = queryTokens.Count(qt => artistTokens.Contains(qt));
                            authorMatch = Math.Min(1.0, (double)matchedAuthor / queryTokens.Count);
                        }
                    }
                    catch { authorMatch = 0.0; }

                    // ASIN exact match: if the query is exactly an ASIN, strong boost
                    double asinMatch = 0.0;
                    if (!string.IsNullOrWhiteSpace(r.Asin) && !string.IsNullOrWhiteSpace(query) && string.Equals(r.Asin, query.Trim(), StringComparison.OrdinalIgnoreCase))
                        asinMatch = 1.0;

                    // Series match: give small boost if series tokens found
                    double seriesMatch = 0.0;
                    if (!string.IsNullOrWhiteSpace(r.Series) && !string.IsNullOrWhiteSpace(query))
                    {
                        var seriesTokens = new HashSet<string>(TokenizeAndNormalize(r.Series));
                        var qtoks = TokenizeAndNormalize(query);
                        if (qtoks.Any())
                        {
                            var matched = qtoks.Count(qt => seriesTokens.Contains(qt));
                            seriesMatch = Math.Min(1.0, (double)matched / qtoks.Count);
                        }
                    }

                    // Source priority normalized to 0..1 (0 = scraped/low, 1 = proper metadata)
                    double sourcePriorityNormalized = (sourcePriority >= 2) ? 1.0 : (sourcePriority == 1 ? 0.5 : 0.0);

                    // Promotional penalty
                    double promoPenalty = IsPromotionalTitle(r.Title) ? 0.25 : 0.0;

                    // Weights: tuned to prefer title containment and author match, then fuzzy and completeness
                    const double W_TitleContainment = 0.45;
                    const double W_AuthorMatch = 0.18;
                    const double W_TitleFuzzy = 0.12;
                    const double W_Completeness = 0.10;
                    const double W_Source = 0.06;
                    const double W_AsinExact = 0.05;
                    const double W_Series = 0.04;

                    double rawScore =
                        (titleContainment * W_TitleContainment) +
                        (authorMatch * W_AuthorMatch) +
                        (titleFuzzy * W_TitleFuzzy) +
                        (completenessScore * W_Completeness) +
                        (sourcePriorityNormalized * W_Source) +
                        (asinMatch * W_AsinExact) +
                        (seriesMatch * W_Series);

                    // Apply promo penalty (subtract) and clamp to 0..1
                    rawScore = Math.Max(0.0, Math.Min(1.0, rawScore - promoPenalty));

                    // Small extra boost if title fuzzy is very high but containment low
                    if (titleContainment < 0.4 && titleFuzzy > 0.85)
                    {
                        rawScore = Math.Min(1.0, rawScore + (titleFuzzy * 0.1));
                    }

                    double score = rawScore;

                    // Attach computed score to the SearchResult so callers / UI can inspect it
                    try { r.Score = (int)Math.Round(score * 100.0); } catch { }

                    // Default keep reason is empty; we'll set DropReason if filtered out
                    scored.Add((r, score, containmentScore, fuzzyScore, string.Empty));
                }

                var finalList = new List<SearchResult>();

                // Now apply filtering rules
                foreach (var s in scored.OrderByDescending(s => s.Score))
                {
                    var r = s.Result;

                    // OpenLibrary items preserved unless explicitly rejected by requireAuthorAndPublisher
                    if (isOpenLibraryResult(r) && !requireAuthorAndPublisher)
                    {
                        finalList.Add(r);
                        continue;
                    }

                    // Author/publisher requirement
                    if (requireAuthorAndPublisher)
                    {
                        if (string.IsNullOrWhiteSpace(r.Artist) || string.IsNullOrWhiteSpace(r.Publisher))
                        {
                            _logger.LogInformation("Dropping ASIN {Asin} because missing author or publisher", r.Asin);
                            continue;
                        }
                    }

                    // Containment modes
                    var keep = true;
                    if (!string.Equals(containmentMode, "Off", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.Equals(containmentMode, "Strict", StringComparison.OrdinalIgnoreCase))
                        {
                            // Require direct containment (substring) in key fields
                            var hay = string.Join(" ", new[] { r.Title, r.Artist, r.Album, r.Description, r.Publisher, r.Narrator, r.Language, r.Series }.Where(s => !string.IsNullOrEmpty(s))).ToLowerInvariant();
                            if (string.IsNullOrEmpty(hay) || hay.IndexOf(query ?? string.Empty, StringComparison.OrdinalIgnoreCase) < 0)
                            {
                                keep = false;
                                _logger.LogInformation("Dropping ASIN {Asin} (Strict containment failed). containmentScore={Score}, fuzzy={Fuzzy}", r.Asin, s.ContainmentScore, s.FuzzyScore);
                            }
                        }
                        else // Relaxed
                        {
                            // If this result came from an authoritative metadata source
                            // (e.g. Audimeta, Audnexus, Audible scrape, OpenLibrary),
                            // treat it as authoritative and bypass the containment check.
                            var mdLower = (r.MetadataSource ?? string.Empty).ToLowerInvariant();
                            var isAuthoritative = mdLower.Contains("audimeta") || mdLower.Contains("audnex") || mdLower.Contains("audnexus") || mdLower.Contains("audible") || mdLower.Contains("openlibrary");

                            if (isAuthoritative)
                            {
                                keep = true;
                            }
                            else
                            {
                                // Accept if containmentScore >= 0.4 OR fuzzySimilarity >= fuzzyThreshold
                                if (s.ContainmentScore >= 0.4 || s.FuzzyScore >= fuzzyThreshold)
                                {
                                    keep = true;
                                }
                                else
                                {
                                    keep = false;
                                    _logger.LogInformation("Dropping ASIN {Asin} (Relaxed containment failed). containmentScore={Score}, fuzzy={Fuzzy}", r.Asin, s.ContainmentScore, s.FuzzyScore);
                                }
                            }
                        }
                    }

                    if (keep)
                    {
                        finalList.Add(r);
                    }

                    if (finalList.Count >= returnLimit)
                        break;
                }

                results.AddRange(finalList);
                await BroadcastSearchProgressAsync($"Returning {results.Count} final results", null);

                // If still no enriched results, try OpenLibrary derived titles to attempt enrichment again
                if (!results.Any())
                {
                    if (!skipOpenLibrary)
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
                                    var altResults = await _amazonSearchService.SearchAudiobooksAsync(book.Title!);

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
                }

                // Final filter: Keep OpenLibrary-sourced results even if they look noisy;
                // otherwise remove results with problematic titles
                results = results.Where(r =>
                    // Preserve OpenLibrary results regardless of title heuristics
                    (string.Equals(r.MetadataSource, "OpenLibrary", StringComparison.OrdinalIgnoreCase))
                    // For all other results apply the usual title checks AND ensure it looks like an audiobook
                    || (!string.IsNullOrWhiteSpace(r.Title) && !IsTitleNoise(r.Title) && r.Title.Length >= 3 && IsLikelyAudiobook(r))
                ).ToList();

                // Additional filter: if the result does not contain the original search query
                // anywhere in its key fields, drop it. Exempt proper metadata sources
                // (e.g. Audimeta/Audnexus) and OpenLibrary (preserve these).
                if (!string.IsNullOrWhiteSpace(query))
                {
                    var q = query.Trim();
                    results = results.Where(r =>
                    {
                        // Preserve results from authoritative metadata sources (OpenLibrary,
                        // Audimeta/Audnexus) and from the audible scraper. These sources
                        // are considered authoritative enough to bypass the simple query
                        // containment requirement used for raw/conversion results.
                        if (!string.IsNullOrWhiteSpace(r.MetadataSource))
                        {
                            var mdLower = r.MetadataSource.ToLowerInvariant();
                            if (mdLower.Contains("openlibrary") || mdLower.Contains("audimeta") || mdLower.Contains("audnex") || mdLower.Contains("audnexus") || mdLower.Contains("audible"))
                                return true;
                        }

                        // For all other results require the original search query to appear
                        // somewhere in the result's own fields (title/author/description/etc).
                        var hay = string.Join(" ", new[] {
                            r.Title, r.Artist, r.Album, r.Description, r.Publisher, r.Narrator, r.Language, r.Series, r.Quality, r.ProductUrl, r.ImageUrl, r.Asin, r.Source
                        }.Where(s => !string.IsNullOrEmpty(s))).ToLowerInvariant();

                        return hay.IndexOf(q.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) >= 0;
                    }).ToList();
                }

                // Sort results primarily by computed relevance score, then by metadata source priority
                results = results
                    .OrderByDescending(r => r.Score)
                    .ThenByDescending(r =>
                    {
                        if (string.IsNullOrEmpty(r.MetadataSource)) return 0;
                        var md = r.MetadataSource.ToLowerInvariant();
                        if (md.Contains("audimeta") || md.Contains("audnex") || md.Contains("audnexus")) return 3;
                        if (md == "amazon" || md == "audible") return 1;
                        if (string.Equals(md, "openlibrary", StringComparison.OrdinalIgnoreCase)) return 2;
                        return 1;
                    })
                    .ToList();

                // Ensure every unified ASIN candidate has a final disposition reason for diagnostics.
                try
                {
                    var finalAsinEntries = new List<string>();

                    foreach (var asin in asinCandidates)
                    {
                        if (string.IsNullOrWhiteSpace(asin)) continue;

                        // If already accepted in the final results, mark as accepted
                        if (results.Any(r => string.Equals(r.Asin, asin, StringComparison.OrdinalIgnoreCase)))
                        {
                            try { candidateDropReasons[asin] = "accepted"; } catch { }
                            finalAsinEntries.Add($"{asin}:accepted");
                            continue;
                        }

                        // If we have an enriched version but it didn't make the final list, try to compute a specific drop reason
                        var enrichedCandidate = enrichedList.FirstOrDefault(e => string.Equals(e.Asin, asin, StringComparison.OrdinalIgnoreCase));
                        if (enrichedCandidate != null)
                        {
                            // Author/publisher requirement
                            if (requireAuthorAndPublisher && (string.IsNullOrWhiteSpace(enrichedCandidate.Artist) || string.IsNullOrWhiteSpace(enrichedCandidate.Publisher)))
                            {
                                try { candidateDropReasons[asin] = "author_publisher_missing"; } catch { }
                                finalAsinEntries.Add($"{asin}:author_publisher_missing");
                                continue;
                            }

                            // Title noise or unlikely audiobook
                            if (IsTitleNoise(enrichedCandidate.Title) || !IsLikelyAudiobook(enrichedCandidate))
                            {
                                try { candidateDropReasons[asin] = "filtered_title_or_not_likely"; } catch { }
                                finalAsinEntries.Add($"{asin}:filtered_title_or_not_likely");
                                continue;
                            }

                            // Containment / fuzzy failure
                            var containment = 0.0;
                            var fuzzy = 0.0;
                            try
                            {
                                containment = ComputeContainmentScore(enrichedCandidate, query ?? string.Empty);
                                fuzzy = ComputeFuzzySimilarity((enrichedCandidate.Title ?? string.Empty) + " " + (enrichedCandidate.Artist ?? string.Empty), query ?? string.Empty);
                            }
                            catch { }

                            if (string.Equals(containmentMode, "Strict", StringComparison.OrdinalIgnoreCase))
                            {
                                // In strict mode we require direct containment
                                var hay = string.Join(" ", new[] { enrichedCandidate.Title, enrichedCandidate.Artist, enrichedCandidate.Album, enrichedCandidate.Description, enrichedCandidate.Publisher, enrichedCandidate.Narrator, enrichedCandidate.Language, enrichedCandidate.Series }.Where(s => !string.IsNullOrEmpty(s))).ToLowerInvariant();
                                if (string.IsNullOrEmpty(hay) || hay.IndexOf(query ?? string.Empty, StringComparison.OrdinalIgnoreCase) < 0)
                                {
                                    try { candidateDropReasons[asin] = "containment_failed_strict"; } catch { }
                                    finalAsinEntries.Add($"{asin}:containment_failed_strict");
                                    continue;
                                }
                            }
                            else
                            {
                                if (containment < 0.4 && fuzzy < fuzzyThreshold)
                                {
                                    try { candidateDropReasons[asin] = "containment_failed_relaxed"; } catch { }
                                    finalAsinEntries.Add($"{asin}:containment_failed_relaxed");
                                    continue;
                                }
                            }

                            // If none of the above matched, mark as filtered by post-scoring rules
                            try { candidateDropReasons[asin] = "filtered_post_scoring"; } catch { }
                            finalAsinEntries.Add($"{asin}:filtered_post_scoring");
                            continue;
                        }

                        // If we reached here, the ASIN never got enriched nor scraped successfully
                        if (!candidateDropReasons.ContainsKey(asin))
                        {
                            try { candidateDropReasons[asin] = "no_metadata_and_no_scrape"; } catch { }
                        }
                        finalAsinEntries.Add($"{asin}:{candidateDropReasons.GetValueOrDefault(asin)}");
                    }

                    // Emit a consolidated diagnostic log with per-ASIN dispositions
                    if (finalAsinEntries.Any())
                    {
                        _logger.LogInformation("Final ASIN dispositions for query '{Query}': {Entries}", query, string.Join(", ", finalAsinEntries));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to compute final ASIN dispositions for query: {Query}", query);
                }

                // Diagnostic: dump final results (title :: metadataSource :: id/asin) to help correlate
                try
                {
                    var dumpList = results.Select(r => string.Format("{0} :: {1} :: {2}", r.Title ?? "<no-title>", r.MetadataSource ?? "<no-md>", string.IsNullOrWhiteSpace(r.Id) ? (r.Asin ?? "<no-id>") : r.Id));
                    var dump = string.Join(" | ", dumpList);
                    _logger.LogInformation("Final results dump for query {Query}: {Dump}", query, dump);
                }
                catch (Exception exDump)
                {
                    _logger.LogDebug(exDump, "Failed to create final results dump for query: {Query}", query);
                }

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

        private static bool IsPromotionalTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            var t = title.Trim();
            var lower = t.ToLowerInvariant();

            // Percent discounts like '15%'
            if (Regex.IsMatch(t, @"\b\d+%")) return true;

            // Phrases like 'Unlock 15% savings', 'Unlock X%'
            if (lower.Contains("unlock") && (lower.Contains("save") || lower.Contains("savings") || Regex.IsMatch(lower, "\\d+%"))) return true;

            // 'Visit the <brand> Store' promotional links
            if (Regex.IsMatch(lower, "visit the .*store")) return true;

            // Short promo starts
            if (lower.StartsWith("unlock ") || lower.StartsWith("save ") || lower.StartsWith("visit the ")) return true;

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

        // Heuristic: detect product-like titles that are unlikely to be audiobooks
        private static bool IsProductLikeTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            var t = title.Trim();
            var lower = t.ToLowerInvariant();

            // Quick rejects for extremely long descriptive product titles
            if (t.Length > 200) return true;

            // Common product keywords that rarely appear in audiobook titles
            string[] productKeywords = new[]
            {
                "led","lamp","light","charger","battery","watt","volt","usb","hdmi","case","cover","shirt",
                "socks","decor","decoration","decorations","gift","necklace","bracelet","ring","halloween",
                "christmas","remote","plug","adapter","holder","stand","tool","kit","pack of","set of","piece",
                "cm","mm","inch","inches","oz","ml","capacity","dimensions","material","fabric","men's","women's"
            };

            if (productKeywords.Any(k => lower.Contains(k))) return true;

            // Model / pack patterns: 'Pack of 2', 'Set of 3', 'x cm', numeric dimension patterns
            if (Regex.IsMatch(lower, "\\b(pack of|set of|set x|\bx\b|\bpcs?\b|\\bqty\\b|\\bpiece\\b)", RegexOptions.IgnoreCase)) return true;
            if (Regex.IsMatch(t, "\\b\\d{1,4}\\s?(cm|mm|in|inches|oz|ml)\\b", RegexOptions.IgnoreCase)) return true;

            // Titles that contain 'store' in a way that suggests a vendor name are product-like
            if (lower.Contains("store") || lower.Contains("official store") || lower.Contains("visit the")) return true;

            // If title contains many commas and descriptive clauses it's more likely a product listing
            if (t.Count(c => c == ',') >= 3) return true;

            return false;
        }

        // Heuristic: detect if the artist field refers to a store/seller rather than an author
        private static bool IsSellerArtist(string? artist)
        {
            if (string.IsNullOrWhiteSpace(artist)) return false;
            var a = artist.Trim().ToLowerInvariant();
            if (a.Contains("store") || a.Contains("seller") || a.Contains("official store") || a.Contains("shop")) return true;
            if (a.StartsWith("visit the ") && a.EndsWith(" store")) return true;
            return false;
        }

        // Conservative heuristic: determine whether a SearchResult looks like a genuine audiobook
        private static bool IsLikelyAudiobook(SearchResult r)
        {
            if (r == null) return false;

            // If a proper metadata provider enriched it, trust it
            if (!string.IsNullOrWhiteSpace(r.MetadataSource))
            {
                var md = r.MetadataSource.ToLowerInvariant();
                if (md.Contains("audimeta") || md.Contains("audnex") || md.Contains("audnexus") || md.Contains("audible"))
                    return true;
            }

            // If we have runtime in minutes and it's a reasonable audiobook length (>5 minutes)
            if (r.Runtime.HasValue && r.Runtime.Value > 5) return true;

            // Narrator or publisher presence strongly indicates audiobook metadata
            if (!string.IsNullOrWhiteSpace(r.Narrator)) return true;
            if (!string.IsNullOrWhiteSpace(r.Publisher)) return true;

            // Title tokens that explicitly mention audiobook cues
            var title = r.Title ?? string.Empty;
            var lower = title.ToLowerInvariant();
            string[] audioKeywords = new[] { "audiobook", "narrated by", "read by", "full-cast", "full cast", "unabridged", "abridged" };
            if (audioKeywords.Any(k => lower.Contains(k))) return true;

            // Reject if title looks product-like or artist looks like a seller
            if (IsProductLikeTitle(title) || IsSellerArtist(r.Artist)) return false;

            // Fallback: if title is short and contains no product keywords, treat as possible audiobook
            if (!string.IsNullOrWhiteSpace(title) && title.Length < 120 && !IsProductLikeTitle(title)) return true;

            return false;
        }

        // Tokenize and normalize a string for containment and fuzzy matching.
        // Preserves hyphenated tokens (e.g. "sg-1") as requested.
        private static List<string> TokenizeAndNormalize(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return new List<string>();
            // Lowercase
            var s = input.ToLowerInvariant();
            // Replace punctuation except hyphen with spaces
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || char.IsWhiteSpace(c))
                    sb.Append(c);
                else
                    sb.Append(' ');
            }

            // Split on whitespace and remove empty tokens
            var tokens = sb.ToString().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 0)
                .ToList();

            return tokens;
        }

        // Compute a containment score between 0.0 - 1.0 representing how much the query
        // tokens are present in the result's combined fields. 1.0 = all tokens present.
        private static double ComputeContainmentScore(SearchResult result, string query)
        {
            if (result == null || string.IsNullOrWhiteSpace(query)) return 0.0;

            var hay = string.Join(" ", new[] { result.Title, result.Artist, result.Album, result.Description, result.Publisher, result.Narrator, result.Language, result.Series }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

            var hayTokens = TokenizeAndNormalize(hay);
            var queryTokens = TokenizeAndNormalize(query);

            if (!queryTokens.Any()) return 0.0;

            int matched = 0;
            var haySet = new HashSet<string>(hayTokens, StringComparer.OrdinalIgnoreCase);
            foreach (var qt in queryTokens)
            {
                if (haySet.Contains(qt)) matched++;
            }

            // Partial credit for hyphen-insensitive matches (e.g., sg-1 vs sg)
            // Also check for substring matches of query tokens in hay tokens.
            for (int i = 0; i < queryTokens.Count; i++)
            {
                var qt = queryTokens[i];
                if (haySet.Contains(qt)) continue;
                foreach (var ht in haySet)
                {
                    if (ht.Contains(qt) || qt.Contains(ht))
                    {
                        matched += 1; // give partial match same weight as token match
                        break;
                    }
                }
            }

            var score = Math.Min(1.0, (double)matched / Math.Max(1, queryTokens.Count));
            return score;
        }

        // Compute fuzzy similarity (0.0 - 1.0) based on normalized Levenshtein distance
        private static double ComputeFuzzySimilarity(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b)) return 1.0;
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0.0;

            var sa = NormalizeForFuzzy(a);
            var sb = NormalizeForFuzzy(b);
            var dist = LevenshteinDistance(sa, sb);
            var max = Math.Max(sa.Length, sb.Length);
            if (max == 0) return 1.0;
            var similarity = 1.0 - ((double)dist / max);
            return Math.Max(0.0, Math.Min(1.0, similarity));
        }

        private static string NormalizeForFuzzy(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;
            var lowered = s.ToLowerInvariant();
            // Remove punctuation except hyphen
            var sb = new System.Text.StringBuilder(lowered.Length);
            foreach (var c in lowered)
            {
                if (char.IsLetterOrDigit(c) || c == '-') sb.Append(c);
            }
            return sb.ToString();
        }

        // Standard Levenshtein distance implementation
        private static int LevenshteinDistance(string s, string t)
        {
            if (s == t) return 0;
            if (string.IsNullOrEmpty(s)) return t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            var n = s.Length;
            var m = t.Length;
            var d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; d[i, 0] = i++) { }
            for (int j = 0; j <= m; d[0, j] = j++) { }

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        private static bool isOpenLibraryResult(SearchResult r)
        {
            return string.Equals(r?.MetadataSource, "OpenLibrary", StringComparison.OrdinalIgnoreCase);
        }

        // Try to pick the best cover URL from a list of OpenLibrary cover IDs by measuring image aspect ratios.
        // Returns a full covers.openlibrary.org URL or null on failure.
        private async Task<string?> PickBestCoverUrlAsync(List<int> coverIds)
        {
            if (coverIds == null || !coverIds.Any()) return null;

            double bestDelta = double.MaxValue;
            string? bestUrl = null;

            foreach (var cid in coverIds)
            {
                try
                {
                    var url = $"https://covers.openlibrary.org/b/id/{cid}-L.jpg";
                    using var resp = await _httpClient.GetAsync(url);
                    if (!resp.IsSuccessStatusCode) continue;
                    using var ms = new System.IO.MemoryStream(await resp.Content.ReadAsByteArrayAsync());
                    try
                    {
                        // Use ImageSharp to measure image dimensions in a cross-platform way
                        using var img = Image.Load(ms);
                        if (img.Height == 0) continue;
                        var ratio = (double)img.Width / img.Height;
                        var delta = Math.Abs(ratio - 1.0);
                        if (delta < bestDelta)
                        {
                            bestDelta = delta;
                            bestUrl = url;
                        }
                        // If exactly 1:1, short-circuit
                        if (Math.Abs(delta) < 0.01)
                            break;
                    }
                    catch (Exception imgEx)
                    {
                        _logger.LogDebug(imgEx, "Failed to measure image dimensions for cover {Url}", url);
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to fetch cover image for id {Id}", cid);
                    continue;
                }
            }

            return bestUrl;
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
                // Sanitize the query for indexer searches to remove illegal characters
                query = SanitizeIndexerQuery(query);
                _logger.LogInformation("Searching indexer {Name} ({Implementation}) for: {Query}", indexer.Name, indexer.Implementation, query);

                // Route to appropriate search method based on implementation

                // Compute a single fallback name to use when indexer.Name is empty
                string fallbackName;
                if (!string.IsNullOrWhiteSpace(indexer.Name))
                {
                    fallbackName = indexer.Name;
                }
                else if (!string.IsNullOrWhiteSpace(indexer.Implementation))
                {
                    fallbackName = indexer.Implementation;
                }
                else
                {
                    try
                    {
                        var baseUrl = indexer.Url?.TrimEnd('/') ?? string.Empty;
                        var baseUri = new Uri(baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? baseUrl : "https://" + baseUrl);
                        fallbackName = baseUri.Host;
                    }
                    catch
                    {
                        fallbackName = "Indexer";
                    }
                }

                if (indexer.Implementation.Equals("InternetArchive", StringComparison.OrdinalIgnoreCase))
                {
                    var iaResults = await SearchInternetArchiveAsync(indexer, query, category);
                    // Ensure Source is set for all results
                    foreach (var r in iaResults)
                    {
                        if (string.IsNullOrWhiteSpace(r.Source)) r.Source = fallbackName;
                    }
                    return iaResults;
                }
                else if (indexer.Implementation.Equals("MyAnonamouse", StringComparison.OrdinalIgnoreCase))
                {
                    var mamResults = await SearchMyAnonamouseAsync(indexer, query, category);
                    foreach (var r in mamResults)
                    {
                        if (string.IsNullOrWhiteSpace(r.Source)) r.Source = fallbackName;
                    }
                    return mamResults;
                }
                else
                {
                    var tnResults = await SearchTorznabNewznabAsync(indexer, query, category);
                    foreach (var r in tnResults)
                    {
                        if (string.IsNullOrWhiteSpace(r.Source)) r.Source = fallbackName;
                    }
                    return tnResults;
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
                // Build Torznab/Newznab API URL (redact api keys before logging)
                var url = BuildTorznabUrl(indexer, query, category);
                _logger.LogDebug("Indexer API URL: {Url}", LogRedaction.RedactText(url, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { indexer.ApiKey ?? string.Empty })));

                // Make HTTP request with User-Agent header
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                var version = typeof(SearchService).Assembly.GetName().Version?.ToString() ?? "0.0.0";
                var userAgent = $"Listenarr/{version} (+https://github.com/therobbiedavis/listenarr)";
                request.Headers.UserAgent.ParseAdd(userAgent);
                
                var response = await _httpClient.SendAsync(request);
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

                // Parse mam_id from AdditionalSettings (robust: case-insensitive and nested)
                var mamId = MyAnonamouseHelper.TryGetMamId(indexer.AdditionalSettings);

                if (string.IsNullOrEmpty(mamId))
                {
                    _logger.LogWarning("MyAnonamouse indexer {Name} missing mam_id", indexer.Name);
                    return new List<SearchResult>();
                }

                // Build MyAnonamouse API request (mam_id is sent as a cookie)
                // Use the JSON form endpoint with application/x-www-form-urlencoded payload
                var url = $"{indexer.Url.TrimEnd('/')}/tor/js/loadSearchJSONbasic.php";

                // Try to parse title/author from the query to give MyAnonamouse more targeted fields
                var (parsedTitle, parsedAuthor) = ParseTitleAuthorFromQuery(query);

                // Decide searchType: prefer a targeted search when we only have title or only author
                var searchType = "all";
                if (!string.IsNullOrWhiteSpace(parsedTitle) && string.IsNullOrWhiteSpace(parsedAuthor)) searchType = "title";
                if (string.IsNullOrWhiteSpace(parsedTitle) && !string.IsNullOrWhiteSpace(parsedAuthor)) searchType = "author";

                // Build JSON payload according to new MyAnonamouse structure
                // Build tor object to mirror the browse.php parameter shapes (tor[text], tor[srchIn][field]=true, tor[cat][]=...)
                var torObject = new Dictionary<string, object>
                {
                    ["text"] = query,
                    // Use field->true mapping like browse.php (e.g. tor[srchIn][title]=true)
                    ["srchIn"] = new Dictionary<string, bool>
                    {
                        ["title"] = true,
                        ["author"] = true,
                        ["narrator"] = true,
                        ["series"] = true,
                        ["description"] = true,
                        ["filetype"] = true
                    },
                    ["searchType"] = searchType,
                    ["searchIn"] = "torrents",
                    // Keep explicit cat[] list copied from the browse URL
                    ["cat"] = new[] { "39", "49", "50", "83", "51", "97", "40", "41", "106", "42", "52", "98", "54", "55", "43", "99", "84", "44", "56", "45", "57", "85", "87", "119", "88", "58", "59", "46", "47", "53", "89", "100", "108", "48", "111", "0" },
                    // Keep main_cat for explicit audiobook focus (some handlers honor it)
                    ["main_cat"] = new[] { "13" },
                    // Additional browse.php parameters observed in the URL
                    ["browse_lang"] = new[] { "1" },
                    ["browseFlagsHideVsShow"] = "0",
                    ["unit"] = "1",
                    ["startDate"] = string.Empty,
                    ["endDate"] = string.Empty,
                    ["hash"] = string.Empty,
                    ["sortType"] = "default",
                    ["startNumber"] = "0",
                    ["perpage"] = "100"
                };

                if (!string.IsNullOrWhiteSpace(parsedTitle))
                {
                    torObject["title"] = parsedTitle;
                }

                if (!string.IsNullOrWhiteSpace(parsedAuthor))
                {
                    torObject["author"] = parsedAuthor;
                }

                // Build form-encoded content following the example for loadSearchJSONbasic.php
                // Example: tor[cat][]=0&tor[sortType]=default&tor[browseStart]=true&tor[startNumber]=0&bannerLink&bookmarks&dlLink&description&tor[text]=mp3%20m4a
                var formPairs = new List<KeyValuePair<string, string>>();
                // Minimal/required fields per example
                formPairs.Add(new KeyValuePair<string, string>("tor[cat][]", "0"));
                formPairs.Add(new KeyValuePair<string, string>("tor[sortType]", "default"));
                formPairs.Add(new KeyValuePair<string, string>("tor[browseStart]", "true"));
                formPairs.Add(new KeyValuePair<string, string>("tor[startNumber]", "0"));

                // Keys present without explicit values in the example; represent them with empty string
                formPairs.Add(new KeyValuePair<string, string>("bannerLink", string.Empty));
                formPairs.Add(new KeyValuePair<string, string>("bookmarks", string.Empty));
                formPairs.Add(new KeyValuePair<string, string>("dlLink", string.Empty));
                formPairs.Add(new KeyValuePair<string, string>("description", string.Empty));

                // tor[text] is the search query (example uses 'mp3 m4a')
                formPairs.Add(new KeyValuePair<string, string>("tor[text]", query ?? string.Empty));

                // Preserve audiobook filtering if available: include main_cat
                formPairs.Add(new KeyValuePair<string, string>("tor[main_cat][]", "13"));

                // Add searchIn and srchIn fields so we request torrents and relevant fields
                formPairs.Add(new KeyValuePair<string, string>("tor[searchIn]", "torrents"));
                formPairs.Add(new KeyValuePair<string, string>("tor[srchIn][title]", "true"));
                formPairs.Add(new KeyValuePair<string, string>("tor[srchIn][author]", "true"));
                formPairs.Add(new KeyValuePair<string, string>("tor[srchIn][narrator]", "true"));
                formPairs.Add(new KeyValuePair<string, string>("tor[srchIn][series]", "true"));

                var content = new FormUrlEncodedContent(formPairs);
                var formString = await content.ReadAsStringAsync();
                _logger.LogInformation("MyAnonamouse outgoing form (loadSearchJSONbasic): {Form}", formString);

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = content
                };

                // Add browser-like headers to avoid "invalid request" errors
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                request.Headers.Referrer = new Uri("https://www.myanonamouse.net/");

                using var httpClient = MyAnonamouseHelper.CreateAuthenticatedHttpClient(mamId, indexer.Url);
                _logger.LogDebug("MyAnonamouse API URL: {Url}", LogRedaction.RedactText(url, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { indexer.ApiKey ?? string.Empty })));

                var response = await httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("MyAnonamouse returned status {Status}", response.StatusCode);
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("MyAnonamouse error response: {Content}", LogRedaction.RedactText(errorContent, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { indexer.ApiKey ?? string.Empty })));
                    return new List<SearchResult>();
                }

                var jsonResponse = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("MyAnonamouse raw response: {Response}", jsonResponse);
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
                _logger.LogDebug("Parsing MyAnonamouse response, length: {Length}", jsonResponse.Length);

                JsonDocument? doc = null;
                JsonElement dataArrayElement = default;

                // Try to parse JSON directly. If that fails, try to extract the first JSON array substring.
                try
                {
                    doc = JsonDocument.Parse(jsonResponse);
                }
                catch (Exception)
                {
                    // Attempt to extract a JSON array from an HTML-wrapped response or stray text
                    var start = jsonResponse.IndexOf('[');
                    var end = jsonResponse.LastIndexOf(']');
                    if (start >= 0 && end > start)
                    {
                        var sub = jsonResponse.Substring(start, end - start + 1);
                        try
                        {
                            doc = JsonDocument.Parse(sub);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to parse extracted JSON array from MyAnonamouse response");
                            return results;
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Unable to locate JSON array in MyAnonamouse response");
                        return results;
                    }
                }

                var root = doc!.RootElement;

                // Support multiple response shapes:
                // 1) Root is an array of items
                // 2) Root is an object with property "data" containing array
                // 3) Root is an object with property "parsed" or "results" or "items"
                if (root.ValueKind == JsonValueKind.Array)
                {
                    dataArrayElement = root;
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("data", out var tmp) && tmp.ValueKind == JsonValueKind.Array)
                    {
                        dataArrayElement = tmp;
                    }
                    else if (root.TryGetProperty("parsed", out tmp) && tmp.ValueKind == JsonValueKind.Array)
                    {
                        dataArrayElement = tmp;
                    }
                    else if (root.TryGetProperty("results", out tmp) && tmp.ValueKind == JsonValueKind.Array)
                    {
                        dataArrayElement = tmp;
                    }
                    else if (root.TryGetProperty("items", out tmp) && tmp.ValueKind == JsonValueKind.Array)
                    {
                        dataArrayElement = tmp;
                    }
                    else
                    {
                        // As a last resort, try to find the first array value anywhere in the object
                        foreach (var prop in root.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                dataArrayElement = prop.Value;
                                break;
                            }
                        }

                        if (dataArrayElement.ValueKind == JsonValueKind.Undefined)
                        {
                            _logger.LogWarning("MyAnonamouse response did not contain an expected array property. Response preview: {Preview}", LogRedaction.RedactText(jsonResponse.Length > 500 ? jsonResponse.Substring(0, 500) + "..." : jsonResponse, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { indexer.ApiKey ?? string.Empty })));
                            return results;
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Unexpected MyAnonamouse root JSON kind: {Kind}", root.ValueKind);
                    return results;
                }

                _logger.LogDebug("Found {Count} MyAnonamouse results", dataArrayElement.GetArrayLength());
                try
                {
                    if (dataArrayElement.GetArrayLength() > 0)
                    {
                        var firstRaw = dataArrayElement[0].ToString();
                        var preview = firstRaw.Length > 400 ? firstRaw.Substring(0, 400) + "..." : firstRaw;
                        _logger.LogDebug("First MyAnonamouse item preview: {Preview}", LogRedaction.RedactText(preview, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { indexer.ApiKey ?? string.Empty })));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to produce preview of first MyAnonamouse item");
                }

                int _mamDebugIndex = 0;
                foreach (var item in dataArrayElement.EnumerateArray())
                {
                    try
                    {
                        string id;
                        if (item.TryGetProperty("id", out var idElem))
                        {
                            id = idElem.ValueKind == JsonValueKind.String ? idElem.GetString() ?? "" : idElem.ToString();
                        }
                        else
                        {
                            id = Guid.NewGuid().ToString();
                        }

                        // MyAnonamouse uses "title" in responses; fall back to "name" if needed
                        var title = "";
                        if (item.TryGetProperty("title", out var titleElem))
                        {
                            title = titleElem.GetString() ?? "";
                        }
                        else if (item.TryGetProperty("name", out titleElem))
                        {
                            title = titleElem.GetString() ?? "";
                        }
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

                        // (debug log moved later after we build the result so all fields exist)

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
                            // Use MyAnonamouse public item page pattern: https://myanonamouse.net/t/{id}
                            ResultUrl = !string.IsNullOrEmpty(id) ? $"https://myanonamouse.net/t/{Uri.EscapeDataString(id)}" : indexer.Url,
                            MagnetLink = "",
                            NzbUrl = ""
                        };
                        result.IndexerId = indexer.Id;
                        result.IndexerImplementation = indexer.Implementation;
                        // Robust link detection: prefer magnet/hash/torrent indicators, only treat as NZB when explicit NZB fields exist
                        try
                        {
                            string magnetLink = "";
                            // Common magnet field names
                            if (item.TryGetProperty("magnet", out var magnetElem) && magnetElem.ValueKind == JsonValueKind.String)
                                magnetLink = magnetElem.GetString() ?? "";
                            else if (item.TryGetProperty("magnetLink", out magnetElem) && magnetElem.ValueKind == JsonValueKind.String)
                                magnetLink = magnetElem.GetString() ?? "";
                            else if (item.TryGetProperty("magnetlink", out magnetElem) && magnetElem.ValueKind == JsonValueKind.String)
                                magnetLink = magnetElem.GetString() ?? "";

                            // If we have a torrent hash, construct a magnet link
                            if (string.IsNullOrEmpty(magnetLink) && item.TryGetProperty("hash", out var hashElem) && hashElem.ValueKind == JsonValueKind.String)
                            {
                                var h = hashElem.GetString();
                                if (!string.IsNullOrWhiteSpace(h))
                                {
                                    magnetLink = $"magnet:?xt=urn:btih:{h}&dn={Uri.EscapeDataString(title ?? string.Empty)}";
                                }
                            }

                            // Detect torrent download URL from other common fields
                            var torrentUrlDetected = result.TorrentUrl ?? string.Empty;
                            string[] torrentFields = new[] { "download", "dlLink", "downloadlink", "download_url", "torrent", "torrent_url", "torrentUrl", "torrentlink" };
                            foreach (var tf in torrentFields)
                            {
                                if (string.IsNullOrEmpty(torrentUrlDetected) && item.TryGetProperty(tf, out var tfElem) && tfElem.ValueKind == JsonValueKind.String)
                                {
                                    torrentUrlDetected = tfElem.GetString() ?? string.Empty;
                                }
                            }

                            // If any URL looks like a .torrent file, prefer it as torrent URL
                            if (string.IsNullOrEmpty(torrentUrlDetected))
                            {
                                foreach (var prop in item.EnumerateObject())
                                {
                                    if (prop.Value.ValueKind == JsonValueKind.String)
                                    {
                                        var v = prop.Value.GetString();
                                        if (!string.IsNullOrEmpty(v) && v.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                                        {
                                            torrentUrlDetected = v;
                                            break;
                                        }
                                    }
                                }
                            }

                            // Detect NZB fields (only treat as NZB when explicit)
                            string nzbUrlDetected = string.Empty;
                            if (item.TryGetProperty("nzb", out var nzbElem) && nzbElem.ValueKind == JsonValueKind.String)
                                nzbUrlDetected = nzbElem.GetString() ?? string.Empty;
                            else if (item.TryGetProperty("nzbLink", out nzbElem) && nzbElem.ValueKind == JsonValueKind.String)
                                nzbUrlDetected = nzbElem.GetString() ?? string.Empty;
                            else if (item.TryGetProperty("nzburl", out nzbElem) && nzbElem.ValueKind == JsonValueKind.String)
                                nzbUrlDetected = nzbElem.GetString() ?? string.Empty;

                            // Apply discovered links to the result
                            if (!string.IsNullOrEmpty(magnetLink)) result.MagnetLink = magnetLink;
                            if (!string.IsNullOrEmpty(torrentUrlDetected)) result.TorrentUrl = torrentUrlDetected;
                            if (!string.IsNullOrEmpty(nzbUrlDetected)) result.NzbUrl = nzbUrlDetected;

                            // Prefer marking as Torrent when either magnet or torrent URL exists
                            if (!string.IsNullOrEmpty(result.MagnetLink) || !string.IsNullOrEmpty(result.TorrentUrl))
                                result.Format = "Torrent";
                            else if (!string.IsNullOrEmpty(result.NzbUrl))
                                result.Format = "NZB";

                            _logger.LogDebug("MyAnonamouse parsed item #{Index} link-disposition: magnet={MagnetPresent}, torrent={TorrentPresent}, nzb={NzbPresent}", _mamDebugIndex, !string.IsNullOrEmpty(result.MagnetLink), !string.IsNullOrEmpty(result.TorrentUrl), !string.IsNullOrEmpty(result.NzbUrl));
                        }
                        catch (Exception exLink)
                        {
                            _logger.LogDebug(exLink, "Failed to detect links for MyAnonamouse item {Id}", id);
                        }

                        // Attempt to parse language codes from title or tags (e.g. [ENG / M4B])
                        var detectedLang = ParseLanguageFromText(title + " " + (tags ?? ""));
                        if (!string.IsNullOrEmpty(detectedLang))
                        {
                            result.Language = detectedLang;
                        }

                        try
                        {
                            if (_mamDebugIndex < 5)
                            {
                                _logger.LogDebug("ParseMyAnonamouse: constructed SearchResult #{Index} -> Id='{Id}', Title='{Title}', Size={Size}, Seeders={Seeders}, TorrentUrl='{TorrentUrl}', Artist='{Artist}', Album='{Album}', Category='{Category}', Source='{Source}'",
                                    _mamDebugIndex, result.Id, result.Title, result.Size, result.Seeders, result.TorrentUrl ?? "", result.Artist ?? "", result.Album ?? "", result.Category ?? "", result.Source ?? "");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to write debug log for constructed MyAnonamouse SearchResult");
                        }

                        _mamDebugIndex++;

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

        // Recursively search a JsonElement for a mam_id-like property (case-insensitive)
        private string? FindMamIdInJson(JsonElement element)
        {
            // Keys to look for
            var keys = new[] { "mam_id", "mamid", "mamId", "mamID", "mam" };

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in element.EnumerateObject())
                {
                    try
                    {
                        if (keys.Any(k => string.Equals(prop.Name, k, StringComparison.OrdinalIgnoreCase)))
                        {
                            if (prop.Value.ValueKind == JsonValueKind.String)
                                return prop.Value.GetString();
                        }

                        // Recurse into objects and arrays
                        if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            var found = FindMamIdInJson(prop.Value);
                            if (!string.IsNullOrEmpty(found)) return found;
                        }
                    }
                    catch { /* ignore malformed inner values */ }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    var found = FindMamIdInJson(item);
                    if (!string.IsNullOrEmpty(found)) return found;
                }
            }

            return null;
        }

        // Try to heuristically split a user query into (title, author).
        // Supports patterns like: "Title by Author", "Title - Author", or "Author, Title".
        private static (string? title, string? author) ParseTitleAuthorFromQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return (null, null);

            var q = query.Trim();

            // Pattern: "Title by Author" (use last occurrence of " by ")
            var byIndex = q.LastIndexOf(" by ", StringComparison.OrdinalIgnoreCase);
            if (byIndex > 0)
            {
                var title = q.Substring(0, byIndex).Trim();
                var author = q.Substring(byIndex + 4).Trim();
                return (string.IsNullOrWhiteSpace(title) ? null : title, string.IsNullOrWhiteSpace(author) ? null : author);
            }

            // Pattern: "Title - Author"
            var dashParts = q.Split(new[] { " - " }, 2, StringSplitOptions.None);
            if (dashParts.Length == 2)
            {
                var title = dashParts[0].Trim();
                var author = dashParts[1].Trim();
                return (string.IsNullOrWhiteSpace(title) ? null : title, string.IsNullOrWhiteSpace(author) ? null : author);
            }

            // Pattern: "Author, Title" -> return (Title, Author)
            var commaParts = q.Split(new[] { ',' }, 2);
            if (commaParts.Length == 2)
            {
                var author = commaParts[0].Trim();
                var title = commaParts[1].Trim();
                return (string.IsNullOrWhiteSpace(title) ? null : title, string.IsNullOrWhiteSpace(author) ? null : author);
            }

            return (null, null);
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

        // Try to extract host from a URL; fallback to the raw url or a generic label
        private string TryGetHostFromUrl(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return "Indexer";
            try
            {
                var url = rawUrl.Trim();
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    url = "https://" + url;
                var u = new Uri(url);
                return u.Host;
            }
            catch
            {
                return rawUrl ?? "Indexer";
            }
        }

        /// <summary>
        /// Remove illegal/unsupported characters from indexer search queries.
        /// Strips a curated set of punctuation/symbols, smart quotes, control
        /// and formatting Unicode categories, then collapses whitespace.
        /// </summary>
        private string SanitizeIndexerQuery(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return string.Empty;

            // Characters explicitly requested to strip
            // Added parentheses to remove '(' and ')' from queries
            const string forbidden = "*/\\<>:?|^~`$#%&+={}[]'\"!()";

            var sb = new System.Text.StringBuilder(query.Length);
            foreach (var ch in query)
            {
                // Remove control and format characters
                var uc = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch);
                if (char.IsControl(ch) || uc == System.Globalization.UnicodeCategory.Format)
                    continue;

                // Remove explicit forbidden ASCII symbols
                if (forbidden.IndexOf(ch) >= 0)
                    continue;

                // Remove common smart quotes and other punctuation variants
                // Left/right single quotation mark, left/right double quotation mark
                if (ch == '\u2018' || ch == '\u2019' || ch == '\u201C' || ch == '\u201D')
                    continue;

                sb.Append(ch);
            }

            // Collapse runs of whitespace to single space and trim
            var cleaned = System.Text.RegularExpressions.Regex.Replace(sb.ToString(), "\\s+", " ").Trim();
            return cleaned;
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
                            // Internet Archive item page
                            ResultUrl = !string.IsNullOrEmpty(identifier) ? $"https://archive.org/details/{identifier}" : null,
                            DownloadType = "DDL", // Direct Download Link
                            Format = audioFile.Format,
                            Quality = DetectQualityFromFormat(audioFile.Format),
                            Source = $"{indexer.Name} (Internet Archive)"
                        };

                        // Ensure ResultUrl is present (fallback to item page or archive details)
                        if (string.IsNullOrEmpty(iaResult.ResultUrl) && !string.IsNullOrEmpty(identifier))
                        {
                            iaResult.ResultUrl = $"https://archive.org/details/{identifier}";
                        }

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
                        result.IndexerId = indexer.Id;
                        result.IndexerImplementation = indexer.Implementation;

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
                        var linkElem = item.Element("link")?.Value;
                        if (!string.IsNullOrEmpty(linkElem))
                        {
                            if (linkElem.StartsWith("magnet:") && string.IsNullOrEmpty(result.MagnetLink) && !isUsenet)
                            {
                                result.MagnetLink = linkElem;
                            }
                            else
                            {
                                // Use the link element as the canonical indexer page when possible
                                if (Uri.IsWellFormedUriString(linkElem, UriKind.Absolute))
                                {
                                    result.ResultUrl = linkElem;
                                }

                                // If torrentUrl is empty, prefer the link
                                if (string.IsNullOrEmpty(result.TorrentUrl) && !linkElem.StartsWith("magnet:") && !isUsenet)
                                {
                                    result.TorrentUrl = linkElem;
                                }
                                else if (string.IsNullOrEmpty(result.NzbUrl) && isUsenet && !linkElem.StartsWith("magnet:"))
                                {
                                    result.NzbUrl = linkElem;
                                }
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
                        var titleParts = result.Title.Split(new[] { " - ", " â€“ " }, StringSplitOptions.RemoveEmptyEntries);
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

            // Handle formats like "500 MB", "1.2 GB", "1024 KB", "3.7 GiB", "279.0 MiB", etc.
            // Support both decimal (KB/MB/GB/TB) and binary (KiB/MiB/GiB/TiB) units
            var match = System.Text.RegularExpressions.Regex.Match(sizeStr, @"^([\d\.]+)\s*(KiB|MiB|GiB|TiB|KB|MB|GB|TB|B)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
                {
                    var unit = match.Groups[2].Value.ToUpper();
                    return unit switch
                    {
                        "B" => (long)value,
                        "KB" => (long)(value * 1000),
                        "MB" => (long)(value * 1000 * 1000),
                        "GB" => (long)(value * 1000 * 1000 * 1000),
                        "TB" => (long)(value * 1000 * 1000 * 1000 * 1000),
                        "KIB" => (long)(value * 1024),
                        "MIB" => (long)(value * 1024 * 1024),
                        "GIB" => (long)(value * 1024 * 1024 * 1024),
                        "TIB" => (long)(value * 1024 * 1024 * 1024 * 1024),
                        _ => (long)value
                    };
                }
            }

            _logger.LogWarning("Unable to parse size string: '{SizeStr}'", sizeStr);
            return 0;
        }

        // (Helper methods for containment and fuzzy scoring are implemented above.)

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


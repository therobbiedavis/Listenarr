using Listenarr.Api.Models;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Listenarr.Api.Services
{
    public class SearchService : ISearchService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configurationService;
        private readonly ILogger<SearchService> _logger;
        private readonly IAudibleMetadataService _audibleMetadataService;
        private readonly IOpenLibraryService _openLibraryService;
        private readonly IAmazonSearchService _amazonSearchService;
        private readonly IAudibleSearchService _audibleSearchService;
        private readonly IImageCacheService _imageCacheService;

        public SearchService(
            HttpClient httpClient, 
            IConfigurationService configurationService, 
            ILogger<SearchService> logger, 
            IAudibleMetadataService audibleMetadataService,
            IOpenLibraryService openLibraryService,
            IAmazonSearchService amazonSearchService,
            IAudibleSearchService audibleSearchService,
            IImageCacheService imageCacheService)
        {
            _httpClient = httpClient;
            _configurationService = configurationService;
            _logger = logger;
            _audibleMetadataService = audibleMetadataService;
            _openLibraryService = openLibraryService;
            _amazonSearchService = amazonSearchService;
            _audibleSearchService = audibleSearchService;
            _imageCacheService = imageCacheService;
        }

        public async Task<List<SearchResult>> SearchAsync(string query, string? category = null, List<string>? apiIds = null)
        {
            var results = new List<SearchResult>();

            // Only use intelligent search - no traditional API fallbacks
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
                return fallback.OrderByDescending(r => r.Seeders).ThenBy(r => r.Size).ToList();
            }

            return results.OrderByDescending(r => r.Seeders).ThenBy(r => r.Size).ToList();
        }

        private async Task<List<SearchResult>> IntelligentSearchAsync(string query)
        {
            var results = new List<SearchResult>();

            try
            {
                _logger.LogInformation("Starting intelligent search for: {Query}", query);

                // Step 1: Parallel search Amazon & Audible
                _logger.LogInformation("Searching Amazon and Audible for: {Query}", query);
                var amazonTask = _amazonSearchService.SearchAudiobooksAsync(query);
                var audibleTask = _audibleSearchService.SearchAudiobooksAsync(query);
                await Task.WhenAll(amazonTask, audibleTask);

                var amazonResults = amazonTask.Result;
                var audibleResults = audibleTask.Result;
                _logger.LogInformation("Collected {AmazonCount} Amazon raw results and {AudibleCount} Audible raw results", amazonResults.Count, audibleResults.Count);

                // Step 2: Build a unified ASIN candidate set (Amazon priority, then Audible)
                // Also create a lookup map for fallback titles and full search result objects
                var asinCandidates = new List<string>();
                var asinToRawResult = new Dictionary<string, (string? Title, string? Author, string? ImageUrl)>(StringComparer.OrdinalIgnoreCase);
                var asinToAudibleResult = new Dictionary<string, AudibleSearchResult>(StringComparer.OrdinalIgnoreCase);
                
                foreach (var a in amazonResults.Where(a => !string.IsNullOrEmpty(a.Asin) && IsValidAsin(a.Asin!)))
                {
                    asinCandidates.Add(a.Asin!);
                    asinToRawResult[a.Asin!] = (a.Title, a.Author, a.ImageUrl);
                }
                
                foreach (var a in audibleResults.Where(a => !string.IsNullOrEmpty(a.Asin) && IsValidAsin(a.Asin!)))
                {
                    if (!asinToRawResult.ContainsKey(a.Asin!))
                    {
                        asinCandidates.Add(a.Asin!);
                        asinToRawResult[a.Asin!] = (a.Title, a.Author, a.ImageUrl);
                        asinToAudibleResult[a.Asin!] = a;  // Store full Audible search result
                    }
                }
                
                asinCandidates = asinCandidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToList();
                _logger.LogInformation("Unified ASIN candidate list size: {Count}", asinCandidates.Count);

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

                // Step 3: Enrich each ASIN with detailed metadata concurrently (limit concurrency)
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
                            
                            // Get the original search results
                            asinToRawResult.TryGetValue(asin, out var rawResult);
                            asinToAudibleResult.TryGetValue(asin, out var audibleResult);
                            
                            // Scrape metadata from product page
                            var metadata = await _audibleMetadataService.ScrapeAudibleMetadataAsync(asin);
                            
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
                                enriched.Add(enrichedResult);
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

                // If still no enriched results, try OpenLibrary derived titles to attempt enrichment again
                if (!results.Any())
                {
                    _logger.LogInformation("No Amazon or Audible results found, trying OpenLibrary for title variations");
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
                                        var metadata = await _audibleMetadataService.ScrapeAudibleMetadataAsync(altResult.Asin);
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
                target.ImageUrl = scraped.ImageUrl;
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

        private async Task<SearchResult> ConvertMetadataToSearchResultAsync(AudibleBookMetadata metadata, string asin, string? fallbackTitle = null, string? fallbackAuthor = null, string? fallbackImageUrl = null)
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
            
            // Download and cache the image to temp storage
            if (!string.IsNullOrEmpty(imageUrl) && !string.IsNullOrEmpty(asin))
            {
                try
                {
                    var cachedPath = await _imageCacheService.DownloadAndCacheImageAsync(imageUrl, asin);
                    if (cachedPath != null)
                    {
                        // Return API endpoint URL instead of file path
                        imageUrl = $"/api/images/{asin}";
                        _logger.LogInformation("Cached image for ASIN {Asin} to temp storage, API URL: {ImageUrl}", asin, imageUrl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to cache image for ASIN {Asin}, will use remote URL", asin);
                    // Keep the original remote URL if caching fails
                }
            }
            
            return new SearchResult
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
                Asin = asin
            };
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
                var apiConfig = await _configurationService.GetApiConfigurationAsync(apiId);
                if (apiConfig == null || !apiConfig.IsEnabled)
                {
                    return new List<SearchResult>();
                }

                // This is a placeholder implementation
                // In a real implementation, you would make HTTP requests to the specific API
                // based on the API configuration and parse the results
                
                _logger.LogInformation($"Searching API {apiConfig.Name} for query: {query}");
                
                // Simulate API call delay
                await Task.Delay(500);
                
                // Return mock results for now
                return GenerateMockResults(query, apiConfig.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error searching API {apiId} for query: {query}");
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
    }
}
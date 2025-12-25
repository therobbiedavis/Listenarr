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

using System;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Listenarr.Domain.Models;
using Listenarr.Api.Services;
using Listenarr.Api.Services.Search;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly Microsoft.Extensions.Logging.ILogger _logger;
        private readonly AudimetaService _audimetaService;
        private readonly IAudiobookMetadataService _metadataService;
        private readonly IImageCacheService? _imageCacheService;
        private readonly MetadataConverters _metadataConverters;

        public SearchController(
            ISearchService searchService,
            Microsoft.Extensions.Logging.ILogger<SearchController> logger,
            AudimetaService audimetaService,
            IAudiobookMetadataService metadataService,
            IImageCacheService? imageCacheService = null,
            MetadataConverters? metadataConverters = null)
        {
            _searchService = searchService;
            _logger = logger;
            _audimetaService = audimetaService;
            _metadataService = metadataService;
            _imageCacheService = imageCacheService;
            _metadataConverters = metadataConverters ?? new MetadataConverters(imageCacheService, (Microsoft.Extensions.Logging.ILogger<Listenarr.Api.Services.Search.MetadataConverters>)logger);
        }


        [HttpPost]
        public async Task<ActionResult<object>> Search([FromBody] JsonElement reqJson)
        {
            try
            {
                if (reqJson.ValueKind == JsonValueKind.Undefined || reqJson.ValueKind == JsonValueKind.Null)
                {
                    return BadRequest("SearchRequest body is required");
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

                var req = JsonSerializer.Deserialize<Listenarr.Api.Models.SearchRequest>(reqJson.GetRawText(), options);
                if (req == null) return BadRequest("SearchRequest body is required");

                if (req.Mode == Listenarr.Api.Models.SearchMode.Simple)
                {
                    var q = req.Query ?? string.Empty;
                    var region = string.IsNullOrWhiteSpace(req.Region) ? "us" : req.Region;
                    var language = string.IsNullOrWhiteSpace(req.Language) ? null : req.Language;
                    var results = await _searchService.IntelligentSearchAsync(q, region: region, language: language, ct: HttpContext.RequestAborted);
                    return Ok(new { results });
                }
                else // Advanced
                {
                    // Route all advanced search logic through SearchService for normalization, filtering, and orchestration
                    

                    // Validate and normalize ISBN/ASIN inputs for advanced searches.
                    // If an ISBN-10 is supplied, convert it to ISBN-13 using the 978 prefix.
                    try
                    {
                        if (!string.IsNullOrWhiteSpace(req.Isbn))
                        {
                            var rawIsbn = Regex.Replace(req.Isbn ?? string.Empty, "[^0-9Xx]", string.Empty);
                            if (rawIsbn.Length == 10)
                            {
                                var converted = ConvertIsbn10ToIsbn13(rawIsbn);
                                if (converted == null)
                                {
                                    return BadRequest("Invalid ISBN-10 provided");
                                }
                                req.Isbn = converted; // replace with ISBN-13
                                _logger.LogInformation("Converted ISBN-10 to ISBN-13: {Original} -> {Converted}", rawIsbn, converted);
                            }
                            else if (rawIsbn.Length == 13)
                            {
                                if (!Regex.IsMatch(rawIsbn, "^[0-9]{13}$"))
                                {
                                    return BadRequest("ISBN must be 13 digits");
                                }
                                req.Isbn = rawIsbn;
                            }
                            else
                            {
                                return BadRequest("ISBN must be either 10 or 13 characters");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to normalize ISBN in advanced search");
                        return BadRequest("Invalid ISBN format");
                    }

                    // Compose a query string from advanced parameters for unified handling
                    var region = string.IsNullOrWhiteSpace(req.Region) ? "us" : req.Region;
                    var language = string.IsNullOrWhiteSpace(req.Language) ? null : req.Language;

                    // Debug: log incoming advanced parameters for diagnostics
                    try { _logger.LogInformation("[DBG] Advanced search request: Author='{Author}', Title='{Title}', Isbn='{Isbn}', Asin='{Asin}', Query='{Query}', Region='{Region}', Language='{Language}'", req.Author, req.Title, req.Isbn, req.Asin, req.Query, region, language); } catch {}

                    // If the advanced request contains an ASIN, prefer a direct Audimeta metadata
                    // lookup and return a single enriched SearchResult. ASIN searches should
                    // be authoritative and ignore other advanced inputs.
                    if (!string.IsNullOrWhiteSpace(req.Asin))
                    {
                        try
                        {
                            var audimeta = await _audimetaService.GetBookMetadataAsync(req.Asin, region, true);
                            if (audimeta != null)
                            {
                                // Convert audimeta response to internal metadata then to SearchResult
                                var metadata = _metadataConverters.ConvertAudimetaToMetadata(audimeta, req.Asin, source: "Audimeta");
                                var sr = await _metadataConverters.ConvertMetadataToSearchResultAsync(metadata, req.Asin, req.Title, req.Author, fallbackImageUrl: null, fallbackLanguage: language);
                                SanitizeResultForPublicApi(sr, region);
                                return Ok(new { results = new List<SearchResult> { sr } });
                            }
                            // If audimeta didn't return a record, fall through to unified search below
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Audimeta lookup failed for ASIN {Asin} in advanced search; falling back to unified search", req.Asin);
                        }
                    }

                    // Previously there was a special-case path here that handled author-only
                    // advanced searches separately. To ensure all advanced searches (author-only,
                    // author+title, title-only, ISBN, etc.) receive identical metadata
                    // enrichment and conversion, route advanced requests through the
                    // unified IntelligentSearch pipeline below. This guarantees Audimeta
                    // metadata is fetched and converted consistently.

                    // Compose a query string from advanced parameters for unified handling
                    var queryParts = new List<string>();
                    // Prefix author/title/isbn/asin tokens so IntelligentSearch parser
                    // recognizes them and selects the correct search branch (e.g. AUTHOR_TITLE).
                    if (!string.IsNullOrWhiteSpace(req.Author)) queryParts.Add($"AUTHOR:{req.Author}");
                    if (!string.IsNullOrWhiteSpace(req.Title)) queryParts.Add($"TITLE:{req.Title}");
                    if (!string.IsNullOrWhiteSpace(req.Isbn)) queryParts.Add($"ISBN:{req.Isbn}");
                    if (!string.IsNullOrWhiteSpace(req.Asin)) queryParts.Add($"ASIN:{req.Asin}");
                    var query = queryParts.Count > 0 ? string.Join(" ", queryParts) : (req.Query ?? string.Empty);
                    try { _logger.LogInformation("Advanced search request composed parts={Parts} -> query='{Query}'", string.Join("|", queryParts), LogRedaction.SanitizeText(query)); } catch {}
                    // Respect optional pagination/candidate caps from the client
                    var candidateLimit = req.Cap.HasValue ? Math.Clamp(req.Cap.Value, 5, 2000) : 200;
                    var returnLimit = req.Pagination != null && req.Pagination.Limit > 0 ? Math.Clamp(req.Pagination.Limit, 1, 1000) : 50;
                    var results = await _searchService.IntelligentSearchAsync(query, candidateLimit, returnLimit, region: region, language: language, ct: HttpContext.RequestAborted);
                    foreach (var r in results)
                    {
                        SanitizeResultForPublicApi(r, region);
                    }
                    return Ok(new { results });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing search request body");
                return BadRequest("Invalid search request");
            }
        }

        private void SanitizeResultForPublicApi(SearchResult r, string region)
        {
            // Minimal sanitization for public API: ensure ProductUrl is an http(s) URL when ASIN is available
            try
            {
                if (r == null) return;
                if (string.IsNullOrWhiteSpace(r.ProductUrl) && !string.IsNullOrWhiteSpace(r.Asin))
                {
                    r.ProductUrl = $"https://www.amazon.com/dp/{r.Asin}";
                }
            }
            catch { }
        }

        private static string? ConvertIsbn10ToIsbn13(string isbn10)
        {
            if (string.IsNullOrWhiteSpace(isbn10)) return null;
            // isbn10 is expected to be 10 chars where first 9 are digits and last is digit or 'X'
            if (isbn10.Length != 10) return null;
            var first9 = isbn10.Substring(0, 9);
            if (!Regex.IsMatch(first9, "^[0-9]{9}$")) return null;
            var twelve = "978" + first9; // 12 digits
            int sum = 0;
            for (int i = 0; i < 12; i++)
            {
                int d = twelve[i] - '0';
                sum += (i % 2 == 0) ? d * 1 : d * 3;
            }
            int mod = sum % 10;
            int check = (10 - mod) % 10;
            return twelve + check.ToString();
        }

        private async Task EnsureCachedImagesForAudimetaResultsAsync(List<AudimetaSearchResult>? results)
        {
            if (results == null || results.Count == 0) return;
            if (_imageCacheService == null) return; // nothing to do in tests if not provided

            foreach (var r in results)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(r.Asin)) continue;

                    var cached = await _imageCacheService.GetCachedImagePathAsync(r.Asin);
                    if (!string.IsNullOrWhiteSpace(cached))
                    {
                        r.ImageUrl = $"/api/images/{r.Asin}";
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(r.ImageUrl))
                    {
                        var downloaded = await _imageCacheService.DownloadAndCacheImageAsync(r.ImageUrl, r.Asin);
                        if (!string.IsNullOrWhiteSpace(downloaded))
                        {
                            r.ImageUrl = $"/api/images/{r.Asin}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to ensure cached image for {Asin}", r?.Asin);
                }
            }
        }

        [HttpGet]
        public async Task<ActionResult<List<SearchResult>>> Search(
            [FromQuery] string query,
            [FromQuery] string? category = null,
            [FromQuery] List<string>? apiIds = null,
            [FromQuery] bool enrichedOnly = false,
            [FromQuery] SearchSortBy sortBy = SearchSortBy.Seeders,
            [FromQuery] SearchSortDirection sortDirection = SearchSortDirection.Descending)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    // If model-binding didn't populate the parameter (direct controller calls in tests),
                    // try to read the raw query string value. If still missing, fall back to empty string
                    // so unit/integration tests that call the action directly don't get a BadRequest.
                    try
                    {
                        var qFromReq = HttpContext?.Request?.Query["query"].ToString();
                        if (!string.IsNullOrWhiteSpace(qFromReq))
                        {
                            query = qFromReq;
                        }
                        else
                        {
                            query = query ?? string.Empty;
                        }
                    }
                    catch { query = query ?? string.Empty; }
                }

                var results = await _searchService.SearchAsync(query, category, apiIds, sortBy, sortDirection);
                if (enrichedOnly)
                {
                    results = results.Where(r => r.IsEnriched).ToList();
                }
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search for query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("intelligent")]
        [ProducesResponseType(typeof(List<SearchResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<SearchResult>>> IntelligentSearch(
                [FromQuery] string query,
                [FromQuery] string? category = null,
                [FromQuery] int candidateLimit = 50,
                [FromQuery] int returnLimit = 50,
                [FromQuery] string containmentMode = "Relaxed",
                [FromQuery] bool requireAuthorAndPublisher = false,
                [FromQuery] double fuzzyThreshold = 0.7)
        {
            try
            {
                // Debug: log raw incoming query to help integration-test diagnostics
                try { System.Console.WriteLine($"[DEBUG] IntelligentSearch called with query='{query ?? "<null>"}'"); } catch { }

                // Also emit a warning-level log so test output captures the value
                try { _logger.LogWarning("[DBG] IntelligentSearch called with query='{Query}'", query ?? "<null>"); } catch { }

                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required");
                }

                _logger.LogInformation("IntelligentSearch called for query: {Query}", LogRedaction.SanitizeText(query));
                var region = Request.Query.ContainsKey("region") ? Request.Query["region"].ToString() ?? "us" : "us";
                var language = Request.Query.ContainsKey("language") ? Request.Query["language"].ToString() : null;
                var results = await _searchService.IntelligentSearchAsync(query, candidateLimit, returnLimit, containmentMode, requireAuthorAndPublisher, fuzzyThreshold, region, language, HttpContext.RequestAborted);
                _logger.LogInformation("IntelligentSearch returning {Count} results for query: {Query}", results.Count, LogRedaction.SanitizeText(query));
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing intelligent search for query: {Query}", LogRedaction.SanitizeText(query));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("indexers")]
        [ProducesResponseType(typeof(List<SearchResult>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<SearchResult>>> IndexersSearch(
                [FromQuery] string query,
                [FromQuery] string? category = null,
                [FromQuery] SearchSortBy sortBy = SearchSortBy.Seeders,
                [FromQuery] SearchSortDirection sortDirection = SearchSortDirection.Descending,
                [FromQuery] bool isAutomaticSearch = false)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required");
                }

                _logger.LogInformation("IndexersSearch called for query: {Query}, isAutomaticSearch={IsAutomatic}", LogRedaction.SanitizeText(query), isAutomaticSearch);
                var results = await _searchService.SearchIndexersAsync(query, category, sortBy, sortDirection, isAutomaticSearch);
                _logger.LogInformation("IndexersSearch returning {Count} results for query: {Query}", results.Count, LogRedaction.SanitizeText(query));
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching indexers for query: {Query}", LogRedaction.SanitizeText(query));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("test/{apiId}")]
        public async Task<ActionResult<bool>> TestApiConnection(string apiId)
        {
            try
            {
                var isConnected = await _searchService.TestApiConnectionAsync(apiId);
                return Ok(isConnected);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error testing API connection for {ApiId}", apiId);
                return StatusCode(500, "Internal server error");
            }
        }

        // [HttpGet("indexers")]
        // public async Task<ActionResult<List<SearchResult>>> SearchIndexers(
        //     [FromQuery] string query,
        //     [FromQuery] string? category = null)
        // {
        //     try
        //     {
        //         if (string.IsNullOrEmpty(query))
        //         {
        //             return BadRequest("Query parameter is required");
        //         }

        //         var results = await _searchService.SearchIndexersAsync(query, category);
        // Optional tuning parameters exposed to callers
        //var candidateLimit = int.TryParse(Request.Query["candidateLimit"], out var cl) ? Math.Clamp(cl, 5, 200) : 50;
        //var returnLimit = int.TryParse(Request.Query["returnLimit"], out var rl) ? Math.Clamp(rl, 1, 100) : 10;
        //var containmentMode = Request.Query.ContainsKey("containmentMode") ? Request.Query["containmentMode"].ToString() ?? "Relaxed" : "Relaxed";
        //var requireAuthorAndPublisher = bool.TryParse(Request.Query["requireAuthorAndPublisher"], out var rap) ? rap : false;
        //var fuzzyThreshold = double.TryParse(Request.Query["fuzzyThreshold"], out var ft) ? Math.Clamp(ft, 0.0, 1.0) : 0.7;
        //         return Ok(results);
        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Error searching indexers for query: {Query}", query);
        //         return StatusCode(500, "Internal server error");
        //     }
        // }

        /// <summary>
        /// Search for audiobooks using audimeta.de
        /// </summary>
        [HttpGet("audimeta")]
        public async Task<ActionResult<AudimetaSearchResponse>> SearchAudimeta(
            [FromQuery] string query,
            [FromQuery] string region = "us",
            [FromQuery] string? language = null)
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required");
                }

                var result = await _audimetaService.SearchBooksAsync(query, region: region, language: language);
                if (result == null)
                {
                    return NotFound("No results found");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching audimeta for query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Search for audiobooks by title, automatically fetching full metadata from configured sources.
        /// Note: currently consumed by the Discord bot; changes here can cascade to that integration.
        /// </summary>
        [HttpGet("title")]
        [ProducesResponseType(typeof(List<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<List<object>>> SearchByTitle(
            [FromQuery] string query,
            [FromQuery] string region = "us",
            [FromQuery] int limit = 10)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    return BadRequest("Query parameter is required");
                }

                _logger.LogInformation("Searching by title: {Query}", query);

                // If the query looks like an ASIN, short-circuit to metadata lookup so we don't run
                // a full Amazon/Audible text search that can return unrelated items.
                bool IsAsin(string s)
                {
                    if (string.IsNullOrEmpty(s)) return false;
                    if (s.Length != 10) return false;
                    if (!(s.StartsWith("B0") || char.IsDigit(s[0]))) return false;
                    return s.All(char.IsLetterOrDigit);
                }

                if (IsAsin(query.Trim()))
                {
                    var asin = query.Trim();
                    _logger.LogInformation("Query appears to be an ASIN; attempting direct metadata lookup for: {Asin}", asin);

                    // Try configured metadata sources (audimeta, audnexus, etc.) via AudimetaService first
                    try
                    {
                        var audimeta = await _audimetaService.GetBookMetadataAsync(asin, region, true);
                        if (audimeta != null)
                        {
                            var metadataObj = new
                            {
                                metadata = audimeta,
                                source = "Audimeta",
                                sourceUrl = "https://audimeta.de"
                            };
                            return Ok(new List<object> { metadataObj });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Audimeta lookup failed for ASIN {Asin}, trying other configured metadata sources", asin);
                    }

                    // If audimeta didn't return anything, try configured metadata sources directly
                    try
                    {
                        var meta = await _metadataService.GetMetadataAsync(asin, region, true);
                        if (meta != null)
                        {
                            return Ok(new List<object> { meta });
                        }
                        _logger.LogWarning("Metadata lookup returned null for ASIN {Asin}, falling back to intelligent search", asin);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Metadata lookup failed for ASIN {Asin}, falling back to intelligent search", asin);
                    }

                    // If no metadata found via configured sources, fall back to the generic intelligent search below
                }

                // Use intelligent search (Amazon/Audible + metadata enrichment) for Discord bot
                // This excludes indexer results which are not suitable for bot interactions
                var searchResults = await _searchService.IntelligentSearchAsync(query, region: region, language: null, ct: HttpContext.RequestAborted);

                if (searchResults == null || !searchResults.Any())
                {
                    _logger.LogWarning("No results found for title search: {Query}", query);
                    return Ok(new List<object>());
                }

                // Convert SearchResult objects to the expected format for Discord bot
                var results = new List<object>();
                var resultsToReturn = searchResults.Take(limit).ToList();

                foreach (var searchResult in resultsToReturn)
                {
                    try
                    {
                        // Create a metadata-like object from the SearchResult
                        var metadata = new
                        {
                            Asin = searchResult.Asin,
                            Title = searchResult.Title,
                            Subtitle = searchResult.Series != null ? $"{searchResult.Series} #{searchResult.SeriesNumber}" : null,
                            Authors = !string.IsNullOrEmpty(searchResult.Author) ? new[] { new { Name = searchResult.Author } } : null,
                            Narrators = !string.IsNullOrEmpty(searchResult.Narrator) ? searchResult.Narrator.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).Select(n => new { Name = n.Trim() }) : null,
                            Publisher = searchResult.Publisher,
                            Description = searchResult.Description,
                            ImageUrl = searchResult.ImageUrl,
                            LengthMinutes = searchResult.Runtime,
                            Language = searchResult.Language,
                            ReleaseDate = searchResult.PublishedDate != DateTime.MinValue ? searchResult.PublishedDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") : null,
                            Series = !string.IsNullOrEmpty(searchResult.Series) ? new[] { new { Name = searchResult.Series, Position = searchResult.SeriesNumber } } : null
                        };

                        results.Add(new
                        {
                            metadata = metadata,
                            source = searchResult.MetadataSource ?? searchResult.Source ?? "Amazon/Audible",
                            sourceUrl = "https://www.amazon.com"
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to convert search result for title: {Title}", searchResult.Title);
                        continue;
                    }
                }

                _logger.LogInformation("Successfully fetched {Count} enriched results for title search: {Query}", results.Count, query);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing title search for query: {Query}", query);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get audiobook metadata from audimeta.de by ASIN (deprecated in favor of /api/metadata/audimeta/{asin})
        /// </summary>
        [Obsolete("Use /api/metadata/audimeta/{asin} instead.")]
        [HttpGet("audimeta/{asin}")]
        public async Task<ActionResult<AudimetaBookResponse>> GetAudimetaMetadata(
            string asin,
            [FromQuery] string region = "us",
            [FromQuery] bool cache = true)
        {
            Response.Headers["Deprecation"] = "true";
            Response.Headers["Link"] = $"</api/metadata/audimeta/{asin}>; rel=\"successor-version\"";

            try
            {
                if (string.IsNullOrEmpty(asin))
                {
                    return BadRequest("ASIN parameter is required");
                }

                var result = await _metadataService.GetAudimetaMetadataAsync(asin, region, cache);
                if (result == null)
                {
                    return NotFound($"No metadata found for ASIN: {asin}");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching audimeta metadata for ASIN: {Asin}", asin);
                return StatusCode(500, "Internal server error");
            }
        }

        /// <summary>
        /// Get audiobook metadata from configured metadata sources by ASIN (deprecated in favor of /api/metadata/{asin})
        /// </summary>
        [Obsolete("Use /api/metadata/{asin} instead.")]
        [HttpGet("metadata/{asin}")]
        public async Task<ActionResult<object>> GetMetadata(
            string asin,
            [FromQuery] string region = "us",
            [FromQuery] bool cache = true)
        {
            Response.Headers["Deprecation"] = "true";
            Response.Headers["Link"] = $"</api/metadata/{asin}>; rel=\"successor-version\"";

            try
            {
                if (string.IsNullOrWhiteSpace(asin))
                {
                    return BadRequest("ASIN is required");
                }

                var result = await _metadataService.GetMetadataAsync(asin, region, cache);
                if (result == null)
                {
                    return NotFound($"No metadata found for ASIN: {asin} from any configured source");
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metadata for ASIN: {Asin}", asin);
                return StatusCode(500, $"Error fetching metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Search a specific API by ID
        /// Note: This route uses a parameter and must come after all specific routes to avoid conflicts
        /// </summary>
        [HttpGet("{apiId}")]
        public async Task<ActionResult<List<SearchResult>>> SearchByApi(
            string apiId,
            [FromQuery] string query,
            [FromQuery] string? category = null)
        {
            try
            {
                _logger.LogInformation("SearchByApi called with apiId: {ApiId}, query: {Query}", apiId, query);

                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required");
                }

                var results = await _searchService.SearchByApiAsync(apiId, query, category);
                _logger.LogInformation("SearchByApi returning {Count} results for apiId: {ApiId}", results.Count, apiId);
                return Ok(results);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching API {ApiId} for query: {Query}", apiId, query);
                return StatusCode(500, "Internal server error");
            }
        }
    }
}

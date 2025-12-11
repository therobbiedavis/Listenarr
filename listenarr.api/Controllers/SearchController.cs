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
using System.Collections.Generic;
using System.Linq;
using Listenarr.Domain.Models;
using Listenarr.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly ISearchService _searchService;
        private readonly ILogger<SearchController> _logger;
        private readonly AudimetaService _audimetaService;
        private readonly IAudiobookMetadataService _metadataService;

        public SearchController(
            ISearchService searchService,
            ILogger<SearchController> logger,
            AudimetaService audimetaService,
            IAudiobookMetadataService metadataService)
        {
            _searchService = searchService;
            _logger = logger;
            _audimetaService = audimetaService;
            _metadataService = metadataService;
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
                    return BadRequest("Query parameter is required");
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
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required");
                }

                _logger.LogInformation("IntelligentSearch called for query: {Query}", LogRedaction.SanitizeText(query));
                var results = await _searchService.IntelligentSearchAsync(query, candidateLimit, returnLimit, containmentMode, requireAuthorAndPublisher, fuzzyThreshold);
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
            [FromQuery] string region = "us")
        {
            try
            {
                if (string.IsNullOrEmpty(query))
                {
                    return BadRequest("Query parameter is required");
                }

                var result = await _audimetaService.SearchBooksAsync(query, region);
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
                var searchResults = await _searchService.IntelligentSearchAsync(query);

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
                            Authors = !string.IsNullOrEmpty(searchResult.Artist) ? new[] { new { Name = searchResult.Artist } } : null,
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

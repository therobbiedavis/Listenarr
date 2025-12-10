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

using Microsoft.AspNetCore.Mvc;
using Listenarr.Api.Services;
using Listenarr.Domain.Models;
using System.Threading.Tasks;
using System.Linq;
using System.Text.RegularExpressions;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/audible")]
    public class AudibleController : ControllerBase
    {
        private readonly IAudibleMetadataService _audibleMetadataService;
        private readonly IAudibleSearchService _audibleSearchService;
        private readonly ILogger<AudibleController> _logger;

        public AudibleController(
            IAudibleMetadataService audibleMetadataService,
            IAudibleSearchService audibleSearchService,
            ILogger<AudibleController> logger)
        {
            _audibleMetadataService = audibleMetadataService;
            _audibleSearchService = audibleSearchService;
            _logger = logger;
        }

        // GET: api/audible/metadata/{asin}
        [HttpGet("metadata/{asin}")]
        public async Task<IActionResult> GetAudibleMetadata(string asin)
        {
            if (string.IsNullOrWhiteSpace(asin))
                return BadRequest("ASIN is required.");

            try
            {
                _logger.LogInformation("Received request for ASIN: {Asin}", asin);

                // First, scrape the product page metadata
                var metadata = await _audibleMetadataService.ScrapeAudibleMetadataAsync(asin);

                // Then try to enrich with search result data (runtime, series, etc.)
                try
                {
                    // Search by ASIN to get the search result with metadata
                    var searchResults = await _audibleSearchService.SearchAudiobooksAsync(asin);
                    var matchingResult = searchResults.FirstOrDefault(r =>
                        r.Asin?.Equals(asin, System.StringComparison.OrdinalIgnoreCase) == true);

                    if (matchingResult != null)
                    {
                        _logger.LogInformation("Found search result for ASIN {Asin}, enriching with search metadata", asin);

                        // Enrich with search result data
                        if (!string.IsNullOrEmpty(matchingResult.Duration))
                        {
                            var match = Regex.Match(matchingResult.Duration, @"(\d+)\s*hrs?\s+(?:and\s+)?(\d+)\s*mins?", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                int hours = int.Parse(match.Groups[1].Value);
                                int minutes = int.Parse(match.Groups[2].Value);
                                metadata.Runtime = hours * 60 + minutes;
                                _logger.LogInformation("Enriched runtime: {Runtime} minutes", metadata.Runtime);
                            }
                        }

                        if (!string.IsNullOrEmpty(matchingResult.Series))
                        {
                            metadata.Series = matchingResult.Series;
                            metadata.SeriesNumber = matchingResult.SeriesNumber;
                            _logger.LogInformation("Enriched series: {Series} #{SeriesNumber}", metadata.Series, metadata.SeriesNumber);
                        }

                        if (!string.IsNullOrEmpty(matchingResult.ReleaseDate))
                        {
                            var yearMatch = Regex.Match(matchingResult.ReleaseDate, @"(\d{2})-(\d{2})-(\d{2})");
                            if (yearMatch.Success)
                            {
                                var year = int.Parse(yearMatch.Groups[3].Value);
                                metadata.PublishYear = (2000 + year).ToString();
                                _logger.LogInformation("Enriched publish year: {Year}", metadata.PublishYear);
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("No matching search result found for ASIN {Asin}", asin);
                    }
                }
                catch (System.Exception searchEx)
                {
                    _logger.LogWarning(searchEx, "Failed to enrich metadata with search results for ASIN {Asin}, continuing with product page data only", asin);
                }

                _logger.LogInformation("Successfully returned metadata for ASIN: {Asin}", asin);
                return Ok(metadata);
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Error scraping Audible metadata for ASIN: {Asin}", asin);
                return StatusCode(500, $"Error scraping Audible metadata: {ex.Message}");
            }
        }

        // POST: api/audible/prefetch
        [HttpPost("prefetch")]
        public async Task<IActionResult> PrefetchMetadata([FromBody] List<string> asins)
        {
            if (asins == null || asins.Count == 0)
                return BadRequest("ASIN list required.");
            var results = await _audibleMetadataService.PrefetchAsync(asins);
            return Ok(results);
        }

        // OPTIONS: api/audible/prefetch (CORS preflight)
        [HttpOptions("prefetch")]
        public IActionResult PrefetchOptions()
        {
            return Ok();
        }
    }
}


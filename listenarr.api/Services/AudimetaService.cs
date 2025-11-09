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
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Listenarr.Api.Services
{
    /// <summary>
    /// Service for fetching audiobook metadata from audimeta.de API
    /// API Documentation: https://audimeta.de/api-docs
    /// </summary>
    public class AudimetaService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AudimetaService> _logger;
        private const string BASE_URL = "https://audimeta.de";

        public AudimetaService(HttpClient httpClient, ILogger<AudimetaService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Set default headers as per API docs
            _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Listenarr/1.0");
        }

        /// <summary>
        /// Fetches audiobook metadata from audimeta.de by ASIN
        /// </summary>
        /// <param name="asin">The Amazon/Audible ASIN</param>
        /// <param name="region">The region code (default: us)</param>
        /// <param name="useCache">Whether to use cached data (default: true)</param>
        /// <returns>Audiobook metadata from audimeta.de</returns>
        public async Task<AudimetaBookResponse?> GetBookMetadataAsync(string asin, string region = "us", bool useCache = true)
        {
            try
            {
                var url = $"{BASE_URL}/book/{asin}?cache={useCache.ToString().ToLower()}&region={region}";
                _logger.LogInformation("Fetching audiobook metadata from audimeta.de: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Audimeta API returned status code {StatusCode} for ASIN {Asin}", 
                        response.StatusCode, asin);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<AudimetaBookResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                _logger.LogInformation("Successfully fetched metadata for ASIN {Asin} from audimeta.de", asin);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching metadata from audimeta.de for ASIN {Asin}", asin);
                return null;
            }
        }

        /// <summary>
        /// Search for audiobooks on audimeta.de
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="region">The region code (default: us)</param>
        /// <returns>Search results from audimeta.de</returns>
        public async Task<AudimetaSearchResponse?> SearchBooksAsync(string query, string region = "us")
        {
            try
            {
                var url = $"{BASE_URL}/search?q={Uri.EscapeDataString(query)}&region={region}";
                _logger.LogInformation("Searching audimeta.de: {Url}", url);

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Audimeta search returned status code {StatusCode} for query {Query}", 
                        response.StatusCode, query);
                    return null;
                }

                var json = await response.Content.ReadAsStringAsync();

                // audimeta.de may return either a JSON object with a 'results' array or a raw JSON array.
                // Try to deserialize into the envelope first, otherwise fall back to an array of results.
                try
                {
                    var envelope = JsonSerializer.Deserialize<AudimetaSearchResponse>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (envelope != null && envelope.Results != null)
                    {
                        _logger.LogInformation("Successfully searched audimeta.de for query: {Query} (enveloped)", query);
                        return envelope;
                    }
                }
                catch (JsonException) { /* fall through to try array */ }

                try
                {
                    var list = JsonSerializer.Deserialize<List<AudimetaSearchResult>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (list != null)
                    {
                        _logger.LogInformation("Successfully searched audimeta.de for query: {Query} (array)", query);
                        return new AudimetaSearchResponse
                        {
                            Results = list,
                            TotalResults = list.Count
                        };
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse audimeta search response for query {Query}", query);
                }

                _logger.LogInformation("Successfully searched audimeta.de for query: {Query}", query);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching audimeta.de for query {Query}", query);
                return null;
            }
        }
    }

    /// <summary>
    /// Response from audimeta.de book endpoint
    /// </summary>
    public class AudimetaBookResponse
    {
        public string? Asin { get; set; }
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public List<AudimetaAuthor>? Authors { get; set; }
        public List<AudimetaNarrator>? Narrators { get; set; }
        public string? Publisher { get; set; }
        public string? PublishDate { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int? LengthMinutes { get; set; }
        public string? Language { get; set; }
        public List<AudimetaGenre>? Genres { get; set; }
        public List<AudimetaSeries>? Series { get; set; }
        public bool? Explicit { get; set; }
        public string? ReleaseDate { get; set; }
        public string? Isbn { get; set; }
        public string? Region { get; set; }
        public string? BookFormat { get; set; }
    }

    public class AudimetaAuthor
    {
        public string? Asin { get; set; }
        public string? Name { get; set; }
        public string? Region { get; set; }
    }

    public class AudimetaNarrator
    {
        public string? Name { get; set; }
    }

    public class AudimetaGenre
    {
        public string? Asin { get; set; }
        public string? Name { get; set; }
        public string? Type { get; set; }
    }

    public class AudimetaSeries
    {
        public string? Asin { get; set; }
        public string? Name { get; set; }
        public string? Position { get; set; }
    }

    /// <summary>
    /// Response from audimeta.de search endpoint
    /// </summary>
    public class AudimetaSearchResponse
    {
        public List<AudimetaSearchResult>? Results { get; set; }
        public int? TotalResults { get; set; }
    }

    /// <summary>
    /// Individual search result from audimeta.de
    /// </summary>
    public class AudimetaSearchResult
    {
        public string? Asin { get; set; }
        public string? Title { get; set; }
        // audimeta now returns authors as objects (e.g. { name: "Author Name", asin: "..." })
        // keep a typed representation so deserialization succeeds
        public List<AudimetaAuthor>? Authors { get; set; }
        public string? ImageUrl { get; set; }
        public int? RuntimeLengthMin { get; set; }
        public string? Language { get; set; }
    }
}

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
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Listenarr.Api.Models;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/indexers")]
    public class IndexersController : ControllerBase
    {
        private readonly ListenArrDbContext _dbContext;
        private readonly ILogger<IndexersController> _logger;
        private readonly HttpClient _httpClient;
        
        public IndexersController(ListenArrDbContext dbContext, ILogger<IndexersController> logger, HttpClient httpClient)
        {
            _dbContext = dbContext;
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// Get all indexers
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var indexers = await _dbContext.Indexers
                .OrderBy(i => i.Priority)
                .ThenBy(i => i.Name)
                .ToListAsync();

            return Ok(indexers);
        }

        /// <summary>
        /// Get indexer by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
        {
            var indexer = await _dbContext.Indexers.FindAsync(id);
            if (indexer == null)
            {
                return NotFound(new { message = "Indexer not found" });
            }

            return Ok(indexer);
        }

        /// <summary>
        /// Create a new indexer
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Indexer indexer)
        {
            indexer.CreatedAt = DateTime.UtcNow;
            indexer.UpdatedAt = DateTime.UtcNow;

            _dbContext.Indexers.Add(indexer);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Created indexer '{Name}' (ID: {Id}, Type: {Type})", 
                indexer.Name, indexer.Id, indexer.Type);

            return CreatedAtAction(nameof(GetById), new { id = indexer.Id }, indexer);
        }

        /// <summary>
        /// Update an existing indexer
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] Indexer indexer)
        {
            var existing = await _dbContext.Indexers.FindAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = "Indexer not found" });
            }

            // Update properties
            existing.Name = indexer.Name;
            existing.Type = indexer.Type;
            existing.Implementation = indexer.Implementation;
            existing.Url = indexer.Url;
            existing.ApiKey = indexer.ApiKey;
            existing.Categories = indexer.Categories;
            existing.AnimeCategories = indexer.AnimeCategories;
            existing.Tags = indexer.Tags;
            existing.EnableRss = indexer.EnableRss;
            existing.EnableAutomaticSearch = indexer.EnableAutomaticSearch;
            existing.EnableInteractiveSearch = indexer.EnableInteractiveSearch;
            existing.EnableAnimeStandardSearch = indexer.EnableAnimeStandardSearch;
            existing.IsEnabled = indexer.IsEnabled;
            existing.Priority = indexer.Priority;
            existing.MinimumAge = indexer.MinimumAge;
            existing.Retention = indexer.Retention;
            existing.MaximumSize = indexer.MaximumSize;
            existing.AdditionalSettings = indexer.AdditionalSettings;
            existing.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Updated indexer '{Name}' (ID: {Id})", existing.Name, existing.Id);

            return Ok(existing);
        }

        /// <summary>
        /// Delete an indexer
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var indexer = await _dbContext.Indexers.FindAsync(id);
            if (indexer == null)
            {
                return NotFound(new { message = "Indexer not found" });
            }

            _dbContext.Indexers.Remove(indexer);
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Deleted indexer '{Name}' (ID: {Id})", indexer.Name, indexer.Id);

            return Ok(new { message = "Indexer deleted successfully", id });
        }

        /// <summary>
        /// Test an indexer connection
        /// </summary>
        [HttpPost("{id}/test")]
        public async Task<IActionResult> Test(int id)
        {
            var indexer = await _dbContext.Indexers.FindAsync(id);
            if (indexer == null)
            {
                return NotFound(new { message = "Indexer not found" });
            }

            try
            {
                _logger.LogInformation("Testing indexer '{Name}' (Type: {Type}, Implementation: {Implementation})", 
                    indexer.Name, indexer.Type, indexer.Implementation);

                // Validate basic fields
                if (string.IsNullOrEmpty(indexer.Url))
                {
                    throw new Exception("URL is required");
                }

                // Handle different implementations
                if (indexer.Implementation.Equals("InternetArchive", StringComparison.OrdinalIgnoreCase))
                {
                    return await TestInternetArchive(indexer);
                }
                else if (indexer.Implementation.Equals("MyAnonamouse", StringComparison.OrdinalIgnoreCase))
                {
                    return await TestMyAnonamouse(indexer);
                }

                // Normalize and persist the indexer URL to avoid common misconfigurations
                var normalized = NormalizeIndexerUrl(indexer.Url);
                if (!string.Equals(normalized, indexer.Url, StringComparison.Ordinal))
                {
                    _logger.LogInformation("Normalizing indexer URL from '{Old}' to '{New}'", indexer.Url, normalized);
                    indexer.Url = normalized;
                    indexer.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }

                // Build test URL (caps endpoint for capabilities) - for Torznab/Newznab
                var url = indexer.Url.TrimEnd('/');
                var apiPath = indexer.Implementation.ToLower() switch
                {
                    "torznab" => "/api",
                    "newznab" => "/api",
                    _ => "/api"
                };

                var queryParams = new List<string> { "t=caps" };
                
                if (!string.IsNullOrEmpty(indexer.ApiKey))
                {
                    queryParams.Add($"apikey={Uri.EscapeDataString(indexer.ApiKey)}");
                }

                var testUrl = $"{url}{apiPath}?{string.Join("&", queryParams)}";
                _logger.LogDebug("Testing indexer URL: {Url}", testUrl);

                // Make test request
                var response = await _httpClient.GetAsync(testUrl);
                var content = await response.Content.ReadAsStringAsync();

                // Defensive checks: redirects or HTML login pages are common misconfigurations
                if ((int)response.StatusCode >= 300 && (int)response.StatusCode < 400)
                {
                    var location = response.Headers.Location?.ToString() ?? "(none)";
                    throw new Exception($"Unexpected redirect (HTTP {(int)response.StatusCode}) to '{location}'. The indexer URL may point to a UI/login page instead of the API endpoint.");
                }

                // If the server returned text/html, it's likely an HTML login or error page rather than Torznab XML
                if (response.Content.Headers.ContentType != null && response.Content.Headers.ContentType.MediaType != null
                    && response.Content.Headers.ContentType.MediaType.Contains("html", StringComparison.OrdinalIgnoreCase))
                {
                    // Truncate content for the error message
                    var snippet = content?.Length > 512 ? content.Substring(0, 512) + "..." : content;
                    throw new Exception($"Indexer returned HTML (Content-Type: {response.Content.Headers.ContentType}). Likely a login page or UI. Sample: {snippet}");
                }

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"HTTP {response.StatusCode}: {content}");
                }

                // Try to parse as XML to verify it's a valid Torznab/Newznab response
                System.Xml.Linq.XDocument doc;
                try
                {
                    // Parse XML with more lenient settings
                    var settings = new System.Xml.XmlReaderSettings
                    {
                        DtdProcessing = System.Xml.DtdProcessing.Ignore,
                        XmlResolver = null,
                        IgnoreWhitespace = true,
                        IgnoreComments = true
                    };

                    using (var reader = System.Xml.XmlReader.Create(new System.IO.StringReader(content), settings))
                    {
                        doc = System.Xml.Linq.XDocument.Load(reader);
                    }
                }
                catch (System.Xml.XmlException xmlEx)
                {
                    _logger.LogError(xmlEx, "XML parsing error at Line {Line}, Position {Position}", 
                        xmlEx.LineNumber, xmlEx.LinePosition);
                    
                    // Log context around the error
                    var lines = content.Split('\n');
                    if (xmlEx.LineNumber > 0 && xmlEx.LineNumber <= lines.Length)
                    {
                        var startLine = Math.Max(0, xmlEx.LineNumber - 3);
                        var endLine = Math.Min(lines.Length - 1, xmlEx.LineNumber + 2);
                        var context = string.Join("\n", lines[startLine..(endLine + 1)]);
                        _logger.LogError("XML context:\n{Context}", context);
                    }
                    
                    throw new Exception($"Invalid XML response: {xmlEx.Message}");
                }
                
                // The root element should be 'caps' for Torznab/Newznab
                var capsElement = doc.Root;
                
                if (capsElement == null || capsElement.Name.LocalName != "caps")
                {
                    _logger.LogWarning("Unexpected root element: {RootElement}", capsElement?.Name.LocalName ?? "null");
                    throw new Exception($"Invalid response: expected 'caps' root element, got '{capsElement?.Name.LocalName ?? "null"}'");
                }

                // Extract capabilities info
                var categories = capsElement.Element("categories")?.Elements("category").Count() ?? 0;
                var searchModes = capsElement.Element("searching")?.Elements().Count() ?? 0;

                // Update test result
                indexer.LastTestedAt = DateTime.UtcNow;
                indexer.LastTestSuccessful = true;
                indexer.LastTestError = null;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Indexer '{Name}' test succeeded - {Categories} categories, {SearchModes} search modes", 
                    indexer.Name, categories, searchModes);

                return Ok(new { 
                    success = true, 
                    message = $"Indexer test successful - {categories} categories available",
                    indexer,
                    capabilities = new {
                        categories,
                        searchModes
                    }
                });
            }
            catch (Exception ex)
            {
                indexer.LastTestedAt = DateTime.UtcNow;
                indexer.LastTestSuccessful = false;
                indexer.LastTestError = ex.Message;
                await _dbContext.SaveChangesAsync();

                _logger.LogWarning(ex, "Indexer '{Name}' test failed", indexer.Name);

                return BadRequest(new { 
                    success = false, 
                    message = "Indexer test failed", 
                    error = ex.Message,
                    indexer 
                });
            }
        }

        /// <summary>
        /// Test Internet Archive indexer connection
        /// </summary>
        private async Task<IActionResult> TestInternetArchive(Indexer indexer)
        {
            try
            {
                // Parse collection from AdditionalSettings
                string collection = "librivoxaudio"; // Default
                if (!string.IsNullOrEmpty(indexer.AdditionalSettings))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(indexer.AdditionalSettings);
                        if (doc.RootElement.TryGetProperty("collection", out var collectionProperty))
                        {
                            collection = collectionProperty.GetString() ?? "librivoxaudio";
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse AdditionalSettings for Internet Archive indexer");
                    }
                }

                // Build test URL with minimal query
                var testUrl = $"https://archive.org/advancedsearch.php?q=collection:{collection}&rows=1&output=json";

                _logger.LogInformation("Testing Internet Archive indexer '{Name}' with collection '{Collection}'", 
                    indexer.Name, collection);

                // Make HTTP request
                var response = await _httpClient.GetAsync(testUrl);
                response.EnsureSuccessStatusCode();

                // Parse JSON response
                var content = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(content);
                
                // Validate response structure
                if (!jsonDoc.RootElement.TryGetProperty("response", out var responseProperty))
                {
                    throw new Exception("Invalid response format: missing 'response' property");
                }

                if (!responseProperty.TryGetProperty("docs", out var docsProperty))
                {
                    throw new Exception("Invalid response format: missing 'docs' property");
                }

                // Update indexer with success
                indexer.LastTestedAt = DateTime.UtcNow;
                indexer.LastTestSuccessful = true;
                indexer.LastTestError = null;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Internet Archive indexer '{Name}' test succeeded for collection '{Collection}'", 
                    indexer.Name, collection);

                return Ok(new { 
                    success = true, 
                    message = $"Internet Archive connection successful for collection '{collection}'",
                    collection = collection,
                    indexer 
                });
            }
            catch (Exception ex)
            {
                indexer.LastTestedAt = DateTime.UtcNow;
                indexer.LastTestSuccessful = false;
                indexer.LastTestError = ex.Message;
                await _dbContext.SaveChangesAsync();

                _logger.LogWarning(ex, "Internet Archive indexer '{Name}' test failed", indexer.Name);

                return BadRequest(new { 
                    success = false, 
                    message = "Internet Archive test failed", 
                    error = ex.Message,
                    indexer 
                });
            }
        }

        /// <summary>
        /// Test MyAnonamouse indexer connection
        /// </summary>
        private async Task<IActionResult> TestMyAnonamouse(Indexer indexer)
        {
            try
            {
                // Parse credentials from AdditionalSettings
                string username = string.Empty;
                string password = string.Empty;
                
                if (!string.IsNullOrEmpty(indexer.AdditionalSettings))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(indexer.AdditionalSettings);
                        if (doc.RootElement.TryGetProperty("username", out var usernameProperty))
                        {
                            username = usernameProperty.GetString() ?? string.Empty;
                        }
                        if (doc.RootElement.TryGetProperty("password", out var passwordProperty))
                        {
                            password = passwordProperty.GetString() ?? string.Empty;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse AdditionalSettings for MyAnonamouse indexer");
                    }
                }

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    throw new Exception("Username and password are required for MyAnonamouse");
                }

                // Build test URL
                var testUrl = "https://www.myanonamouse.net/tor/js/loadSearchJSONbasic.php";

                _logger.LogInformation("Testing MyAnonamouse indexer '{Name}' for user '{Username}'", 
                    indexer.Name, username);

                // Create request with Basic Auth
                var request = new HttpRequestMessage(HttpMethod.Post, testUrl);
                var authBytes = System.Text.Encoding.UTF8.GetBytes($"{username}:{password}");
                var authHeader = Convert.ToBase64String(authBytes);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authHeader);

                // Make HTTP request
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                // Parse JSON response
                var content = await response.Content.ReadAsStringAsync();
                using var jsonDoc = JsonDocument.Parse(content);
                
                // Validate response (MyAnonamouse returns JSON with data array)
                if (!jsonDoc.RootElement.TryGetProperty("data", out _))
                {
                    throw new Exception("Invalid response format: missing 'data' property");
                }

                // Update indexer with success
                indexer.LastTestedAt = DateTime.UtcNow;
                indexer.LastTestSuccessful = true;
                indexer.LastTestError = null;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("MyAnonamouse indexer '{Name}' test succeeded for user '{Username}'", 
                    indexer.Name, username);

                return Ok(new { 
                    success = true, 
                    message = $"MyAnonamouse authentication successful for user '{username}'",
                    username = username,
                    indexer 
                });
            }
            catch (Exception ex)
            {
                indexer.LastTestedAt = DateTime.UtcNow;
                indexer.LastTestSuccessful = false;
                indexer.LastTestError = ex.Message;
                await _dbContext.SaveChangesAsync();

                _logger.LogWarning(ex, "MyAnonamouse indexer '{Name}' test failed", indexer.Name);

                return BadRequest(new { 
                    success = false, 
                    message = "MyAnonamouse test failed", 
                    error = ex.Message,
                    indexer 
                });
            }
        }

        /// <summary>
        /// Toggle indexer enabled state
        /// </summary>
        [HttpPut("{id}/toggle")]
        public async Task<IActionResult> Toggle(int id)
        {
            var indexer = await _dbContext.Indexers.FindAsync(id);
            if (indexer == null)
            {
                return NotFound(new { message = "Indexer not found" });
            }

            indexer.IsEnabled = !indexer.IsEnabled;
            indexer.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Toggled indexer '{Name}' to {State}", 
                indexer.Name, indexer.IsEnabled ? "enabled" : "disabled");

            return Ok(indexer);
        }

        /// <summary>
        /// Get enabled indexers only
        /// </summary>
        [HttpGet("enabled")]
        public async Task<IActionResult> GetEnabled()
        {
            var indexers = await _dbContext.Indexers
                .Where(i => i.IsEnabled)
                .OrderBy(i => i.Priority)
                .ThenBy(i => i.Name)
                .ToListAsync();

            return Ok(indexers);
        }

        /// <summary>
        /// Normalize indexer URL by removing duplicate or trailing '/api' segments and ensuring a scheme
        /// </summary>
        private string NormalizeIndexerUrl(string? rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return rawUrl ?? string.Empty;

            var url = rawUrl.Trim();

            // Add scheme if missing (assume https)
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            // Remove repeated '/api/api' or trailing '/api'
            // Normalize multiple slashes
            while (url.Contains("/api/api", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Replace("/api/api", "/api", StringComparison.OrdinalIgnoreCase);
            }

            // If the url ends with '/api', remove it so we can append the correct apiPath later
            if (url.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                url = url.Substring(0, url.Length - 4);
            }

            return url.TrimEnd('/');
        }
    }
}

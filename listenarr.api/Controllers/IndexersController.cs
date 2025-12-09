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
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Listenarr.Domain.Models;
using Listenarr.Infrastructure.Models;

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

        private async Task SaveTestResultAsync(Indexer indexer, bool persist, bool success, string? error)
        {
            // Update the passed indexer instance
            indexer.LastTestedAt = DateTime.UtcNow;
            indexer.LastTestSuccessful = success;
            indexer.LastTestError = error;

            if (persist && indexer.Id != 0)
            {
                // Persist test result back to the database for the stored indexer
                var existing = await _dbContext.Indexers.FindAsync(indexer.Id);
                if (existing != null)
                {
                    existing.LastTestedAt = indexer.LastTestedAt;
                    existing.LastTestSuccessful = success;
                    existing.LastTestError = error;
                    existing.UpdatedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
            }
        }

        private async Task<IActionResult> ExecuteIndexerTestAsync(Indexer indexer, bool persist)
        {
            // Normalize URL first
            indexer.Url = NormalizeIndexerUrl(indexer.Url);

            var impl = (indexer.Implementation ?? string.Empty).Trim().ToLowerInvariant();

            try
            {
                _logger.LogInformation("[IndexerTest] Testing indexer {Name} (impl={Impl}, url={Url})", indexer.Name, indexer.Implementation, indexer.Url);
                return impl switch
                {
                    var s when s == "internetarchive" || s == "internet archive" => await TestInternetArchive(indexer, persist),
                    var s when s == "myanonamouse" => await TestMyAnonamouse(indexer, persist),
                    // For Newznab/Torznab/Custom fall back to a generic connectivity check
                    _ => await TestGenericIndexer(indexer, persist)
                };
            }
            catch (Exception ex)
            {
                await SaveTestResultAsync(indexer, persist, false, ex.Message);
                _logger.LogWarning(ex, "Indexer '{Name}' test failed", indexer.Name);
                return BadRequest(new { success = false, message = "Indexer test failed", error = ex.Message, indexer });
            }
        }

        private async Task<IActionResult> TestGenericIndexer(Indexer indexer, bool persist)
        {
            // Minimal connectivity check: attempt to hit base URL or indexer 'api' endpoint
            try
            {
                var target = indexer.Url?.TrimEnd('/') ?? string.Empty;
                // Prefer /api endpoint if present, otherwise base URL
                var testUrl = target.EndsWith("/api", StringComparison.OrdinalIgnoreCase) ? target : target + "/api";

                // If this is a Newznab/Torznab style indexer, append the apikey query parameter and add capabilities query to test auth
                var implName = (indexer.Implementation ?? string.Empty).Trim().ToLowerInvariant();
                var isNewznabStyle = implName == "newznab" || implName == "torznab";
                
                if (isNewznabStyle)
                {
                    // Newznab/Torznab indexers REQUIRE an API key for authentication
                    if (string.IsNullOrWhiteSpace(indexer.ApiKey))
                    {
                        await SaveTestResultAsync(indexer, persist, false, "API key is required for Newznab/Torznab indexers");
                        return BadRequest(new { success = false, message = "API key is required for Newznab/Torznab indexers", indexer });
                    }
                    
                    // Use search endpoint (t=search) instead of capabilities (t=caps) because
                    // many indexers expose t=caps publicly without authentication.
                    // t=search reliably enforces authentication.
                    var separator = testUrl.Contains('?') ? '&' : '?';
                    testUrl = testUrl + separator + "t=search&limit=1&offset=0";
                    testUrl = testUrl + "&apikey=" + System.Net.WebUtility.UrlEncode(indexer.ApiKey);
                }

                var request = new HttpRequestMessage(HttpMethod.Get, testUrl);
                // Ensure User-Agent is present even if the injected HttpClient was created without defaults
                var version = typeof(IndexersController).Assembly.GetName().Version?.ToString() ?? "0.0.0";
                var userAgent = $"Listenarr/{version} (+https://github.com/therobbiedavis/listenarr)";
                request.Headers.UserAgent.ParseAdd(userAgent);
                // For Newznab/Torznab, also add API key as header (some servers support both)
                // For other indexers, add header if API key is provided
                if (!string.IsNullOrEmpty(indexer.ApiKey))
                {
                    request.Headers.Add("X-Api-Key", indexer.ApiKey);
                }

                _logger.LogInformation("[IndexerTest] GET {Url} UA={UserAgent}", testUrl, userAgent);

                var response = await _httpClient.SendAsync(request);

                _logger.LogInformation("[IndexerTest] {Name} responded {StatusCode}", indexer.Name, (int)response.StatusCode);
                
                // Check for HTTP-level authentication failures
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized || 
                    response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    await SaveTestResultAsync(indexer, persist, false, $"Authentication failed: HTTP {(int)response.StatusCode}");
                    return BadRequest(new { success = false, message = "Authentication failed", status = (int)response.StatusCode, indexer });
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    await SaveTestResultAsync(indexer, persist, false, $"HTTP {(int)response.StatusCode}");
                    return BadRequest(new { success = false, message = "Generic indexer test failed", status = (int)response.StatusCode, indexer });
                }

                // For Newznab/Torznab, parse XML response to check for error elements
                if (isNewznabStyle)
                {
                    var xmlContent = await response.Content.ReadAsStringAsync();
                    var errorMessage = ParseNewznabError(xmlContent);
                    
                    if (errorMessage != null)
                    {
                        var isAuthError = errorMessage.Contains("api", StringComparison.OrdinalIgnoreCase) ||
                                         errorMessage.Contains("key", StringComparison.OrdinalIgnoreCase) ||
                                         errorMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                         errorMessage.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                                         errorMessage.Contains("authentication", StringComparison.OrdinalIgnoreCase);
                        
                        var failureMessage = isAuthError ? $"Authentication failed: {errorMessage}" : errorMessage;
                        await SaveTestResultAsync(indexer, persist, false, failureMessage);
                        return BadRequest(new { success = false, message = failureMessage, indexer });
                    }
                }

                await SaveTestResultAsync(indexer, persist, true, null);
                return Ok(new { success = true, message = "Indexer authentication successful", indexer });
            }
            catch (Exception ex)
            {
                await SaveTestResultAsync(indexer, persist, false, ex.Message);
                _logger.LogWarning(ex, "Generic indexer test failed for {Name}", indexer.Name);
                return BadRequest(new { success = false, message = "Indexer test failed", error = ex.Message, indexer });
            }
        }

        private string? ParseNewznabError(string xmlContent)
        {
            try
            {
                // Parse XML response to check for error element
                // Newznab spec: <error code="XXX" description="..." />
                var settings = new System.Xml.XmlReaderSettings
                {
                    DtdProcessing = System.Xml.DtdProcessing.Ignore,
                    XmlResolver = null
                };

                using var reader = System.Xml.XmlReader.Create(new System.IO.StringReader(xmlContent), settings);
                var doc = System.Xml.Linq.XDocument.Load(reader);
                
                // Check for error element (can be at root, under rss, or as a descendant)
                System.Xml.Linq.XElement? errorElement = null;
                
                // Case 1: Root element is <error>
                if (doc.Root?.Name.LocalName.Equals("error", StringComparison.OrdinalIgnoreCase) == true)
                {
                    errorElement = doc.Root;
                }
                // Case 2: Error is a child or descendant
                else
                {
                    errorElement = doc.Root?.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("error", StringComparison.OrdinalIgnoreCase));
                }
                
                if (errorElement != null)
                {
                    var code = errorElement.Attribute("code")?.Value;
                    var description = errorElement.Attribute("description")?.Value ?? errorElement.Value;
                    return string.IsNullOrEmpty(description) ? $"Error code: {code}" : description;
                }
                
                return null;
            }
            catch
            {
                // If we can't parse the XML, assume no error element
                return null;
            }
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

            return await ExecuteIndexerTestAsync(indexer, persist: true);
        }

        [HttpPost("test")]
        public async Task<IActionResult> TestDraft([FromBody] Indexer indexer)
        {
            if (indexer == null)
            {
                return BadRequest(new { message = "Index data is required" });
            }

            return await ExecuteIndexerTestAsync(indexer, persist: false);
        }

        /// <summary>
        /// Test Internet Archive indexer connection
        /// </summary>
        private async Task<IActionResult> TestInternetArchive(Indexer indexer, bool persist)
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
                await SaveTestResultAsync(indexer, persist, true, null);

                _logger.LogInformation("Internet Archive indexer '{Name}' test succeeded for collection '{Collection}'",
                    indexer.Name, collection);

                return Ok(new
                {
                    success = true,
                    message = $"Internet Archive connection successful for collection '{collection}'",
                    collection = collection,
                    indexer
                });
            }
            catch (Exception ex)
            {
                await SaveTestResultAsync(indexer, persist, false, ex.Message);

                _logger.LogWarning(ex, "Internet Archive indexer '{Name}' test failed", indexer.Name);

                return BadRequest(new
                {
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
        private async Task<IActionResult> TestMyAnonamouse(Indexer indexer, bool persist)
        {
            try
            {
                // Parse mam_id from AdditionalSettings
                string mamId = string.Empty;

                if (!string.IsNullOrEmpty(indexer.AdditionalSettings))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(indexer.AdditionalSettings);
                        if (doc.RootElement.TryGetProperty("mam_id", out var mamIdProperty))
                        {
                            mamId = mamIdProperty.GetString() ?? string.Empty;
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse AdditionalSettings for MyAnonamouse indexer");
                    }
                }

                if (string.IsNullOrEmpty(mamId))
                {
                    throw new Exception("MAM ID is required for MyAnonamouse");
                }

                // Build test URL (mam_id is sent as a cookie)
                var testUrl = $"https://www.myanonamouse.net/tor/js/loadSearchJSONbasic.php";

                _logger.LogInformation("Testing MyAnonamouse indexer '{Name}' with MAM ID '{MamId}'",
                    indexer.Name, LogRedaction.RedactText(mamId, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { mamId ?? string.Empty })));

                // Create request with mam_id as cookie
                var request = new HttpRequestMessage(HttpMethod.Post, testUrl);

                // Add browser-like headers to avoid "invalid request" errors
                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                request.Headers.Referrer = new Uri("https://www.myanonamouse.net/");

                // Create form data (without mam_id since it's now in the cookie)
                var formData = new Dictionary<string, string>
                {
                    ["tor[text]"] = "test",
                    ["tor[srchIn][]"] = "title",
                    ["tor[searchType]"] = "all",
                    ["tor[searchIn]"] = "torrents",
                    ["tor[cat][]"] = "0",
                    ["tor[browseFlagsHideVsShow]"] = "0",
                    ["tor[startDate]"] = "",
                    ["tor[endDate]"] = "",
                    ["tor[hash]"] = "",
                    ["tor[sortType]"] = "default",
                    ["tor[startNumber]"] = "0",
                    ["perpage"] = "1",
                    ["thumbnail"] = "false",
                    ["dlLink"] = "",
                    ["description"] = ""
                };

                var formContent = new FormUrlEncodedContent(formData);
                request.Content = formContent;

                // Add mam_id as a cookie for authentication (bind cookie to the indexer's base host)
                var cookieContainer = new System.Net.CookieContainer();
                var baseUrl = indexer.Url.TrimEnd('/');
                var baseUri = new Uri(baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? baseUrl : "https://" + baseUrl);
                cookieContainer.Add(baseUri, new System.Net.Cookie("mam_id", mamId));
                try
                {
                    var host = baseUri.Host;
                    if (!host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        var wwwUri = new Uri($"{baseUri.Scheme}://www.{host}");
                        cookieContainer.Add(wwwUri, new System.Net.Cookie("mam_id", mamId));
                    }
                }
                catch { }

                // Create HttpClientHandler with cookies
                var handler = new HttpClientHandler
                {
                    CookieContainer = cookieContainer,
                    UseCookies = true
                };

                using var cookieClient = new HttpClient(handler);
                cookieClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                cookieClient.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                cookieClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                cookieClient.DefaultRequestHeaders.Referrer = new Uri("https://www.myanonamouse.net/");

                // Make HTTP request
                var response = await cookieClient.SendAsync(request);
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
                await SaveTestResultAsync(indexer, persist, true, null);

                _logger.LogInformation("MyAnonamouse indexer '{Name}' test succeeded with MAM ID '{MamId}'",
                    indexer.Name, LogRedaction.RedactText(mamId, LogRedaction.GetSensitiveValuesFromEnvironment().Concat(new[] { mamId ?? string.Empty })));

                return Ok(new
                {
                    success = true,
                    message = $"MyAnonamouse authentication successful with MAM ID '{mamId}'",
                    mam_id = mamId,
                    indexer
                });
            }
            catch (Exception ex)
            {
                await SaveTestResultAsync(indexer, persist, false, ex.Message);

                _logger.LogWarning(ex, "MyAnonamouse indexer '{Name}' test failed", indexer.Name);

                return BadRequest(new
                {
                    success = false,
                    message = "MyAnonamouse test failed",
                    error = ex.Message,
                    indexer
                });
            }
        }

        /// <summary>
        /// Debug search against a MyAnonamouse indexer: returns raw response plus parsed results
        /// </summary>
        [HttpPost("{id}/debug-search")]
        public async Task<IActionResult> DebugMyAnonamouseSearch(int id, [FromBody] JsonElement body)
        {
            var indexer = await _dbContext.Indexers.FindAsync(id);
            if (indexer == null) return NotFound(new { message = "Indexer not found" });

            try
            {
                string query = "test";
                if (body.ValueKind == JsonValueKind.Object && body.TryGetProperty("query", out var q))
                {
                    query = q.GetString() ?? "test";
                }

                // Parse mam_id from AdditionalSettings
                string mamId = string.Empty;
                if (!string.IsNullOrEmpty(indexer.AdditionalSettings))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(indexer.AdditionalSettings);
                        if (doc.RootElement.TryGetProperty("mam_id", out var mamIdProperty))
                            mamId = mamIdProperty.GetString() ?? string.Empty;
                    }
                    catch { }
                }

                if (string.IsNullOrEmpty(mamId))
                    return BadRequest(new { success = false, message = "MAM ID missing in indexer settings" });

                var testUrl = $"{indexer.Url.TrimEnd('/')}/tor/js/loadSearchJSONbasic.php";

                var formData = new Dictionary<string, string>
                {
                    ["tor[text]"] = query,
                    ["tor[srchIn][]"] = "title",
                    ["tor[searchType]"] = "all",
                    ["tor[searchIn]"] = "torrents",
                    ["tor[cat][]"] = "0",
                    ["tor[browseFlagsHideVsShow]"] = "0",
                    ["tor[startDate]"] = "",
                    ["tor[endDate]"] = "",
                    ["tor[hash]"] = "",
                    ["tor[sortType]"] = "default",
                    ["tor[startNumber]"] = "0",
                    ["perpage"] = "100",
                    ["thumbnail"] = "false",
                    ["dlLink"] = "",
                    ["description"] = ""
                };

                var request = new HttpRequestMessage(HttpMethod.Post, testUrl)
                {
                    Content = new FormUrlEncodedContent(formData)
                };

                request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                request.Headers.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                request.Headers.Referrer = new Uri("https://www.myanonamouse.net/");

                var cookieContainer = new System.Net.CookieContainer();
                var baseUrl = indexer.Url.TrimEnd('/');
                var baseUri = new Uri(baseUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? baseUrl : "https://" + baseUrl);
                cookieContainer.Add(baseUri, new System.Net.Cookie("mam_id", mamId));
                try
                {
                    var host = baseUri.Host;
                    if (!host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    {
                        var wwwUri = new Uri($"{baseUri.Scheme}://www.{host}");
                        cookieContainer.Add(wwwUri, new System.Net.Cookie("mam_id", mamId));
                    }
                }
                catch { }

                var handler = new HttpClientHandler { CookieContainer = cookieContainer, UseCookies = true };
                using var client = new HttpClient(handler);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/javascript, */*; q=0.01");
                client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
                client.DefaultRequestHeaders.Referrer = new Uri("https://www.myanonamouse.net/");

                var response = await client.SendAsync(request);
                var raw = await response.Content.ReadAsStringAsync();

                // Get parsed results via the Search API on this host
                var parsed = new List<SearchResult>();
                try
                {
                    var scheme = Request.Scheme;
                    var hostVal = Request.Host.Value;
                    var localSearchUrl = $"{scheme}://{hostVal}/api/search/{id}?query={Uri.EscapeDataString(query)}";
                    var localResp = await _httpClient.GetAsync(localSearchUrl);
                    if (localResp.IsSuccessStatusCode)
                    {
                        var json = await localResp.Content.ReadAsStringAsync();
                        var options = new System.Text.Json.JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        parsed = System.Text.Json.JsonSerializer.Deserialize<List<SearchResult>>(json, options) ?? new List<SearchResult>();
                    }
                }
                catch { }

                return Ok(new { success = true, status = (int)response.StatusCode, raw, parsedCount = parsed.Count, parsed });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MyAnonamouse debug search failed for indexer {Id}", id);
                return BadRequest(new { success = false, error = ex.Message });
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


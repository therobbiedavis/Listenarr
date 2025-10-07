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

                // Build test URL (caps endpoint for capabilities)
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
    }
}

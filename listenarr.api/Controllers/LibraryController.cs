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
using System.Threading.Tasks;
using Listenarr.Api.Models;
using Listenarr.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Text.Json;
using System.Reflection;

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/library")]
    public class LibraryController : ControllerBase
    {
        private readonly IAudiobookRepository _repo;
        private readonly IImageCacheService _imageCacheService;
        private readonly ILogger<LibraryController> _logger;
        private readonly ListenArrDbContext _dbContext;
        private readonly IServiceProvider _serviceProvider;
        
        public LibraryController(
            IAudiobookRepository repo, 
            IImageCacheService imageCacheService, 
            ILogger<LibraryController> logger,
            ListenArrDbContext dbContext,
            IServiceProvider serviceProvider)
        {
            _repo = repo;
            _imageCacheService = imageCacheService;
            _logger = logger;
            _dbContext = dbContext;
            _serviceProvider = serviceProvider;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToLibrary([FromBody] AddToLibraryRequest request)
        {
            var metadata = request.Metadata;
            
            // Check if audiobook already exists in library
            if (!string.IsNullOrEmpty(metadata.Asin))
            {
                var existingByAsin = await _repo.GetByAsinAsync(metadata.Asin);
                if (existingByAsin != null)
                {
                    return Conflict(new { message = "Audiobook already exists in library", audiobook = existingByAsin });
                }
            }
            
            if (!string.IsNullOrEmpty(metadata.Isbn))
            {
                var existingByIsbn = await _repo.GetByIsbnAsync(metadata.Isbn);
                if (existingByIsbn != null)
                {
                    return Conflict(new { message = "Audiobook already exists in library", audiobook = existingByIsbn });
                }
            }
            
            // Move image from temp cache to permanent library storage
            string? imageUrl = metadata.ImageUrl;
            if (!string.IsNullOrEmpty(metadata.Asin))
            {
                try
                {
                    var libraryImagePath = await _imageCacheService.MoveToLibraryStorageAsync(metadata.Asin);
                    if (libraryImagePath != null)
                    {
                        imageUrl = $"/api/images/{metadata.Asin}";
                        _logger.LogInformation("Moved image for ASIN {Asin} to permanent library storage", metadata.Asin);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to move image for ASIN {Asin}, image may not be in temp cache", metadata.Asin);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error moving image for ASIN {Asin} to library storage", metadata.Asin);
                    // Continue with original image URL if move fails
                }
            }
            
            // Convert metadata to Audiobook entity and save to database
            var audiobook = new Audiobook
            {
                Title = metadata.Title,
                Subtitle = metadata.Subtitle,
                Authors = metadata.Authors,
                ImageUrl = imageUrl,
                PublishYear = metadata.PublishYear,
                Series = metadata.Series,
                SeriesNumber = metadata.SeriesNumber,
                Description = metadata.Description,
                Genres = metadata.Genres,
                Tags = metadata.Tags,
                Narrators = metadata.Narrators,
                Isbn = metadata.Isbn,
                Asin = metadata.Asin,
                Publisher = metadata.Publisher,
                Language = metadata.Language,
                Runtime = metadata.Runtime,
                Version = metadata.Version,
                Explicit = metadata.Explicit,
                Abridged = metadata.Abridged,
                Monitored = request.Monitored  // Use custom monitored setting
            };
            
            // Assign quality profile - use custom if provided, otherwise default
            if (request.QualityProfileId.HasValue)
            {
                audiobook.QualityProfileId = request.QualityProfileId.Value;
                _logger.LogInformation("Assigned custom quality profile ID {ProfileId} to new audiobook '{Title}'", 
                    request.QualityProfileId.Value, audiobook.Title);
            }
            else
            {
                // Assign default quality profile to new audiobooks
                using (var scope = _serviceProvider.CreateScope())
                {
                    var qualityProfileService = scope.ServiceProvider.GetRequiredService<IQualityProfileService>();
                    var defaultProfile = await qualityProfileService.GetDefaultAsync();
                    if (defaultProfile != null)
                    {
                        audiobook.QualityProfileId = defaultProfile.Id;
                        _logger.LogInformation("Assigned default quality profile '{ProfileName}' (ID: {ProfileId}) to new audiobook '{Title}'", 
                            defaultProfile.Name, defaultProfile.Id, audiobook.Title);
                    }
                    else
                    {
                        _logger.LogWarning("No default quality profile found. New audiobook '{Title}' will not have a quality profile assigned.", audiobook.Title);
                    }
                }
            }
            
            await _repo.AddAsync(audiobook);
            
            // Log history entry for the added audiobook
            var historyEntry = new History
            {
                AudiobookId = audiobook.Id,
                AudiobookTitle = audiobook.Title ?? "Unknown Title",
                EventType = "Added",
                Message = $"Audiobook '{audiobook.Title}' added to library from Add New page",
                Source = "AddNew",
                Timestamp = DateTime.UtcNow
            };
            
            _dbContext.History.Add(historyEntry);
            await _dbContext.SaveChangesAsync();
            
            _logger.LogInformation("Added audiobook '{Title}' (ASIN: {Asin}) to library with Monitored={Monitored}, QualityProfileId={QualityProfileId}, AutoSearch={AutoSearch}", 
                audiobook.Title, audiobook.Asin, request.Monitored, audiobook.QualityProfileId, request.AutoSearch);
            
            return Ok(new { message = "Audiobook added to library successfully", audiobook });
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var audiobooks = await _repo.GetAllAsync();
            return Ok(audiobooks);
        }

        [HttpGet("by-asin/{asin}")]
        public async Task<IActionResult> GetByAsin(string asin)
        {
            var book = await _repo.GetByAsinAsync(asin);
            if (book == null) return NotFound();
            return Ok(book);
        }

        [HttpGet("by-isbn/{isbn}")]
        public async Task<IActionResult> GetByIsbn(string isbn)
        {
            var book = await _repo.GetByIsbnAsync(isbn);
            if (book == null) return NotFound();
            return Ok(book);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Audiobook>> GetAudiobook(int id)
        {
            var audiobook = await _repo.GetByIdAsync(id);
            if (audiobook == null)
            {
                return NotFound(new { message = "Audiobook not found" });
            }
            // Include QualityProfile and Files in the query
            audiobook = await _dbContext.Audiobooks
                .Include(a => a.QualityProfile)
                .Include(a => a.Files)
                .FirstOrDefaultAsync(a => a.Id == id);
            return Ok(audiobook);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateAudiobook(int id, [FromBody] Audiobook updatedAudiobook)
        {
            var existingAudiobook = await _repo.GetByIdAsync(id);
            if (existingAudiobook == null)
            {
                return NotFound(new { message = "Audiobook not found" });
            }

            // Update properties
            existingAudiobook.Title = updatedAudiobook.Title;
            existingAudiobook.Subtitle = updatedAudiobook.Subtitle;
            existingAudiobook.Authors = updatedAudiobook.Authors;
            existingAudiobook.ImageUrl = updatedAudiobook.ImageUrl;
            existingAudiobook.PublishYear = updatedAudiobook.PublishYear;
            existingAudiobook.Series = updatedAudiobook.Series;
            existingAudiobook.SeriesNumber = updatedAudiobook.SeriesNumber;
            existingAudiobook.Description = updatedAudiobook.Description;
            existingAudiobook.Genres = updatedAudiobook.Genres;
            existingAudiobook.Tags = updatedAudiobook.Tags;
            existingAudiobook.Narrators = updatedAudiobook.Narrators;
            existingAudiobook.Isbn = updatedAudiobook.Isbn;
            existingAudiobook.Asin = updatedAudiobook.Asin;
            existingAudiobook.Publisher = updatedAudiobook.Publisher;
            existingAudiobook.Language = updatedAudiobook.Language;
            existingAudiobook.Runtime = updatedAudiobook.Runtime;
            existingAudiobook.Version = updatedAudiobook.Version;
            existingAudiobook.Explicit = updatedAudiobook.Explicit;
            existingAudiobook.Abridged = updatedAudiobook.Abridged;
            existingAudiobook.Monitored = updatedAudiobook.Monitored;
            existingAudiobook.FilePath = updatedAudiobook.FilePath;
            existingAudiobook.FileSize = updatedAudiobook.FileSize;
            existingAudiobook.Quality = updatedAudiobook.Quality;
            existingAudiobook.QualityProfileId = updatedAudiobook.QualityProfileId;

            await _repo.UpdateAsync(existingAudiobook);

            _logger.LogInformation("Updated audiobook '{Title}' (ID: {Id})", existingAudiobook.Title, id);

            return Ok(new { message = "Audiobook updated successfully", audiobook = existingAudiobook });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteAudiobook(int id)
        {
            var audiobook = await _repo.GetByIdAsync(id);
            if (audiobook == null)
            {
                return NotFound(new { message = "Audiobook not found" });
            }

            // Delete associated image from cache if it exists
            if (!string.IsNullOrEmpty(audiobook.Asin))
            {
                try
                {
                    var imagePath = await _imageCacheService.GetCachedImagePathAsync(audiobook.Asin);
                    if (imagePath != null)
                    {
                        var fullPath = Path.Combine(Directory.GetCurrentDirectory(), imagePath);
                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                            _logger.LogInformation("Deleted cached image for ASIN {Asin}", audiobook.Asin);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete cached image for ASIN {Asin}", audiobook.Asin);
                    // Continue with deletion even if image cleanup fails
                }
            }

            var deleted = await _repo.DeleteByIdAsync(id);
            if (deleted)
            {
                return Ok(new { message = "Audiobook deleted successfully", id });
            }

            return StatusCode(500, new { message = "Failed to delete audiobook" });
        }

        [HttpPost("delete-bulk")]
        public async Task<IActionResult> DeleteBulk([FromBody] BulkDeleteRequest request)
        {
            if (request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(new { message = "No IDs provided for deletion" });
            }

            // Get audiobooks to delete (for image cleanup)
            var audiobooks = new List<Audiobook>();
            foreach (var id in request.Ids)
            {
                var audiobook = await _repo.GetByIdAsync(id);
                if (audiobook != null)
                {
                    audiobooks.Add(audiobook);
                }
            }

            // Delete associated images from cache
            var deletedImagesCount = 0;
            foreach (var audiobook in audiobooks)
            {
                if (!string.IsNullOrEmpty(audiobook.Asin))
                {
                    try
                    {
                        var imagePath = await _imageCacheService.GetCachedImagePathAsync(audiobook.Asin);
                        if (imagePath != null)
                        {
                            var fullPath = Path.Combine(Directory.GetCurrentDirectory(), imagePath);
                            if (System.IO.File.Exists(fullPath))
                            {
                                System.IO.File.Delete(fullPath);
                                deletedImagesCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete cached image for ASIN {Asin}", audiobook.Asin);
                        // Continue with deletion even if image cleanup fails
                    }
                }
            }

            var deletedCount = await _repo.DeleteBulkAsync(request.Ids);
            
            _logger.LogInformation("Bulk delete: {DeletedCount} audiobooks and {ImageCount} images deleted", deletedCount, deletedImagesCount);
            
            return Ok(new 
            { 
                message = $"Successfully deleted {deletedCount} audiobook(s)", 
                deletedCount,
                deletedImagesCount,
                ids = request.Ids 
            });
        }

    [HttpPost("bulk-update")]
    public async Task<IActionResult> BulkUpdate([FromBody] BulkUpdateRequest request)
        {
            if (request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(new { message = "No IDs provided for update" });
            }
            if (request.Updates == null || !request.Updates.Any())
            {
                return BadRequest(new { message = "No updates provided" });
            }

            var results = new List<object>();
            foreach (var id in request.Ids)
            {
                var audiobook = await _repo.GetByIdAsync(id);
                if (audiobook == null)
                {
                    results.Add(new { id, success = false, error = "Audiobook not found" });
                    continue;
                }

                bool anySuccess = false;
                var errors = new List<string>();
                foreach (var kvp in request.Updates)
                {
                    var prop = typeof(Audiobook).GetProperty(kvp.Key, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (prop == null || !prop.CanWrite)
                    {
                        errors.Add($"Property '{kvp.Key}' not found or not writable");
                        continue;
                    }
                    try
                    {
                        var converted = ConvertUpdateValue(kvp.Value, prop.PropertyType);
                        prop.SetValue(audiobook, converted);
                        anySuccess = true;
                    }
                    catch (Exception ex)
                    {
                        var incomingType = kvp.Value?.GetType().FullName ?? "null";
                        var raw = kvp.Value is JsonElement j ? j.GetRawText() : kvp.Value?.ToString();
                        var msg = $"Failed to set '{kvp.Key}': {ex.Message} (incomingType={incomingType}, raw={raw})";
                        errors.Add(msg);
                        _logger.LogError(ex, "Bulk update conversion error for property {Property} on audiobook {Id}: {Msg}", kvp.Key, id, msg);
                    }
                }
                if (anySuccess)
                {
                    await _repo.UpdateAsync(audiobook);
                }
                results.Add(new { id, success = anySuccess, errors });
            }
            return Ok(new { message = "Bulk update completed", results });
        }

        [HttpPost("{id}/search")]
        public async Task<IActionResult> TriggerAutomaticSearch(int id)
        {
            var audiobook = await _repo.GetByIdAsync(id);
            if (audiobook == null)
            {
                return NotFound(new { message = "Audiobook not found" });
            }

            if (!audiobook.Monitored)
            {
                return BadRequest(new { message = "Audiobook is not monitored" });
            }

            if (audiobook.QualityProfile == null)
            {
                return BadRequest(new { message = "Audiobook has no quality profile assigned" });
            }

            try
            {
                // Get required services
                using var scope = _serviceProvider.CreateScope();
                var searchService = scope.ServiceProvider.GetRequiredService<ISearchService>();
                var qualityProfileService = scope.ServiceProvider.GetRequiredService<IQualityProfileService>();
                var downloadService = scope.ServiceProvider.GetRequiredService<IDownloadService>();

                // Process the audiobook using the same logic as AutomaticSearchService
                var downloadsQueued = await ProcessAudiobookForSearchAsync(
                    audiobook, searchService, qualityProfileService, downloadService, _dbContext);

                // Update last search time
                audiobook.LastSearchTime = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();

                _logger.LogInformation("Manual search triggered for audiobook '{Title}' - queued {QueuedCount} downloads",
                    audiobook.Title, downloadsQueued);

                return Ok(new { 
                    message = $"Search completed for audiobook '{audiobook.Title}'", 
                    downloadsQueued,
                    audiobookId = id
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during manual search for audiobook '{Title}' (ID: {Id})", audiobook.Title, id);
                return StatusCode(500, new { message = "Error during search", error = ex.Message });
            }
        }

        private async Task<int> ProcessAudiobookForSearchAsync(
            Audiobook audiobook,
            ISearchService searchService,
            IQualityProfileService qualityProfileService,
            IDownloadService downloadService,
            ListenArrDbContext dbContext)
        {
            // Check if quality cutoff is already met
            if (await IsQualityCutoffMetAsync(audiobook, qualityProfileService, dbContext))
            {
                _logger.LogInformation("Quality cutoff already met for audiobook '{Title}', skipping search", audiobook.Title);
                return 0;
            }

            // Build search query
            var searchQuery = BuildSearchQuery(audiobook);
            _logger.LogInformation("Searching for audiobook '{Title}' with query: {Query}", audiobook.Title, searchQuery);

            // Search for results
            var searchResults = await searchService.SearchAsync(searchQuery);
            _logger.LogInformation("Found {Count} raw search results for audiobook '{Title}'", searchResults.Count, audiobook.Title);
            
            if (!searchResults.Any())
            {
                _logger.LogInformation("No search results found for audiobook '{Title}'", audiobook.Title);
                return 0;
            }

            // Score results against quality profile
            var scoredResults = await qualityProfileService.ScoreSearchResults(searchResults, audiobook.QualityProfile!);
            
            // Log all scored results for debugging
            _logger.LogInformation("Scored {Count} search results for audiobook '{Title}':", scoredResults.Count, audiobook.Title);
            foreach (var scoredResult in scoredResults.OrderByDescending(s => s.TotalScore))
            {
                var status = scoredResult.IsRejected ? "REJECTED" : (scoredResult.TotalScore > 0 ? "ACCEPTABLE" : "LOW SCORE");
                _logger.LogInformation("  [{Status}] Score: {Score} | Title: {Title} | Source: {Source} | Size: {Size}MB | Seeders: {Seeders} | Quality: {Quality}",
                    status, scoredResult.TotalScore, scoredResult.SearchResult.Title, scoredResult.SearchResult.Source, 
                    scoredResult.SearchResult.Size / 1024 / 1024, scoredResult.SearchResult.Seeders, scoredResult.SearchResult.Quality);
                
                if (scoredResult.IsRejected && scoredResult.RejectionReasons.Any())
                {
                    _logger.LogInformation("    Rejection reasons: {Reasons}", string.Join(", ", scoredResult.RejectionReasons));
                }
            }
            
            var topResult = scoredResults
                .Where(s => !s.IsRejected && s.TotalScore > 0) // Only results that pass quality filters and are not rejected
                .OrderByDescending(s => s.TotalScore)
                .FirstOrDefault(); // Pick only the top scoring result

            if (topResult == null)
            {
                _logger.LogInformation("No acceptable search results found for audiobook '{Title}' after quality filtering", audiobook.Title);
                return 0;
            }

            _logger.LogInformation("Found top result for audiobook '{Title}': {ResultTitle} (Score: {Score})",
                audiobook.Title, topResult.SearchResult.Title, topResult.TotalScore);

            // Add score to the search result for tracking
            topResult.SearchResult.Score = topResult.TotalScore;

            // Queue download for the top result
            var downloadsQueued = 0;
            try
            {
                // Determine appropriate download client for this result
                var isTorrent = IsTorrentResult(topResult.SearchResult);
                var downloadClientId = await GetAppropriateDownloadClientAsync(topResult.SearchResult, isTorrent);

                if (string.IsNullOrEmpty(downloadClientId))
                {
                    _logger.LogWarning("No suitable download client found for result type: {Type}", isTorrent ? "torrent" : "NZB");
                    return 0;
                }

                await downloadService.StartDownloadAsync(topResult.SearchResult, downloadClientId, audiobook.Id);
                downloadsQueued++;

                _logger.LogInformation("Queued download for audiobook '{Title}': {ResultTitle} (Score: {Score})",
                    audiobook.Title, topResult.SearchResult.Title, topResult.TotalScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to queue download for audiobook '{Title}': {ResultTitle}",
                    audiobook.Title, topResult.SearchResult.Title);
            }

            return downloadsQueued;
        }

        private async Task<bool> IsQualityCutoffMetAsync(
            Audiobook audiobook,
            IQualityProfileService qualityProfileService,
            ListenArrDbContext dbContext)
        {
            if (audiobook.QualityProfile == null)
                return false;

            // Get existing downloads for this audiobook
            var existingDownloads = await dbContext.Downloads
                .Where(d => d.AudiobookId == audiobook.Id &&
                           (d.Status == DownloadStatus.Completed || d.Status == DownloadStatus.Downloading))
                .ToListAsync();

            if (!existingDownloads.Any())
                return false;

            // Check if any existing download meets or exceeds the cutoff quality
            var cutoffQuality = audiobook.QualityProfile.Qualities
                .FirstOrDefault(q => q.Quality == audiobook.QualityProfile.CutoffQuality);

            if (cutoffQuality == null)
                return false;

            foreach (var download in existingDownloads)
            {
                // For completed downloads, check if the file quality meets cutoff
                if (download.Status == DownloadStatus.Completed && !string.IsNullOrEmpty(download.Metadata?.GetValueOrDefault("Quality")?.ToString()))
                {
                    var downloadQuality = download.Metadata["Quality"].ToString();
                    var downloadQualityDefinition = audiobook.QualityProfile.Qualities
                        .FirstOrDefault(q => q.Quality == downloadQuality);

                    if (downloadQualityDefinition != null && downloadQualityDefinition.Priority >= cutoffQuality.Priority)
                    {
                        _logger.LogDebug("Quality cutoff met for audiobook '{Title}' by completed download (Quality: {Quality})",
                            audiobook.Title, downloadQuality);
                        return true;
                    }
                }
                // For active downloads, assume they will meet quality requirements
                else if (download.Status == DownloadStatus.Downloading)
                {
                    _logger.LogDebug("Quality cutoff assumed met for audiobook '{Title}' due to active download", audiobook.Title);
                    return true;
                }
            }

            return false;
        }

        private string BuildSearchQuery(Audiobook audiobook)
        {
            var parts = new List<string>();

            // Add title
            if (!string.IsNullOrEmpty(audiobook.Title))
                parts.Add(audiobook.Title);

            // Add primary author
            if (audiobook.Authors != null && audiobook.Authors.Any())
                parts.Add(audiobook.Authors.First());

            // Add series if available
            if (!string.IsNullOrEmpty(audiobook.Series))
                parts.Add(audiobook.Series);

            return string.Join(" ", parts);
        }

        private bool IsTorrentResult(SearchResult result)
        {
            // Check DownloadType first if it's set
            if (!string.IsNullOrEmpty(result.DownloadType))
            {
                if (result.DownloadType == "DDL")
                {
                    return false; // DDL is not a torrent
                }
                else if (result.DownloadType == "Torrent")
                {
                    return true;
                }
                else if (result.DownloadType == "Usenet")
                {
                    return false;
                }
            }

            // Fallback to legacy detection logic
            // Check for NZB first - if it has an NZB URL, it's a Usenet/NZB download
            if (!string.IsNullOrEmpty(result.NzbUrl))
            {
                return false;
            }

            // Check for torrent indicators - magnet link or torrent file
            if (!string.IsNullOrEmpty(result.MagnetLink) || !string.IsNullOrEmpty(result.TorrentUrl))
            {
                return true;
            }

            // If neither is set, we can't reliably determine the type
            // Log a warning and default to false (NZB) as a safer choice
            _logger.LogWarning("Unable to determine result type for '{Title}' from source '{Source}'. No MagnetLink, TorrentUrl, or NzbUrl found. Defaulting to NZB.",
                result.Title, result.Source);
            return false;
        }

        private async Task<string> GetAppropriateDownloadClientAsync(SearchResult searchResult, bool isTorrent)
        {
            using var scope = _serviceProvider.CreateScope();
            var configurationService = scope.ServiceProvider.GetRequiredService<IConfigurationService>();

            // Special handling for DDL downloads - they don't use external clients
            if (searchResult.DownloadType?.Equals("DDL", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogInformation("DDL download detected, using internal DDL client");
                return "DDL";
            }

            // Get all configured download clients
            var clients = await configurationService.GetDownloadClientConfigurationsAsync();
            var enabledClients = clients.Where(c => c.IsEnabled).ToList();

            _logger.LogInformation("Looking for {ClientType} client. Found {Count} enabled download clients: {Clients}",
                isTorrent ? "torrent" : "NZB",
                enabledClients.Count,
                string.Join(", ", enabledClients.Select(c => $"{c.Name} ({c.Type})")));

            if (isTorrent)
            {
                // Prefer qBittorrent, then Transmission
                var client = enabledClients.FirstOrDefault(c => c.Type.Equals("qbittorrent", StringComparison.OrdinalIgnoreCase))
                          ?? enabledClients.FirstOrDefault(c => c.Type.Equals("transmission", StringComparison.OrdinalIgnoreCase));

                if (client != null)
                {
                    _logger.LogInformation("Selected torrent client: {ClientName} ({ClientType})", client.Name, client.Type);
                }
                else
                {
                    _logger.LogWarning("No torrent client (qBittorrent or Transmission) found among enabled clients");
                }

                return client?.Id ?? string.Empty;
            }
            else
            {
                // Prefer SABnzbd, then NZBGet
                var client = enabledClients.FirstOrDefault(c => c.Type.Equals("sabnzbd", StringComparison.OrdinalIgnoreCase))
                          ?? enabledClients.FirstOrDefault(c => c.Type.Equals("nzbget", StringComparison.OrdinalIgnoreCase));

                if (client != null)
                {
                    _logger.LogInformation("Selected NZB client: {ClientName} ({ClientType})", client.Name, client.Type);
                }
                else
                {
                    _logger.LogWarning("No NZB client (SABnzbd or NZBGet) found among enabled clients");
                }

                return client?.Id ?? string.Empty;
            }
        }

        // Helper to convert incoming update values (possibly JsonElement or boxed types) to the target property type
        private static object? ConvertUpdateValue(object? value, Type targetType)
        {
            if (value == null)
            {
                if (targetType == typeof(string)) return string.Empty;
                if (targetType.IsValueType) return Activator.CreateInstance(targetType);
                return null;
            }

            // Unwrap JsonElement if present (from System.Text.Json)
            if (value is JsonElement je)
            {
                try
                {
                    if (je.ValueKind == JsonValueKind.Number && (targetType == typeof(int) || targetType == typeof(int?)))
                        return je.GetInt32();
                    if (je.ValueKind == JsonValueKind.Number && targetType == typeof(double))
                        return je.GetDouble();
                    if (je.ValueKind == JsonValueKind.True || je.ValueKind == JsonValueKind.False)
                        return je.GetBoolean();
                    if (je.ValueKind == JsonValueKind.String)
                        return je.GetString();
                    // Fall back to raw string
                    return je.GetRawText();
                }
                catch
                {
                    // continue to other conversion attempts
                }
            }

            // Handle nullable types
            var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Enums
            if (underlying.IsEnum)
            {
                if (value is string s)
                    return Enum.Parse(underlying, s, true);
                return Enum.ToObject(underlying, Convert.ChangeType(value, Enum.GetUnderlyingType(underlying)));
            }

            // If value already matches
            if (underlying.IsInstanceOfType(value))
                return value;

            // Try Convert.ChangeType on primitives
            try
            {
                return Convert.ChangeType(value, underlying);
            }
            catch
            {
                // Final fallback: attempt parse from string
                var str = value.ToString();
                if (underlying == typeof(int) && int.TryParse(str, out var i)) return i;
                if (underlying == typeof(double) && double.TryParse(str, out var d)) return d;
                if (underlying == typeof(bool) && bool.TryParse(str, out var b)) return b;
                if (underlying == typeof(string)) return str;
            }

            // As a last resort, return the original value
            return value;
        }

        }

    public class BulkDeleteRequest
    {
        public List<int> Ids { get; set; } = new List<int>();
    }

    public class BulkUpdateRequest
    {
        public List<int> Ids { get; set; } = new List<int>();
        public Dictionary<string, object> Updates { get; set; } = new Dictionary<string, object>();
    }

    public class AddToLibraryRequest
    {
        public AudibleBookMetadata Metadata { get; set; } = new();
        public bool Monitored { get; set; } = true;
        public int? QualityProfileId { get; set; }
        public bool AutoSearch { get; set; } = false;
    }
}

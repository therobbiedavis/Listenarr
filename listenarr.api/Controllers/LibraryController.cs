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
        
        public LibraryController(
            IAudiobookRepository repo, 
            IImageCacheService imageCacheService, 
            ILogger<LibraryController> logger,
            ListenArrDbContext dbContext)
        {
            _repo = repo;
            _imageCacheService = imageCacheService;
            _logger = logger;
            _dbContext = dbContext;
        }

        [HttpPost("add")]
        public async Task<IActionResult> AddToLibrary([FromBody] AudibleBookMetadata metadata)
        {
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
                Monitored = true  // Enable monitoring by default for new audiobooks
            };
            
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
            
            _logger.LogInformation("Added audiobook '{Title}' (ASIN: {Asin}) to library with history log", audiobook.Title, audiobook.Asin);
            
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
        public async Task<IActionResult> GetById(int id)
        {
            var audiobook = await _repo.GetByIdAsync(id);
            if (audiobook == null)
            {
                return NotFound(new { message = "Audiobook not found" });
            }
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
    }

    public class BulkDeleteRequest
    {
        public List<int> Ids { get; set; } = new List<int>();
    }
}

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

namespace Listenarr.Api.Controllers
{
    [ApiController]
    [Route("api/images")]
    public class ImagesController : ControllerBase
    {
        private readonly IImageCacheService _imageCacheService;
        private readonly IAudiobookMetadataService _audiobookMetadataService;
        private readonly AudimetaService _audimetaService;
        private readonly IAudnexusService _audnexusService;
        private readonly IAudiobookRepository _audiobookRepository;
        private readonly ILogger<ImagesController> _logger;
        private readonly IWebHostEnvironment _environment;

        public ImagesController(
            IImageCacheService imageCacheService,
            IAudiobookMetadataService audiobookMetadataService,
            AudimetaService audimetaService,
            IAudnexusService audnexusService,
            IAudiobookRepository audiobookRepository,
            ILogger<ImagesController> logger,
            IWebHostEnvironment environment)
        {
            _imageCacheService = imageCacheService;
            _audiobookMetadataService = audiobookMetadataService;
            _audimetaService = audimetaService;
            _audnexusService = audnexusService;
            _audiobookRepository = audiobookRepository;
            _logger = logger;
            _environment = environment;
        }

        [HttpGet("{identifier}")]
        public async Task<IActionResult> GetImage(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return BadRequest("Identifier is required");
            }

            // Strip any query parameters from the identifier (e.g., "B0CQZ5167B?access_token=..." -> "B0CQZ5167B")
            var queryIndex = identifier.IndexOf('?');
            if (queryIndex >= 0)
            {
                identifier = identifier.Substring(0, queryIndex);
            }

            // Check for url parameter to download on demand
            var url = Request.Query["url"].ToString();
            if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("http://") || url.StartsWith("https://")))
            {
                // Try to download and cache the image
                try
                {
                    var downloaded = await _imageCacheService.DownloadAndCacheImageAsync(url, identifier);
                    if (!string.IsNullOrWhiteSpace(downloaded))
                    {
                        _logger.LogInformation("Downloaded image on demand for identifier: {Identifier}", identifier);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to download image on demand for identifier: {Identifier}", identifier);
                }
            }

            try
            {
                // Get the cached image path (checks library first, then temp)
                var relativePath = await _imageCacheService.GetCachedImagePathAsync(identifier);

                // If we found a temp cached image but the identifier corresponds to an audiobook in the library,
                // attempt to move it into permanent library storage so library images don't live in /temp.
                if (!string.IsNullOrWhiteSpace(relativePath) && relativePath.Contains("cache/images/temp/"))
                {
                    try
                    {
                        var book = await _audiobookRepository.GetByAsinAsync(identifier);
                        if (book != null)
                        {
                            _logger.LogInformation("Found temp cached image for library audiobook {Identifier}, attempting move to library storage", identifier);
                            var moved = await _imageCacheService.MoveToLibraryStorageAsync(identifier);
                            if (!string.IsNullOrWhiteSpace(moved))
                            {
                                // Prefer the moved library path when serving the image
                                relativePath = moved;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to move temp image to library for {Identifier}", identifier);
                    }
                }

                if (relativePath == null)
                {
                    _logger.LogWarning("Image not found for identifier: {Identifier}", identifier);

                    // Try to fetch metadata-driven image candidates and download on-demand
                    try
                    {
                        var region = Request.Query["region"].ToString();
                        if (string.IsNullOrWhiteSpace(region)) region = "us";

                        var audimeta = await _audiobookMetadataService.GetAudimetaMetadataAsync(identifier, region, cache: true);
                        string? candidateUrl = null;

                        if (audimeta != null)
                        {
                            candidateUrl = audimeta.ImageUrl ?? audimeta.Description;
                        }

                        // If audimeta didn't yield an image, call GetMetadataAsync to allow other providers (Audnexus) to be used
                        if (string.IsNullOrWhiteSpace(candidateUrl))
                        {
                            _logger.LogDebug("No image found in audimeta, attempting fallback GetMetadataAsync for {Identifier}", identifier);
                            try
                            {
                                var metadataEnvelope = await _audiobookMetadataService.GetMetadataAsync(identifier, region, cache: true);
                                if (metadataEnvelope != null)
                                {
                                    try
                                    {
                                        // If the service returned an AudimetaBookResponse directly
                                        if (metadataEnvelope is global::Listenarr.Api.Services.AudimetaBookResponse directMeta)
                                        {
                                            candidateUrl = directMeta.ImageUrl ?? directMeta.Description;
                                        }
                                        else
                                        {
                                            // Try dynamic access
                                            dynamic env = metadataEnvelope;
                                            object? mdObj = env.metadata as object;

                                            // If it's already the Audimeta type, use it
                                            if (mdObj is global::Listenarr.Api.Services.AudimetaBookResponse mdMeta)
                                            {
                                                candidateUrl = mdMeta.ImageUrl ?? mdMeta.Description;
                                            }
                                            else if (mdObj != null)
                                            {
                                                // Try reflection for common property names
                                                var t = mdObj.GetType();
                                                var prop = t.GetProperty("ImageUrl") ?? t.GetProperty("Image") ?? t.GetProperty("image") ?? t.GetProperty("imageUrl");
                                                if (prop != null)
                                                {
                                                    var v = prop.GetValue(mdObj)?.ToString();
                                                    if (!string.IsNullOrWhiteSpace(v)) candidateUrl = v;
                                                }
                                            }
                                        }

                                        if (!string.IsNullOrWhiteSpace(candidateUrl))
                                        {
                                            _logger.LogInformation("Found image URL in fallback metadata source for identifier {Identifier}: {Url}", identifier, candidateUrl);
                                        }
                                        else
                                        {
                                            _logger.LogDebug("Fallback metadata returned no image URL for {Identifier}", identifier);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogDebug(ex, "Failed to parse fallback metadata envelope for {Identifier}", identifier);
                                    }
                                }
                                else
                                {
                                    _logger.LogDebug("GetMetadataAsync returned null for {Identifier}", identifier);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Fallback metadata lookup failed for {Identifier}", identifier);
                            }
                        }

                        // If no image found from book metadata, attempt author lookups (treating identifier as author name/asin)
                        if (string.IsNullOrWhiteSpace(candidateUrl))
                        {
                            try
                            {
                                // 1) Audimeta author lookup by name
                                var authorLookup = await _audimetaService.LookupAuthorAsync(identifier, region);
                                if (authorLookup != null && !string.IsNullOrWhiteSpace(authorLookup.Image) && (authorLookup.Image.StartsWith("http://") || authorLookup.Image.StartsWith("https://")))
                                {
                                    candidateUrl = authorLookup.Image;
                                    _logger.LogInformation("Found author image from Audimeta for identifier {Identifier}: {Url}", identifier, candidateUrl);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogDebug(ex, "Audimeta author lookup failed for {Identifier}", identifier);
                            }

                            // 2) Audnexus author search fallback
                            if (string.IsNullOrWhiteSpace(candidateUrl))
                            {
                                try
                                {
                                    // If identifier looks like an ASIN, prefer GetAuthorAsync to fetch the author directly
                                    if (identifier != null && identifier.Length >= 10 && (identifier.StartsWith("B", StringComparison.OrdinalIgnoreCase) || identifier.All(char.IsLetterOrDigit)))
                                    {
                                        try
                                        {
                                            var authorResp = await _audnexusService.GetAuthorAsync(identifier, region, update: false);
                                            if (authorResp != null && !string.IsNullOrWhiteSpace(authorResp.Image) && (authorResp.Image.StartsWith("http://") || authorResp.Image.StartsWith("https://")))
                                            {
                                                candidateUrl = authorResp.Image;
                                                _logger.LogInformation("Found author image from Audnexus (by ASIN) for identifier {Identifier}: {Url}", identifier, candidateUrl);
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogDebug(ex, "Audnexus GetAuthorAsync failed for ASIN {Identifier}", identifier);
                                        }
                                    }

                                    // If still not found, fallback to searching by name
                                    if (string.IsNullOrWhiteSpace(candidateUrl))
                                    {
                                        // Try to find stored author ASIN in database (match by author name) and prefer direct GET
                                        try
                                        {
                                            var books = await _audiobookRepository.GetAllAsync();
                                            var match = books.SelectMany(b => (b.Authors ?? new List<string>()).Select((name, idx) => new { book = b, name, idx }))
                                                             .FirstOrDefault(x => string.Equals(x.name?.Trim(), identifier?.Trim(), StringComparison.OrdinalIgnoreCase) && ((x.book.AuthorAsins?.Count ?? 0) > 0));
                                            if (match != null)
                                            {
                                                var authorAsin = match.book.AuthorAsins?.FirstOrDefault();
                                                if (!string.IsNullOrWhiteSpace(authorAsin))
                                                {
                                                    try
                                                    {
                                                        var authorResp = await _audnexusService.GetAuthorAsync(authorAsin, region, update: false);
                                                        if (authorResp != null && !string.IsNullOrWhiteSpace(authorResp.Image) && (authorResp.Image.StartsWith("http://") || authorResp.Image.StartsWith("https://")))
                                                        {
                                                            candidateUrl = authorResp.Image;
                                                            _logger.LogInformation("Found author image from Audnexus by stored ASIN {Asin} for identifier {Identifier}: {Url}", authorAsin, identifier, candidateUrl);
                                                        }
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        _logger.LogDebug(ex, "Audnexus GetAuthorAsync failed for ASIN {Asin}", authorAsin);
                                                    }
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            _logger.LogDebug(ex, "Failed to lookup author ASINs in database for identifier {Identifier}", identifier);
                                        }

                                        // If still not found, fallback to searching by name
                                        if (string.IsNullOrWhiteSpace(candidateUrl))
                                        {
                                            var authors = await _audnexusService.SearchAuthorsAsync(identifier!, region);
                                            var first = authors?.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Image));
                                            if (first != null && !string.IsNullOrWhiteSpace(first.Image) && (first.Image.StartsWith("http://") || first.Image.StartsWith("https://")))
                                            {
                                                candidateUrl = first.Image;
                                                _logger.LogInformation("Found author image from Audnexus (search) for identifier {Identifier}: {Url}", identifier, candidateUrl);
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogDebug(ex, "Audnexus author search failed for {Identifier}", identifier);
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(candidateUrl) && (candidateUrl.StartsWith("http://") || candidateUrl.StartsWith("https://")))
                        {
                            _logger.LogInformation("Attempting metadata-driven image download for identifier {Identifier} from {Url}", identifier, candidateUrl);
                            try
                            {
                                _logger.LogDebug("Calling DownloadAndCacheImageAsync for {Identifier} from {Url}", identifier, candidateUrl);
                                var downloaded = await _imageCacheService.DownloadAndCacheImageAsync(candidateUrl, identifier!);
                                if (!string.IsNullOrWhiteSpace(downloaded))
                                {
                                    _logger.LogInformation("Downloaded metadata image for identifier: {Identifier}", identifier);
                                    // Re-check cache
                                    relativePath = await _imageCacheService.GetCachedImagePathAsync(identifier!);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Failed to download metadata-driven image for {Identifier}", identifier);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Metadata-driven image download failed for {Identifier}", identifier);
                    }

                    if (relativePath == null)
                    {
                        // Attempt to serve the frontend placeholder first (fe/public/placeholder.svg)
                        try
                        {
                            var frontendPlaceholder = Path.Combine(_environment.ContentRootPath, "..", "fe", "public", "placeholder.svg");
                            if (System.IO.File.Exists(frontendPlaceholder))
                            {
                                _logger.LogInformation("Serving frontend placeholder image for missing identifier: {Identifier}", identifier);
                                Response.Headers["Cache-Control"] = "public, max-age=300";
                                return PhysicalFile(frontendPlaceholder, "image/svg+xml");
                            }

                            // Fallback to backend wwwroot placeholder if frontend file not present
                            var placeholderPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "placeholder.svg");
                            if (System.IO.File.Exists(placeholderPath))
                            {
                                _logger.LogInformation("Serving backend placeholder image for missing identifier: {Identifier}", identifier);
                                Response.Headers["Cache-Control"] = "public, max-age=300";
                                return PhysicalFile(placeholderPath, "image/svg+xml");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to serve placeholder for missing image {Identifier}", identifier);
                        }

                        // Return NotFound with short caching to reduce repeated filesystem lookups by other clients
                        Response.Headers["Cache-Control"] = "public, max-age=300";
                        return NotFound(new { message = "Image not found" });
                    }
                }

                // Build the full file path
                var fullPath = Path.Combine(_environment.ContentRootPath, relativePath);

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning("Image file does not exist at path: {Path}", fullPath);
                    // Try to serve the frontend placeholder first, then backend placeholder
                    try
                    {
                        var frontendPlaceholder = Path.Combine(_environment.ContentRootPath, "..", "fe", "public", "placeholder.svg");
                        if (System.IO.File.Exists(frontendPlaceholder))
                        {
                            _logger.LogInformation("Serving frontend placeholder image for missing file at path: {Path}", fullPath);
                            Response.Headers["Cache-Control"] = "public, max-age=300";
                            return PhysicalFile(frontendPlaceholder, "image/svg+xml");
                        }

                        var placeholderPath = Path.Combine(_environment.ContentRootPath, "wwwroot", "placeholder.svg");
                        if (System.IO.File.Exists(placeholderPath))
                        {
                            _logger.LogInformation("Serving backend placeholder image for missing file at path: {Path}", fullPath);
                            Response.Headers["Cache-Control"] = "public, max-age=300";
                            return PhysicalFile(placeholderPath, "image/svg+xml");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to serve placeholder for missing file {Path}", fullPath);
                    }

                    Response.Headers["Cache-Control"] = "public, max-age=300";
                    return NotFound(new { message = "Image file not found" });
                }

                // Determine content type based on file extension
                var extension = Path.GetExtension(fullPath).ToLowerInvariant();
                var contentType = extension switch
                {
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".webp" => "image/webp",
                    ".svg" => "image/svg+xml",
                    _ => "application/octet-stream"
                };

                _logger.LogInformation("Serving cached image for identifier: {Identifier}, path: {Path}", identifier, relativePath);

                // Return the image with caching headers
                return PhysicalFile(fullPath, contentType, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving image for identifier: {Identifier}", identifier);
                return StatusCode(500, new { message = "Error retrieving image" });
            }
        }

        [HttpDelete("{identifier}")]
        public async Task<IActionResult> DeleteImage(string identifier)
        {
            if (string.IsNullOrEmpty(identifier))
            {
                return BadRequest("Identifier is required");
            }

            try
            {
                var relativePath = await _imageCacheService.GetCachedImagePathAsync(identifier);

                if (relativePath == null)
                {
                    return NotFound(new { message = "Image not found" });
                }

                var fullPath = Path.Combine(_environment.ContentRootPath, relativePath);

                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                    _logger.LogInformation("Deleted cached image for identifier: {Identifier}", identifier);
                    return Ok(new { message = "Image deleted successfully" });
                }

                return NotFound(new { message = "Image file not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting image for identifier: {Identifier}", identifier);
                return StatusCode(500, new { message = "Error deleting image" });
            }
        }
    }
}

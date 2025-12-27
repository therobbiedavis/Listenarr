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
        private readonly ILogger<ImagesController> _logger;
        private readonly IWebHostEnvironment _environment;

        public ImagesController(
            IImageCacheService imageCacheService,
            ILogger<ImagesController> logger,
            IWebHostEnvironment environment)
        {
            _imageCacheService = imageCacheService;
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

            try
            {
                // Get the cached image path (checks library first, then temp)
                var relativePath = await _imageCacheService.GetCachedImagePathAsync(identifier);

                if (relativePath == null)
                {
                    _logger.LogWarning("Image not found for identifier: {Identifier}", identifier);
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

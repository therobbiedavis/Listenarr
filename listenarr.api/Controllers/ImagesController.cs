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

            try
            {
                // Get the cached image path (checks library first, then temp)
                var relativePath = await _imageCacheService.GetCachedImagePathAsync(identifier);
                
                if (relativePath == null)
                {
                    _logger.LogWarning("Image not found for identifier: {Identifier}", identifier);
                    return NotFound(new { message = "Image not found" });
                }

                // Build the full file path
                var fullPath = Path.Combine(_environment.ContentRootPath, relativePath);
                
                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogWarning("Image file does not exist at path: {Path}", fullPath);
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
